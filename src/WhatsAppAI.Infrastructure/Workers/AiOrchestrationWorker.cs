using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Automation;
using WhatsAppAI.Application.Automation.Context;
using WhatsAppAI.Application.Automation.Policy;
using WhatsAppAI.Domain.Automation;
using WhatsAppAI.Domain.Audit;
using WhatsAppAI.Domain.Integrations;
using WhatsAppAI.Domain.Identity;
using WhatsAppAI.Domain.Knowledge;
using WhatsAppAI.Domain.Messaging;
using WhatsAppAI.Domain.Privacy;
using WhatsAppAI.Domain.Usage;
using WhatsAppAI.Infrastructure.Identity;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.Infrastructure.Workers;

public sealed class AiOrchestrationWorker(
    IServiceProvider serviceProvider,
    ILogger<AiOrchestrationWorker> logger) : BackgroundService
{
    private const int BatchSize = 20;
    private const int MaxConcurrency = 4;
    private const int MaxAiAttempts = 3;
    private static readonly TimeSpan AiClaimLease = TimeSpan.FromMinutes(5);
    private readonly ConcurrentDictionary<string, CircuitBreaker> _providerBreakers = new(StringComparer.OrdinalIgnoreCase);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("AI Orchestration Worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var hadWork = await ProcessInboundAsync(stoppingToken);
                await Task.Delay(
                    hadWork ? TimeSpan.FromMilliseconds(50) : TimeSpan.FromMilliseconds(100),
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in AI orchestration worker");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        logger.LogInformation("AI Orchestration Worker stopped");
    }

    private async Task<bool> ProcessInboundAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var messageRepository = scope.ServiceProvider.GetRequiredService<IMessageRepository>();
        var pendingInbound = await messageRepository.GetUnprocessedInboundAsync(BatchSize, cancellationToken);
        if (pendingInbound.Count == 0)
            return false;

        await Parallel.ForEachAsync(
            pendingInbound.GroupBy(message => message.ConversationId),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = MaxConcurrency,
                CancellationToken = cancellationToken
            },
            async (conversationMessages, ct) =>
            {
                foreach (var message in conversationMessages)
                {
                    using var itemScope = serviceProvider.CreateScope();
                    var itemMessageRepository = itemScope.ServiceProvider.GetRequiredService<IMessageRepository>();
                    if (!await itemMessageRepository.TryClaimInboundForAiAsync(
                            message.TenantId,
                            message.Id,
                            DateTime.UtcNow.Add(AiClaimLease),
                            ct))
                    {
                        continue;
                    }

                    await ProcessSingleInboundAsync(message, itemScope.ServiceProvider, ct);
                }
            });

        return true;
    }

    private async Task ProcessSingleInboundAsync(
        Message message,
        IServiceProvider scopedServices,
        CancellationToken cancellationToken)
    {
        var dbContext = scopedServices.GetRequiredService<AppDbContext>();
        Guid? responseQuotaReservation = null;
        AiResponseQuotaPackageType? responseQuotaPackageType = null;
        string? responseQuotaPackageReference = null;
        var responseQuotaFinalized = false;
        var botConfigRepository = scopedServices.GetRequiredService<IBotConfigurationRepository>();
        var queueRepository = scopedServices.GetRequiredService<IServiceLineRepository>();
        var tagRepository = scopedServices.GetRequiredService<IClientTagRepository>();
        var contactTagRepository = scopedServices.GetRequiredService<IContactTagRepository>();
        var messageRepository = scopedServices.GetRequiredService<IMessageRepository>();
        var conversationRepository = scopedServices.GetRequiredService<IConversationRepository>();
        var credentialRepository = scopedServices.GetRequiredService<IAiProviderCredentialRepository>();
        var secretStore = scopedServices.GetRequiredService<ISecretStore>();
        var aiProviderResolver = scopedServices.GetRequiredService<IAiProviderResolver>();
        var contextAssembler = scopedServices.GetRequiredService<ContextAssembler>();
        var outboxRepository = scopedServices.GetRequiredService<IOutboxMessageRepository>();
        var responseQuotaService = scopedServices.GetRequiredService<IAiResponseQuotaService>();
        var pricingRepository = scopedServices.GetRequiredService<IAiModelPricingRepository>();
        var modelEvaluationRepository = scopedServices.GetRequiredService<IModelEvaluationRepository>();
        var auditLogRepository = scopedServices.GetRequiredService<IAuditLogRepository>();
        var handoffEventRepository = scopedServices.GetRequiredService<IHandoffEventRepository>();
        long monthlyAiResponsesUsed = 0;
        int? effectiveMonthlyAiResponseLimit = null;

        try
        {
            var conversation = await conversationRepository.GetByIdAsync(message.ConversationId, cancellationToken);
            if (conversation is null)
            {
                logger.LogWarning("Conversation {ConversationId} not found for message {MessageId}",
                    message.ConversationId, message.Id);
                message.MarkProcessedByAi();
                await messageRepository.UpdateAsync(message, cancellationToken);
                return;
            }

            if (conversation.Mode != ConversationMode.Automatic)
            {
                message.MarkProcessedByAi();
                await messageRepository.UpdateAsync(message, cancellationToken);
                return;
            }

            var expectedConversationVersion = conversation.Version;

            // Do not process automated replies outside the WhatsApp 24-hour window.
            if (!AiReplyDeliveryGuard.CanSend(conversation, expectedConversationVersion, DateTime.UtcNow))
            {
                message.MarkProcessedByAi();
                await messageRepository.UpdateAsync(message, cancellationToken);
                logger.LogInformation("Automated reply blocked for closed or changed conversation {ConversationId}", message.ConversationId);
                return;
            }

            var tenant = await dbContext.Tenants.FindAsync([message.TenantId], cancellationToken);
            if (tenant?.Status != TenantStatus.Active)
            {
                message.MarkProcessedByAi();
                await messageRepository.UpdateAsync(message, cancellationToken);
                logger.LogInformation("Automation blocked for suspended tenant {TenantId}", message.TenantId);
                return;
            }

            // Check BotConfiguration mode
            var botConfig = await botConfigRepository.GetByTenantAsync(message.TenantId, cancellationToken);
            if (botConfig is null)
            {
                await FinalizeUnavailableAiAsync(
                    message, conversation, null, messageRepository, conversationRepository,
                    outboxRepository, handoffEventRepository, dbContext, cancellationToken);
                logger.LogWarning("Bot configuration not available for tenant {TenantId}", message.TenantId);
                return;
            }

            if (!botConfig.Enabled)
            {
                logger.LogInformation("Bot disabled for tenant {TenantId}, skipping", message.TenantId);
                message.MarkProcessedByAi();
                await messageRepository.UpdateAsync(message, cancellationToken);
                return;
            }

            if (botConfig.Mode == BotMode.Manual)
            {
                logger.LogInformation("Bot in Manual mode for tenant {TenantId}, skipping", message.TenantId);
                message.MarkProcessedByAi();
                await messageRepository.UpdateAsync(message, cancellationToken);
                return;
            }

            if (await HandleConsentOptInAsync(
                    message, conversation, expectedConversationVersion, dbContext,
                    messageRepository, outboxRepository, cancellationToken))
            {
                return;
            }

            // Handle SimpleAutoReply mode
            if (botConfig.Mode == BotMode.SimpleAutoReply)
            {
                if (!await dbContext.HasBotEnabledAsync(message.TenantId, cancellationToken))
                {
                    message.MarkProcessedByAi();
                    await messageRepository.UpdateAsync(message, cancellationToken);
                    logger.LogInformation(
                        "SimpleAutoReply blocked by plan for tenant {TenantId}",
                        message.TenantId);
                    return;
                }

                try
                {
                    var withinBusinessHours = BusinessHoursPolicy.IsOpen(
                        botConfig.BusinessHoursEnabled,
                        botConfig.BusinessHoursJson,
                        botConfig.TimeZoneId,
                        DateTime.UtcNow);
                    string? replyContent;
                    if (!withinBusinessHours)
                    {
                        replyContent = botConfig.OfflineMessage;
                        if (string.IsNullOrWhiteSpace(replyContent))
                        {
                            message.MarkProcessedByAi();
                            await messageRepository.UpdateAsync(message, cancellationToken);
                            logger.LogInformation("SimpleAutoReply skipped outside business hours for tenant {TenantId}", message.TenantId);
                            return;
                        }
                    }
                    else
                    {
                        var previousInboundCount = await dbContext.Messages
                            .IgnoreQueryFilters()
                            .CountAsync(item => item.ConversationId == message.ConversationId &&
                                item.Direction == MessageDirection.Inbound && item.Id != message.Id, cancellationToken);
                        replyContent = FindFlowReply(botConfig.FlowStepsJson, message.Content)
                            ?? (previousInboundCount > 0 ? botConfig.ReturningMessage : botConfig.WelcomeMessage)
                            ?? botConfig.FallbackMessage;
                    }

                    if (string.IsNullOrWhiteSpace(replyContent))
                        replyContent = "Obrigado pela sua mensagem. Em breve retornaremos o contato.";

                    // The flow lookup is asynchronous; revalidate human takeover, version and window.
                    await dbContext.Entry(conversation).ReloadAsync(cancellationToken);
                    if (!AiReplyDeliveryGuard.CanSend(
                            conversation, expectedConversationVersion, DateTime.UtcNow))
                    {
                        message.MarkProcessedByAi();
                        await messageRepository.UpdateAsync(message, cancellationToken);
                        logger.LogInformation("SimpleAutoReply discarded after conversation {ConversationId} changed", message.ConversationId);
                        return;
                    }

                    ServiceLine? routingQueue = null;
                    if (withinBusinessHours &&
                        await dbContext.HasAutomaticDistributionEnabledAsync(message.TenantId, cancellationToken))
                    {
                        var activeQueues = await queueRepository.GetActiveByTenantAsync(
                            message.TenantId, cancellationToken);
                        routingQueue = SelectBotRoutingQueue(
                            conversation.QueueId, activeQueues, message.Content);
                    }

                    if (routingQueue is not null)
                    {
                        await PersistAutomaticHandoffAsync(
                            message.TenantId,
                            message,
                            conversation,
                            "queue_selection",
                            ResolveQueueTransferMessage(botConfig),
                            "bot-queue-transfer",
                            dbContext,
                            messageRepository,
                            conversationRepository,
                            outboxRepository,
                            handoffEventRepository,
                            cancellationToken,
                            routingQueue.Id);
                        logger.LogInformation(
                            "SimpleAutoReply routed conversation {ConversationId} to queue {QueueName}",
                            conversation.Id,
                            routingQueue.Name);
                        return;
                    }

                    var outboundMsg = Message.CreateOutbound(
                        message.TenantId, message.ConversationId, message.ContactId,
                        MessageType.Text,
                        replyContent,
                        AiReplyDeliveryGuard.CreateAutomatedIdempotencyKey(
                            "simple-auto-reply", message.Id, expectedConversationVersion));
                    var outboxMsg = OutboxMessage.Create(message.TenantId, outboundMsg.Id);
                    message.MarkProcessedByAi();
                    dbContext.Set<Message>().Add(outboundMsg);
                    dbContext.Set<OutboxMessage>().Add(outboxMsg);
                    dbContext.Set<Message>().Update(message);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    logger.LogInformation("SimpleAutoReply sent for tenant {TenantId}", message.TenantId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "SimpleAutoReply failed for message {MessageId}", message.Id);
                    throw new InvalidOperationException($"SimpleAutoReply failed for message {message.Id}.", ex);
                }
                return;
            }

            // Check if tenant plan has AI enabled
            if (!await dbContext.HasAiEnabledAsync(message.TenantId, cancellationToken))
            {
                await FinalizeUnavailableAiAsync(
                    message, conversation, botConfig, messageRepository, conversationRepository,
                    outboxRepository, handoffEventRepository, dbContext, cancellationToken);
                logger.LogWarning("AI not enabled for tenant {TenantId} plan", message.TenantId);
                return;
            }

            var credential = await credentialRepository.GetByTenantAsync(message.TenantId, cancellationToken);
            if (credential is null || !credential.IsActive)
            {
                await FinalizeUnavailableAiAsync(
                    message, conversation, botConfig, messageRepository, conversationRepository,
                    outboxRepository, handoffEventRepository, dbContext, cancellationToken);
                logger.LogWarning("No active AI credential for tenant {TenantId}", message.TenantId);
                return;
            }

            var apiKey = string.IsNullOrWhiteSpace(credential.ApiKeyRef)
                ? null
                : await secretStore.GetAsync(credential.ApiKeyRef, cancellationToken);
            if (string.IsNullOrEmpty(apiKey))
            {
                await FinalizeUnavailableAiAsync(
                    message, conversation, botConfig, messageRepository, conversationRepository,
                    outboxRepository, handoffEventRepository, dbContext, cancellationToken);
                logger.LogWarning("API key not available for tenant {TenantId}", message.TenantId);
                return;
            }
            if (!AiModelPolicy.IsAllowed(credential.Provider, credential.ModelId))
            {
                await FinalizeUnavailableAiAsync(
                    message, conversation, botConfig, messageRepository, conversationRepository,
                    outboxRepository, handoffEventRepository, dbContext, cancellationToken);
                logger.LogWarning("AI model is not allowed for tenant {TenantId}", message.TenantId);
                return;
            }
            if (await modelEvaluationRepository.GetApprovedForModelAsync(
                    message.TenantId, credential.Provider, credential.ModelId, cancellationToken) is null)
            {
                await FinalizeUnavailableAiAsync(
                    message, conversation, botConfig, messageRepository, conversationRepository,
                    outboxRepository, handoffEventRepository, dbContext, cancellationToken);
                logger.LogWarning("AI model has no approved evaluation for tenant {TenantId}", message.TenantId);
                return;
            }

            var purposes = await dbContext.ProcessingPurposes
                .IgnoreQueryFilters()
                .Where(purpose => purpose.TenantId == message.TenantId && purpose.IsActive)
                .ToListAsync(cancellationToken);
            var consentPurposeIds = purposes
                .Where(purpose => purpose.LegalBasis == WhatsAppAI.Domain.Privacy.LegalBasis.Consent)
                .Select(purpose => purpose.Id)
                .ToList();
            List<WhatsAppAI.Domain.Privacy.ConsentEvidence> consents = consentPurposeIds.Count == 0
                ? []
                : await dbContext.ConsentEvidence
                    .IgnoreQueryFilters()
                    .Where(consent => consent.TenantId == message.TenantId &&
                        consent.ContactId == message.ContactId &&
                        consentPurposeIds.Contains(consent.ProcessingPurposeId))
                    .ToListAsync(cancellationToken);
            if (!AiDataProcessingPolicy.IsAuthorized(message.TenantId, message.ContactId, purposes, consents))
            {
                await PersistAutomaticHandoffAsync(
                    message.TenantId, message, conversation, "data_processing_not_authorized", null, "ai-data-policy",
                    dbContext, messageRepository, conversationRepository, outboxRepository,
                    handoffEventRepository, cancellationToken);
                logger.LogInformation("AI data processing is not authorized for tenant {TenantId}", message.TenantId);
                return;
            }

            // Resolve the correct AI provider based on the credential's provider name
            IAiProvider aiProvider;
            try
            {
                aiProvider = aiProviderResolver.Resolve(credential.Provider);
            }
            catch (InvalidOperationException ex)
            {
                await FinalizeUnavailableAiAsync(
                    message, conversation, botConfig, messageRepository, conversationRepository,
                    outboxRepository, handoffEventRepository, dbContext, cancellationToken);
                logger.LogWarning(ex, "AI provider '{Provider}' not available for tenant {TenantId}", credential.Provider, message.TenantId);
                return;
            }

            var configuredQueueIds = await dbContext.HasAutomaticDistributionEnabledAsync(
                message.TenantId, cancellationToken)
                ? credential.GetRoutingQueueIds().ToHashSet()
                : [];
            List<ServiceLine> routingQueues = configuredQueueIds.Count == 0
                ? []
                : (await queueRepository.GetActiveByTenantAsync(message.TenantId, cancellationToken))
                    .Where(queue => configuredQueueIds.Contains(queue.Id))
                    .ToList();

            var configuredTagIds = await dbContext.HasTagsEnabledAsync(
                message.TenantId, cancellationToken)
                ? credential.GetRoutingTagIds().ToHashSet()
                : [];
            List<ClientTag> routingTags = configuredTagIds.Count == 0
                ? []
                : (await tagRepository.GetActiveByTenantAsync(message.TenantId, cancellationToken))
                    .Where(tag => configuredTagIds.Contains(tag.Id))
                    .ToList();

            var context = await contextAssembler.BuildAsync(
                message.TenantId, message.ConversationId, credential.SystemPrompt,
                routingQueues
                    .Select(queue => new RoutingQueueContext(queue.Name, queue.Description))
                    .ToList(),
                routingTags
                    .Select(tag => new RoutingTagContext(tag.Name, tag.Description))
                    .ToList(),
                cancellationToken);

            var request = new AiRequest
            {
                ModelId = credential.ModelId,
                ApiKey = apiKey,
                Messages = context.Messages,
                SystemPrompt = context.SystemPrompt,
                MaxTokens = Math.Clamp(credential.MaxTokensPerResponse, 48, 120)
            };

            var pricing = await pricingRepository.GetActiveAsync(
                credential.Provider, credential.ModelId, DateTime.UtcNow, cancellationToken);
            var quotaResult = await responseQuotaService.TryReserveAsync(
                message.TenantId,
                message.Id,
                AiReplyDeliveryGuard.CreateIdempotencyKey(message.Id, expectedConversationVersion),
                cancellationToken);
            if (quotaResult.IsExisting)
            {
                message.MarkProcessedByAi();
                await messageRepository.UpdateAsync(message, cancellationToken);
                logger.LogInformation(
                    "AI response reservation already exists for message {MessageId} with status {Status}",
                    message.Id,
                    quotaResult.ReservationStatus);
                return;
            }
            if (!quotaResult.IsReserved)
            {
                await FinalizeAiResponseQuotaExceededAsync(
                    message, conversation, expectedConversationVersion, botConfig, dbContext, messageRepository,
                    conversationRepository, outboxRepository, auditLogRepository, handoffEventRepository,
                    quotaResult.Snapshot.EffectiveLimit,
                    checked(quotaResult.Snapshot.CommittedResponses + quotaResult.Snapshot.PendingReservations),
                    cancellationToken);
                logger.LogWarning("Monthly AI response quota exhausted for tenant {TenantId}", message.TenantId);
                return;
            }
            responseQuotaReservation = quotaResult.ReservationId;
            responseQuotaPackageType = quotaResult.PackageType;
            responseQuotaPackageReference = quotaResult.PackageReference;

            var providerBreaker = _providerBreakers.GetOrAdd(
                $"{message.TenantId:N}:{credential.Provider}",
                _ => new CircuitBreaker());
            if (!providerBreaker.CanExecute())
            {
                await responseQuotaService.ReleaseAsync(
                    message.TenantId, responseQuotaReservation!.Value, "provider-circuit-open", cancellationToken);
                responseQuotaFinalized = true;
                await FinalizeUnavailableAiAsync(
                    message, conversation, botConfig, messageRepository, conversationRepository,
                    outboxRepository, handoffEventRepository, dbContext, cancellationToken);
                logger.LogWarning(
                    "AI provider circuit is open for tenant {TenantId} and provider {Provider}",
                    message.TenantId, credential.Provider);
                return;
            }

            AiResponse response;
            try
            {
                response = await aiProvider.GetResponseAsync(request, cancellationToken);
                providerBreaker.RecordSuccess();
            }
            catch
            {
                providerBreaker.RecordFailure();
                throw;
            }
            // Apply behavior policy
            var sanitizedResponse = BehaviorPolicy.SanitizeResponse(
                response, botConfig.ConfidenceThreshold);
            response = sanitizedResponse with
            {
                Decision = DefaultGreetingPolicy.Apply(sanitizedResponse.Decision, message.Content)
            };
            var routingResult = QueueRoutingPolicy.Apply(
                response.Decision,
                routingQueues.Select(queue => new RoutingQueueCandidate(queue.Id, queue.Name)).ToList(),
                conversation.QueueId is not null);
            response = response with { Decision = routingResult.Decision };
            var categorizedTagIds = TagCategorizationPolicy.ResolveAuthorizedTagIds(
                response.Decision.TagNames,
                routingTags.Select(tag => new RoutingTagCandidate(tag.Id, tag.Name)).ToList());

            // Persist interaction (no prompt/response content)
            var interaction = AiInteraction.Create(
                message.TenantId, message.ConversationId, message.Id,
                credential.ModelId, response.Decision.Action.ToString(),
                response.Decision.HandoffReason, response.Decision.Confidence,
                response.InputTokens, response.OutputTokens, 0, response.RawResponseId);
            dbContext.Set<AiInteraction>().Add(interaction);

            // Persist usage ledger with actual provider name
            if (response.InputTokens > 0)
            {
                var usage = UsageLedger.Create(
                    message.TenantId, credential.Provider, "input_tokens",
                    response.RawResponseId ?? message.Id.ToString(),
                    response.InputTokens, "tokens",
                    pricing?.CalculateCostMinorUnits(response.InputTokens, input: true),
                    pricing?.Currency,
                    pricing?.Version,
                    responseQuotaPackageType,
                    responseQuotaPackageReference);
                dbContext.Set<UsageLedger>().Add(usage);
            }
            if (response.OutputTokens > 0)
            {
                var usage = UsageLedger.Create(
                    message.TenantId, credential.Provider, "output_tokens",
                    response.RawResponseId ?? message.Id.ToString(),
                    response.OutputTokens, "tokens",
                    pricing?.CalculateCostMinorUnits(response.OutputTokens, input: false),
                    pricing?.Currency,
                    pricing?.Version,
                    responseQuotaPackageType,
                    responseQuotaPackageReference);
                dbContext.Set<UsageLedger>().Add(usage);
            }

            // Interaction and token usage are independent rows but share this
            // context; flush them together before delivery revalidation.
            await dbContext.SaveChangesAsync(cancellationToken);

            // Revalidate persisted state after the AI call.
            await dbContext.Entry(conversation).ReloadAsync(cancellationToken);
            if (!AiReplyDeliveryGuard.CanSend(
                    conversation, expectedConversationVersion, DateTime.UtcNow))
            {
                logger.LogInformation("Conversation {ConversationId} changed during AI call, discarding decision", message.ConversationId);
                message.MarkProcessedByAi();
                await messageRepository.UpdateAsync(message, cancellationToken);
                return;
            }

            // Auto-assign queue if AI categorized and conversation has no queue yet
            if (routingResult.QueueId is Guid routingQueueId && conversation.QueueId is null)
            {
                conversation.AssignQueue(routingQueueId);
                await conversationRepository.UpdateAsync(conversation, cancellationToken);
                // Queue routing is an internal change; use its new version for the final delivery guard.
                expectedConversationVersion = conversation.Version;
                logger.LogInformation("Conversation {ConversationId} auto-assigned to queue {QueueName}",
                    conversation.Id, response.Decision.QueueName);

                // Send queue transfer message to client
                var queueTransferText = !string.IsNullOrWhiteSpace(botConfig.QueueTransferMessage)
                    ? botConfig.QueueTransferMessage
                    : "Estou transferindo seu atendimento para a fila especializada. Por favor, aguarde.";

                var queueTransferMsg = Message.CreateOutbound(
                    message.TenantId, conversation.Id, message.ContactId,
                    MessageType.Text,
                    queueTransferText,
                    AiReplyDeliveryGuard.CreateAutomatedIdempotencyKey(
                        "ai-queue-transfer", message.Id, expectedConversationVersion));
                await messageRepository.AddAsync(queueTransferMsg, cancellationToken);

                var queueTransferOutbox = OutboxMessage.Create(message.TenantId, queueTransferMsg.Id);
                await outboxRepository.AddAsync(queueTransferOutbox);

                // Apply tag with same name as the queue (if it exists for this tenant)
                var queueName = response.Decision.QueueName;
                if (!string.IsNullOrWhiteSpace(queueName))
                {
                    var allTenantTags = await tagRepository.GetActiveByTenantAsync(message.TenantId, cancellationToken);
                    var queueTag = allTenantTags.FirstOrDefault(t =>
                        t.Name.Equals(queueName, StringComparison.OrdinalIgnoreCase));
                    if (queueTag is not null &&
                        !await contactTagRepository.ExistsAsync(message.TenantId, message.ContactId, queueTag.Id, cancellationToken))
                    {
                        await contactTagRepository.AddAsync(
                            ContactTag.Create(message.ContactId, queueTag.Id, message.TenantId),
                            cancellationToken);
                        logger.LogInformation("Tag '{TagName}' applied to contact {ContactId} via queue routing",
                            queueTag.Name, message.ContactId);
                    }
                }
            }

            foreach (var tagId in categorizedTagIds)
            {
                if (await contactTagRepository.ExistsAsync(message.TenantId, message.ContactId, tagId, cancellationToken))
                    continue;

                await contactTagRepository.AddAsync(
                    ContactTag.Create(message.ContactId, tagId, message.TenantId),
                    cancellationToken);
            }

            if (response.Decision.Action == AiAction.Reply)
            {
                // Final check immediately before creating any customer-facing message/outbox.
                await dbContext.Entry(conversation).ReloadAsync(cancellationToken);
                if (!AiReplyDeliveryGuard.CanSend(
                        conversation, expectedConversationVersion, DateTime.UtcNow))
                {
                    await responseQuotaService.ReleaseAsync(
                        message.TenantId, responseQuotaReservation!.Value, "conversation-changed", cancellationToken);
                    responseQuotaFinalized = true;
                    message.MarkProcessedByAi();
                    await messageRepository.UpdateAsync(message, cancellationToken);
                    logger.LogInformation("AI reply discarded after conversation {ConversationId} changed", message.ConversationId);
                    return;
                }

                if (string.IsNullOrWhiteSpace(response.Content))
                {
                    await responseQuotaService.ReleaseAsync(
                        message.TenantId, responseQuotaReservation!.Value, "empty-ai-reply", cancellationToken);
                    responseQuotaFinalized = true;
                    await PersistAutomaticHandoffAsync(
                        message.TenantId, message, conversation, "empty_ai_reply", ResolveHandoffMessage(botConfig),
                        "ai-empty-reply", dbContext, messageRepository, conversationRepository,
                        outboxRepository, handoffEventRepository, cancellationToken);
                    logger.LogWarning("AI returned an empty reply; conversation {ConversationId} transferred to human", message.ConversationId);
                    return;
                }

                await using var quotaTransaction = await dbContext.Database
                    .BeginTransactionAsync(cancellationToken);
                if (dbContext.Database.IsNpgsql())
                {
                    await dbContext.Database.ExecuteSqlInterpolatedAsync(
                        $"SELECT pg_advisory_xact_lock(hashtext({message.TenantId.ToString()}))",
                        cancellationToken);
                }

                var quotaSnapshot = await responseQuotaService.GetSnapshotAsync(
                    message.TenantId, cancellationToken);
                monthlyAiResponsesUsed = quotaSnapshot.CommittedResponses;
                effectiveMonthlyAiResponseLimit = quotaSnapshot.EffectiveLimit;

                await dbContext.Entry(conversation).ReloadAsync(cancellationToken);
                if (!AiReplyDeliveryGuard.CanSend(
                        conversation, expectedConversationVersion, DateTime.UtcNow))
                {
                    await quotaTransaction.RollbackAsync(cancellationToken);
                    await quotaTransaction.DisposeAsync();
                    await responseQuotaService.ReleaseAsync(
                        message.TenantId, responseQuotaReservation!.Value, "conversation-changed", cancellationToken);
                    responseQuotaFinalized = true;
                    message.MarkProcessedByAi();
                    await messageRepository.UpdateAsync(message, cancellationToken);
                    logger.LogInformation(
                        "AI reply discarded after conversation {ConversationId} changed while reserving quota",
                        message.ConversationId);
                    return;
                }

                var replyMessage = Message.CreateOutbound(
                    message.TenantId,
                    message.ConversationId,
                    message.ContactId,
                    MessageType.Text,
                    response.Content,
                    AiReplyDeliveryGuard.CreateIdempotencyKey(
                        message.Id, expectedConversationVersion));

                var outboxMessage = OutboxMessage.Create(message.TenantId, replyMessage.Id);

                var responseUsage = UsageLedger.Create(
                    message.TenantId,
                    credential.Provider,
                    UsageMetricNames.AiResponses,
                    replyMessage.Id.ToString(),
                    1,
                    "responses",
                    aiResponseQuotaPackageType: responseQuotaPackageType,
                    aiResponseQuotaPackageReference: responseQuotaPackageReference);
                dbContext.Set<Message>().Add(replyMessage);
                dbContext.Set<OutboxMessage>().Add(outboxMessage);
                dbContext.Set<UsageLedger>().Add(responseUsage);
                await responseQuotaService.CommitAsync(
                    message.TenantId, responseQuotaReservation!.Value, cancellationToken);
                await RegisterAiQuotaAuditAsync(
                    dbContext,
                    auditLogRepository,
                    message.TenantId,
                    effectiveMonthlyAiResponseLimit,
                    monthlyAiResponsesUsed + 1,
                    transactionAlreadyHeld: true,
                    cancellationToken);

                conversation.RecordMessage();
                dbContext.Set<Conversation>().Update(conversation);

                message.MarkProcessedByAi();
                dbContext.Set<Message>().Update(message);

                // RegisterAiQuotaAuditAsync saves pending entities when it needs
                // to create the audit row; save explicitly when no audit was due.
                await dbContext.SaveChangesAsync(cancellationToken);

                await quotaTransaction.CommitAsync(cancellationToken);
                responseQuotaFinalized = true;

                logger.LogInformation("AI reply created for message {MessageId}", message.Id);
                return;
            }
            else if (response.Decision.Action == AiAction.Handoff)
            {
                await responseQuotaService.ReleaseAsync(
                    message.TenantId, responseQuotaReservation!.Value, "ai-handoff", cancellationToken);
                responseQuotaFinalized = true;
                await dbContext.Entry(conversation).ReloadAsync(cancellationToken);
                if (!AiReplyDeliveryGuard.CanSend(
                        conversation, expectedConversationVersion, DateTime.UtcNow))
                {
                    message.MarkProcessedByAi();
                    await messageRepository.UpdateAsync(message, cancellationToken);
                    logger.LogInformation("AI handoff message discarded after conversation {ConversationId} changed or window closed", message.ConversationId);
                    return;
                }

                await PersistAutomaticHandoffAsync(
                    message.TenantId, message, conversation,
                    response.Decision.HandoffReason ?? "handoff",
                    ResolveHandoffMessage(botConfig), "ai-handoff", dbContext,
                    messageRepository, conversationRepository, outboxRepository,
                    handoffEventRepository, cancellationToken);

                logger.LogInformation("AI handoff for conversation {ConversationId}: {Reason}",
                    conversation.Id, response.Decision.HandoffReason);
                return;
            }

            await responseQuotaService.ReleaseAsync(
                message.TenantId, responseQuotaReservation!.Value, "non-reply-ai-decision", cancellationToken);
            responseQuotaFinalized = true;
            message.MarkProcessedByAi();
            await messageRepository.UpdateAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            if (responseQuotaReservation is not null && !responseQuotaFinalized)
            {
                await responseQuotaService.ReleaseAsync(
                    message.TenantId, responseQuotaReservation.Value, "processing-failed", cancellationToken);
            }
            logger.LogError(ex, "Error processing inbound message {MessageId} for AI", message.Id);
            var errorText = ex.ToString();

            if (errorText.Contains("429", StringComparison.Ordinal) ||
                errorText.Contains("quota", StringComparison.OrdinalIgnoreCase))
            {
                var botConfig = await botConfigRepository.GetByTenantAsync(message.TenantId, cancellationToken);
                var currentConversation = await conversationRepository.GetByIdAsync(message.ConversationId, cancellationToken);
                if (currentConversation is not null && currentConversation.Mode == ConversationMode.Automatic && currentConversation.IsWindowOpen(DateTime.UtcNow))
                {
                    await PersistAutomaticHandoffAsync(
                        message.TenantId, message, currentConversation, "ai_quota_exhausted",
                        ResolveHandoffMessage(botConfig), "ai-quota", dbContext, messageRepository,
                        conversationRepository, outboxRepository, handoffEventRepository, cancellationToken);
                }
                else
                {
                    message.MarkProcessedByAi();
                    await messageRepository.UpdateAsync(message, cancellationToken);
                }
                logger.LogWarning("AI quota exhausted; conversation {ConversationId} transferred to human", message.ConversationId);
                return;
            }

            var retryDelay = TimeSpan.FromSeconds(Math.Pow(2, message.AiRetryCount) * 10);
            if (message.RegisterAiFailure(MaxAiAttempts, retryDelay))
            {
                await messageRepository.UpdateAsync(message, cancellationToken);
                logger.LogWarning("AI attempt {Attempt} scheduled for message {MessageId}", message.AiRetryCount, message.Id);
                return;
            }

            var failedConversation = await conversationRepository.GetByIdAsync(message.ConversationId, cancellationToken);
            if (failedConversation is not null && failedConversation.Mode == ConversationMode.Automatic && failedConversation.IsWindowOpen(DateTime.UtcNow))
            {
                var botConfig = await botConfigRepository.GetByTenantAsync(message.TenantId, cancellationToken);
                await PersistAutomaticHandoffAsync(
                    message.TenantId, message, failedConversation, "ai_retry_exhausted",
                    ResolveHandoffMessage(botConfig), "ai-retry-exhausted", dbContext, messageRepository,
                    conversationRepository, outboxRepository, handoffEventRepository, cancellationToken);
            }
            else
            {
                message.MarkProcessedByAi();
                await messageRepository.UpdateAsync(message, cancellationToken);
            }
            logger.LogWarning("AI retries exhausted; conversation {ConversationId} transferred to human", message.ConversationId);
        }
    }

    private async Task<bool> HandleConsentOptInAsync(
        Message message,
        Conversation conversation,
        uint expectedConversationVersion,
        AppDbContext dbContext,
        IMessageRepository messageRepository,
        IOutboxMessageRepository outboxRepository,
        CancellationToken cancellationToken)
    {
        var isAuthorizedByAnotherLegalBasis = await dbContext.ProcessingPurposes
            .IgnoreQueryFilters()
            .AnyAsync(item =>
                item.TenantId == message.TenantId &&
                item.IsActive &&
                item.LegalBasis != LegalBasis.Consent,
                cancellationToken);
        if (isAuthorizedByAnotherLegalBasis)
            return false;

        var purpose = await dbContext.ProcessingPurposes
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(item =>
                item.TenantId == message.TenantId &&
                item.IsActive &&
                item.LegalBasis == LegalBasis.Consent &&
                item.Name == AiConsentOptInPolicy.DefaultPurposeName,
                cancellationToken);
        if (purpose is null)
            return false;

        var isAuthorized = await dbContext.ConsentEvidence
            .IgnoreQueryFilters()
            .AnyAsync(item =>
                item.TenantId == message.TenantId &&
                item.ContactId == message.ContactId &&
                item.ProcessingPurposeId == purpose.Id &&
                !item.RevokedAt.HasValue,
                cancellationToken);
        if (isAuthorized)
            return false;

        if (AiConsentOptInPolicy.IsAccepted(message.Content))
        {
            var evidence = ConsentEvidence.Create(
                message.TenantId,
                message.ContactId,
                purpose,
                "WhatsAppOptIn",
                message.ExternalId,
                message.CreatedAt,
                purpose.CreatedByUserId);
            var confirmation = Message.CreateOutbound(
                message.TenantId,
                message.ConversationId,
                message.ContactId,
                MessageType.Text,
                AiConsentOptInPolicy.ConfirmationMessage,
                AiReplyDeliveryGuard.CreateAutomatedIdempotencyKey(
                    "consent-confirmation", message.Id, expectedConversationVersion));

            message.MarkProcessedByAi();
            dbContext.ConsentEvidence.Add(evidence);
            dbContext.Messages.Add(confirmation);
            dbContext.OutboxMessages.Add(OutboxMessage.Create(message.TenantId, confirmation.Id));
            dbContext.AuditLogs.Add(AuditLog.Create(
                message.TenantId,
                null,
                "Privacy.ConsentRecordedByContact",
                "ConsentEvidence",
                evidence.Id.ToString()));
            dbContext.Messages.Update(message);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("AI consent recorded from WhatsApp for contact {ContactId}", message.ContactId);
            return true;
        }

        var requestIdempotencyKey = $"consent-request:{conversation.Id}";
        var requestAlreadyQueued = await dbContext.Messages
            .IgnoreQueryFilters()
            .AnyAsync(item =>
                item.TenantId == message.TenantId &&
                item.ConversationId == message.ConversationId &&
                item.IdempotencyKey == requestIdempotencyKey,
                cancellationToken);
        if (!requestAlreadyQueued && AiReplyDeliveryGuard.CanSend(conversation, expectedConversationVersion, DateTime.UtcNow))
        {
            var request = Message.CreateOutbound(
                message.TenantId,
                message.ConversationId,
                message.ContactId,
                MessageType.Text,
                AiConsentOptInPolicy.RequestMessage,
                requestIdempotencyKey);
            dbContext.Messages.Add(request);
            dbContext.OutboxMessages.Add(OutboxMessage.Create(message.TenantId, request.Id));
        }

        message.MarkProcessedByAi();
        dbContext.Messages.Update(message);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("AI consent requested for contact {ContactId}", message.ContactId);
        return true;
    }

    private static async Task FinalizeUnavailableAiAsync(
        Message message,
        Conversation conversation,
        BotConfiguration? botConfig,
        IMessageRepository messageRepository,
        IConversationRepository conversationRepository,
        IOutboxMessageRepository outboxRepository,
        IHandoffEventRepository handoffEventRepository,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!conversation.IsWindowOpen(DateTime.UtcNow))
        {
            message.MarkProcessedByAi();
            await messageRepository.UpdateAsync(message, cancellationToken);
            return;
        }

        await PersistAutomaticHandoffAsync(
            message.TenantId, message, conversation, "ai_unavailable",
            ResolveHandoffMessage(botConfig), "ai-unavailable", dbContext, messageRepository,
            conversationRepository, outboxRepository, handoffEventRepository, cancellationToken);
    }

    private static async Task FinalizeAiResponseQuotaExceededAsync(
        Message message,
        Conversation conversation,
        uint expectedConversationVersion,
        BotConfiguration? botConfig,
        AppDbContext dbContext,
        IMessageRepository messageRepository,
        IConversationRepository conversationRepository,
        IOutboxMessageRepository outboxRepository,
        IAuditLogRepository auditLogRepository,
        IHandoffEventRepository handoffEventRepository,
        int? monthlyLimit,
        long monthlyResponsesUsed,
        CancellationToken cancellationToken)
    {
        await dbContext.Entry(conversation).ReloadAsync(cancellationToken);
        if (!AiReplyDeliveryGuard.CanSend(
                conversation, expectedConversationVersion, DateTime.UtcNow))
        {
            message.MarkProcessedByAi();
            await messageRepository.UpdateAsync(message, cancellationToken);
            return;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await RegisterAiQuotaAuditAsync(
                dbContext,
                auditLogRepository,
                message.TenantId,
                monthlyLimit,
                monthlyResponsesUsed,
                transactionAlreadyHeld: true,
                cancellationToken);

            await PersistAutomaticHandoffInTransactionAsync(
                message.TenantId, message, conversation, "ai_quota_exhausted",
                ResolveHandoffMessage(botConfig), "ai-quota", messageRepository,
                conversationRepository, outboxRepository, handoffEventRepository, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task RegisterAiQuotaAuditAsync(
        AppDbContext dbContext,
        IAuditLogRepository auditLogRepository,
        Guid tenantId,
        int? monthlyLimit,
        long monthlyResponsesUsed,
        bool transactionAlreadyHeld,
        CancellationToken cancellationToken)
    {
        var level = AiQuotaAlertPolicy.GetLevel(monthlyLimit, monthlyResponsesUsed);
        if (level is null)
            return;

        var period = $"{DateTime.UtcNow:yyyy-MM}";
        var action = AiQuotaAlertPolicy.GetAuditAction(level.Value);
        var entityId = $"{period}:{level.Value}";

        if (!transactionAlreadyHeld)
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            if (dbContext.Database.IsNpgsql())
            {
                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock(hashtext({tenantId.ToString()}))",
                    cancellationToken);
            }

            await AddAiQuotaAuditIfMissingAsync(
                auditLogRepository, tenantId, action, entityId, period,
                monthlyLimit, monthlyResponsesUsed, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        await AddAiQuotaAuditIfMissingAsync(
            auditLogRepository, tenantId, action, entityId, period,
            monthlyLimit, monthlyResponsesUsed, cancellationToken);
    }

    private static async Task AddAiQuotaAuditIfMissingAsync(
        IAuditLogRepository auditLogRepository,
        Guid tenantId,
        string action,
        string entityId,
        string period,
        int? monthlyLimit,
        long monthlyResponsesUsed,
        CancellationToken cancellationToken)
    {
        if (await auditLogRepository.ExistsAsync(tenantId, action, entityId, cancellationToken))
            return;

        await auditLogRepository.AddAsync(AuditLog.Create(
            tenantId,
            null,
            action,
            "AiResponseQuota",
            entityId,
            $"period={period};used={monthlyResponsesUsed};limit={monthlyLimit?.ToString() ?? "unlimited"}"),
            cancellationToken);
    }

    internal static Message ApplyUnavailableAiFallback(
        Message message,
        BotConfiguration? botConfig,
        uint? expectedConversationVersion = null)
    {
        message.MarkProcessedByAi();

        return Message.CreateOutbound(
            message.TenantId,
            message.ConversationId,
            message.ContactId,
            MessageType.Text,
            ResolveHandoffMessage(botConfig),
            expectedConversationVersion is uint version
                ? AiReplyDeliveryGuard.CreateAutomatedIdempotencyKey("ai-unavailable", message.Id, version)
                : $"ai-unavailable:{message.Id}");
    }

    internal static string ResolveHandoffMessage(BotConfiguration? botConfig)
    {
        var handoffMessage = botConfig?.HandoffMessage;
        if (!string.IsNullOrWhiteSpace(handoffMessage))
            return handoffMessage;

        var fallbackMessage = botConfig?.FallbackMessage;
        if (!string.IsNullOrWhiteSpace(fallbackMessage))
            return fallbackMessage;

        return "Vou encaminhar voce para um atendente.";
    }

    internal static string ResolveQueueTransferMessage(BotConfiguration? botConfig)
    {
        if (!string.IsNullOrWhiteSpace(botConfig?.QueueTransferMessage))
            return botConfig.QueueTransferMessage;

        return "Estou transferindo seu atendimento para a fila especializada. Por favor, aguarde.";
    }

    internal static ServiceLine? SelectBotRoutingQueue(
        Guid? assignedQueueId,
        IReadOnlyList<ServiceLine> activeQueues,
        string? messageContent)
    {
        var text = messageContent ?? string.Empty;
        return assignedQueueId is Guid queueId
            ? activeQueues.FirstOrDefault(queue => queue.Id == queueId && queue.MatchesKeywords(text))
            : activeQueues.FirstOrDefault(queue => queue.MatchesKeywords(text));
    }

    internal static async Task<bool> RegisterAutomaticHandoffAsync(
        Guid tenantId,
        Conversation conversation,
        string reason,
        IConversationRepository conversationRepository,
        IHandoffEventRepository handoffEventRepository,
        CancellationToken cancellationToken)
    {
        if (conversation.Mode == ConversationMode.Human)
            return false;

        var previousMode = conversation.SwitchMode(ConversationMode.Human, conversation.Version, null);
        await conversationRepository.UpdateAsync(conversation, cancellationToken);
        await handoffEventRepository.AddAsync(HandoffEvent.Create(
            tenantId, conversation.Id, previousMode, ConversationMode.Human, null, reason));
        return true;
    }

    internal static async Task<bool> PersistAutomaticHandoffAsync(
        Guid tenantId,
        Message inboundMessage,
        Conversation conversation,
        string reason,
        string? handoffText,
        string idempotencyPrefix,
        AppDbContext dbContext,
        IMessageRepository messageRepository,
        IConversationRepository conversationRepository,
        IOutboxMessageRepository outboxRepository,
        IHandoffEventRepository handoffEventRepository,
        CancellationToken cancellationToken,
        Guid? queueId = null)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var registered = await PersistAutomaticHandoffInTransactionAsync(
                tenantId, inboundMessage, conversation, reason, handoffText, idempotencyPrefix,
                messageRepository, conversationRepository, outboxRepository, handoffEventRepository,
                cancellationToken, queueId);
            await transaction.CommitAsync(cancellationToken);
            return registered;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task<bool> PersistAutomaticHandoffInTransactionAsync(
        Guid tenantId,
        Message inboundMessage,
        Conversation conversation,
        string reason,
        string? handoffText,
        string idempotencyPrefix,
        IMessageRepository messageRepository,
        IConversationRepository conversationRepository,
        IOutboxMessageRepository outboxRepository,
        IHandoffEventRepository handoffEventRepository,
        CancellationToken cancellationToken,
        Guid? queueId = null)
    {
        var registered = await RegisterAutomaticHandoffAsync(
            tenantId, conversation, reason, conversationRepository, handoffEventRepository, cancellationToken);

        if (registered && queueId is Guid selectedQueueId && conversation.QueueId is null)
        {
            conversation.AssignQueue(selectedQueueId);
            await conversationRepository.UpdateAsync(conversation, cancellationToken);
        }

        if (registered && !string.IsNullOrWhiteSpace(handoffText))
        {
            var handoffMessage = Message.CreateOutbound(
                tenantId,
                conversation.Id,
                inboundMessage.ContactId,
                MessageType.Text,
                handoffText,
                AiReplyDeliveryGuard.CreateAutomatedIdempotencyKey(
                    idempotencyPrefix, inboundMessage.Id, conversation.Version));
            await messageRepository.AddAsync(handoffMessage, cancellationToken);
            await outboxRepository.AddAsync(OutboxMessage.Create(tenantId, handoffMessage.Id));
        }

        inboundMessage.MarkProcessedByAi();
        await messageRepository.UpdateAsync(inboundMessage, cancellationToken);
        return registered;
    }

    private static string? FindFlowReply(string? flowStepsJson, string? content)
    {
        if (string.IsNullOrWhiteSpace(flowStepsJson) || string.IsNullOrWhiteSpace(content)) return null;
        try
        {
            var normalizedContent = content.Trim().ToLowerInvariant();
            foreach (var step in JsonSerializer.Deserialize<JsonElement[]>(flowStepsJson) ?? [])
            {
                if (!step.TryGetProperty("keywords", out var keywords) ||
                    !step.TryGetProperty("response", out var response)) continue;
                var keywordList = keywords.GetString()?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? [];
                var matches = Array.Exists(keywordList,
                    keyword => normalizedContent.Contains(keyword.Trim().ToLowerInvariant()));
                if (matches && !string.IsNullOrWhiteSpace(response.GetString())) return response.GetString();
            }
        }
        catch (JsonException)
        {
        }
        return null;
    }
}

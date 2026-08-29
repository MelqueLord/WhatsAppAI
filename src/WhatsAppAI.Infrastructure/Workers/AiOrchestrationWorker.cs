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
using WhatsAppAI.Domain.Usage;
using WhatsAppAI.Infrastructure.Identity;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.Infrastructure.Workers;

public sealed class AiOrchestrationWorker(
    IServiceProvider serviceProvider,
    ILogger<AiOrchestrationWorker> logger) : BackgroundService
{
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
                await ProcessInboundAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
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

    private async Task ProcessInboundAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var messageRepository = scope.ServiceProvider.GetRequiredService<IMessageRepository>();
        var conversationRepository = scope.ServiceProvider.GetRequiredService<IConversationRepository>();
        var credentialRepository = scope.ServiceProvider.GetRequiredService<IAiProviderCredentialRepository>();
        var secretStore = scope.ServiceProvider.GetRequiredService<ISecretStore>();
        var aiProviderResolver = scope.ServiceProvider.GetRequiredService<IAiProviderResolver>();
        var contextAssembler = scope.ServiceProvider.GetRequiredService<ContextAssembler>();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();
        var handoffEventRepository = scope.ServiceProvider.GetRequiredService<IHandoffEventRepository>();
        var interactionRepository = scope.ServiceProvider.GetRequiredService<IAiInteractionRepository>();
        var usageRepository = scope.ServiceProvider.GetRequiredService<IUsageLedgerRepository>();
        var pricingRepository = scope.ServiceProvider.GetRequiredService<IAiModelPricingRepository>();
        var modelEvaluationRepository = scope.ServiceProvider.GetRequiredService<IModelEvaluationRepository>();
        var auditLogRepository = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var botConfigRepository = scope.ServiceProvider.GetRequiredService<IBotConfigurationRepository>();
        var queueRepository = scope.ServiceProvider.GetRequiredService<IServiceLineRepository>();
        var tagRepository = scope.ServiceProvider.GetRequiredService<IClientTagRepository>();
        var contactTagRepository = scope.ServiceProvider.GetRequiredService<IContactTagRepository>();
        var pendingInbound = await messageRepository.GetUnprocessedInboundAsync(20, cancellationToken);

        foreach (var message in pendingInbound)
        {
            if (!await messageRepository.TryClaimInboundForAiAsync(
                    message.TenantId,
                    message.Id,
                    DateTime.UtcNow.Add(AiClaimLease),
                    cancellationToken))
            {
                continue;
            }

            await ProcessSingleInboundAsync(
                message, dbContext, botConfigRepository, queueRepository, tagRepository, contactTagRepository,
                messageRepository, conversationRepository,
                credentialRepository, secretStore, aiProviderResolver,
                contextAssembler, outboxRepository, interactionRepository,
                usageRepository, pricingRepository, modelEvaluationRepository,
                auditLogRepository, handoffEventRepository, cancellationToken);
        }
    }

    private async Task ProcessSingleInboundAsync(
        Message message,
        AppDbContext dbContext,
        IBotConfigurationRepository botConfigRepository,
        IServiceLineRepository queueRepository,
        IClientTagRepository tagRepository,
        IContactTagRepository contactTagRepository,
        IMessageRepository messageRepository,
        IConversationRepository conversationRepository,
        IAiProviderCredentialRepository credentialRepository,
        ISecretStore secretStore,
        IAiProviderResolver aiProviderResolver,
        ContextAssembler contextAssembler,
        IOutboxMessageRepository outboxRepository,
        IAiInteractionRepository interactionRepository,
        IUsageLedgerRepository usageRepository,
        IAiModelPricingRepository pricingRepository,
        IModelEvaluationRepository modelEvaluationRepository,
        IAuditLogRepository auditLogRepository,
        IHandoffEventRepository handoffEventRepository,
        CancellationToken cancellationToken)
    {
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
                    outboxRepository, handoffEventRepository, cancellationToken);
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
                    var previousInboundCount = await dbContext.Messages
                        .IgnoreQueryFilters()
                        .CountAsync(item => item.ConversationId == message.ConversationId &&
                            item.Direction == MessageDirection.Inbound && item.Id != message.Id, cancellationToken);
                    var replyContent = FindFlowReply(botConfig.FlowStepsJson, message.Content)
                        ?? (previousInboundCount > 0 ? botConfig.ReturningMessage : botConfig.WelcomeMessage)
                        ?? botConfig.FallbackMessage;
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

                    var outboundMsg = Message.CreateOutbound(
                        message.TenantId, message.ConversationId, message.ContactId,
                        MessageType.Text,
                        replyContent,
                        AiReplyDeliveryGuard.CreateAutomatedIdempotencyKey(
                            "simple-auto-reply", message.Id, expectedConversationVersion));
                    await messageRepository.AddAsync(outboundMsg, cancellationToken);

                    var outboxMsg = OutboxMessage.Create(message.TenantId, outboundMsg.Id);
                    await outboxRepository.AddAsync(outboxMsg);
                    message.MarkProcessedByAi();
                    await messageRepository.UpdateAsync(message, cancellationToken);
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
                    outboxRepository, handoffEventRepository, cancellationToken);
                logger.LogWarning("AI not enabled for tenant {TenantId} plan", message.TenantId);
                return;
            }

            var monthlyAiResponsesUsed = await GetMonthlyAiResponsesUsedAsync(
                usageRepository, message.TenantId, cancellationToken);
            if (!AiResponseQuotaPolicy.HasAvailableResponse(
                    tenant.MonthlyAiResponseLimit, monthlyAiResponsesUsed))
            {
                await FinalizeAiResponseQuotaExceededAsync(
                    message, conversation, expectedConversationVersion, botConfig, dbContext, messageRepository,
                    conversationRepository, outboxRepository, auditLogRepository, handoffEventRepository,
                    tenant.MonthlyAiResponseLimit, monthlyAiResponsesUsed, cancellationToken);
                logger.LogWarning(
                    "Monthly AI response quota exhausted for tenant {TenantId}",
                    message.TenantId);
                return;
            }

            var credential = await credentialRepository.GetByTenantAsync(message.TenantId, cancellationToken);
            if (credential is null || !credential.IsActive)
            {
                await FinalizeUnavailableAiAsync(
                    message, conversation, botConfig, messageRepository, conversationRepository,
                    outboxRepository, handoffEventRepository, cancellationToken);
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
                    outboxRepository, handoffEventRepository, cancellationToken);
                logger.LogWarning("API key not available for tenant {TenantId}", message.TenantId);
                return;
            }
            if (!AiModelPolicy.IsAllowed(credential.Provider, credential.ModelId))
            {
                await FinalizeUnavailableAiAsync(
                    message, conversation, botConfig, messageRepository, conversationRepository,
                    outboxRepository, handoffEventRepository, cancellationToken);
                logger.LogWarning("AI model is not allowed for tenant {TenantId}", message.TenantId);
                return;
            }
            if (await modelEvaluationRepository.GetApprovedForModelAsync(
                    message.TenantId, credential.Provider, credential.ModelId, cancellationToken) is null)
            {
                await FinalizeUnavailableAiAsync(
                    message, conversation, botConfig, messageRepository, conversationRepository,
                    outboxRepository, handoffEventRepository, cancellationToken);
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
                await RegisterAutomaticHandoffAsync(
                    message.TenantId, conversation, "data_processing_not_authorized",
                    conversationRepository, handoffEventRepository, cancellationToken);
                message.MarkProcessedByAi();
                await messageRepository.UpdateAsync(message, cancellationToken);
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
                    outboxRepository, handoffEventRepository, cancellationToken);
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
                MaxTokens = Math.Clamp(credential.MaxTokensPerResponse, 80, 300)
            };

            var providerBreaker = _providerBreakers.GetOrAdd(
                $"{message.TenantId:N}:{credential.Provider}",
                _ => new CircuitBreaker());
            if (!providerBreaker.CanExecute())
            {
                await FinalizeUnavailableAiAsync(
                    message, conversation, botConfig, messageRepository, conversationRepository,
                    outboxRepository, handoffEventRepository, cancellationToken);
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
            var pricing = await pricingRepository.GetActiveAsync(
                credential.Provider, credential.ModelId, DateTime.UtcNow, cancellationToken);

            // Apply behavior policy
            var sanitizedResponse = BehaviorPolicy.SanitizeResponse(
                response, botConfig.ConfidenceThreshold);
            var routingResult = QueueRoutingPolicy.Apply(
                sanitizedResponse.Decision,
                routingQueues.Select(queue => new RoutingQueueCandidate(queue.Id, queue.Name)).ToList(),
                conversation.QueueId is not null);
            response = sanitizedResponse with { Decision = routingResult.Decision };
            var categorizedTagIds = TagCategorizationPolicy.ResolveAuthorizedTagIds(
                response.Decision.TagNames,
                routingTags.Select(tag => new RoutingTagCandidate(tag.Id, tag.Name)).ToList());

            // Persist interaction (no prompt/response content)
            var interaction = AiInteraction.Create(
                message.TenantId, message.ConversationId, message.Id,
                credential.ModelId, response.Decision.Action.ToString(),
                response.Decision.HandoffReason, response.Decision.Confidence,
                response.InputTokens, response.OutputTokens, 0, response.RawResponseId);
            await interactionRepository.AddAsync(interaction, cancellationToken);

            // Persist usage ledger with actual provider name
            if (response.InputTokens > 0)
            {
                var usage = UsageLedger.Create(
                    message.TenantId, credential.Provider, "input_tokens",
                    response.RawResponseId ?? message.Id.ToString(),
                    response.InputTokens, "tokens",
                    pricing?.CalculateCostMinorUnits(response.InputTokens, input: true),
                    pricing?.Currency,
                    pricing?.Version);
                await usageRepository.AddAsync(usage, cancellationToken);
            }
            if (response.OutputTokens > 0)
            {
                var usage = UsageLedger.Create(
                    message.TenantId, credential.Provider, "output_tokens",
                    response.RawResponseId ?? message.Id.ToString(),
                    response.OutputTokens, "tokens",
                    pricing?.CalculateCostMinorUnits(response.OutputTokens, input: false),
                    pricing?.Currency,
                    pricing?.Version);
                await usageRepository.AddAsync(usage, cancellationToken);
            }

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
                    message.MarkProcessedByAi();
                    await messageRepository.UpdateAsync(message, cancellationToken);
                    logger.LogInformation("AI reply discarded after conversation {ConversationId} changed", message.ConversationId);
                    return;
                }

                if (string.IsNullOrWhiteSpace(response.Content))
                {
                    if (!await RegisterAutomaticHandoffAsync(
                            message.TenantId, conversation, "empty_ai_reply",
                            conversationRepository, handoffEventRepository, cancellationToken))
                    {
                        message.MarkProcessedByAi();
                        await messageRepository.UpdateAsync(message, cancellationToken);
                        return;
                    }

                    var fallbackMessage = Message.CreateOutbound(
                        message.TenantId, conversation.Id, message.ContactId,
                        MessageType.Text, ResolveHandoffMessage(botConfig),
                        AiReplyDeliveryGuard.CreateAutomatedIdempotencyKey(
                            "ai-empty-reply", message.Id, conversation.Version));
                    await messageRepository.AddAsync(fallbackMessage, cancellationToken);
                    await outboxRepository.AddAsync(OutboxMessage.Create(message.TenantId, fallbackMessage.Id));
                    message.MarkProcessedByAi();
                    await messageRepository.UpdateAsync(message, cancellationToken);
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

                monthlyAiResponsesUsed = await GetMonthlyAiResponsesUsedAsync(
                    usageRepository, message.TenantId, cancellationToken);
                await dbContext.Entry(tenant).ReloadAsync(cancellationToken);
                if (!AiResponseQuotaPolicy.HasAvailableResponse(
                        tenant.MonthlyAiResponseLimit, monthlyAiResponsesUsed))
                {
                    await quotaTransaction.RollbackAsync(cancellationToken);
                    await quotaTransaction.DisposeAsync();
                    await FinalizeAiResponseQuotaExceededAsync(
                        message, conversation, expectedConversationVersion, botConfig, dbContext, messageRepository,
                        conversationRepository, outboxRepository, auditLogRepository, handoffEventRepository,
                        tenant.MonthlyAiResponseLimit, monthlyAiResponsesUsed, cancellationToken);
                    logger.LogWarning(
                        "AI reply discarded because tenant {TenantId} reached its monthly quota",
                        message.TenantId);
                    return;
                }

                await dbContext.Entry(conversation).ReloadAsync(cancellationToken);
                if (!AiReplyDeliveryGuard.CanSend(
                        conversation, expectedConversationVersion, DateTime.UtcNow))
                {
                    await quotaTransaction.RollbackAsync(cancellationToken);
                    await quotaTransaction.DisposeAsync();
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

                await messageRepository.AddAsync(replyMessage, cancellationToken);

                var outboxMessage = OutboxMessage.Create(message.TenantId, replyMessage.Id);
                await outboxRepository.AddAsync(outboxMessage);

                var responseUsage = UsageLedger.Create(
                    message.TenantId,
                    credential.Provider,
                    UsageMetricNames.AiResponses,
                    replyMessage.Id.ToString(),
                    1,
                    "responses");
                await usageRepository.AddAsync(responseUsage, cancellationToken);
                await RegisterAiQuotaAuditAsync(
                    dbContext,
                    auditLogRepository,
                    message.TenantId,
                    tenant.MonthlyAiResponseLimit,
                    monthlyAiResponsesUsed + 1,
                    transactionAlreadyHeld: true,
                    cancellationToken);

                conversation.RecordMessage();
                await conversationRepository.UpdateAsync(conversation, cancellationToken);

                message.MarkProcessedByAi();
                await messageRepository.UpdateAsync(message, cancellationToken);

                await quotaTransaction.CommitAsync(cancellationToken);

                logger.LogInformation("AI reply created for message {MessageId}", message.Id);
                return;
            }
            else if (response.Decision.Action == AiAction.Handoff)
            {
                await dbContext.Entry(conversation).ReloadAsync(cancellationToken);
                if (!AiReplyDeliveryGuard.CanSend(
                        conversation, expectedConversationVersion, DateTime.UtcNow))
                {
                    message.MarkProcessedByAi();
                    await messageRepository.UpdateAsync(message, cancellationToken);
                    logger.LogInformation("AI handoff message discarded after conversation {ConversationId} changed or window closed", message.ConversationId);
                    return;
                }

                if (!await RegisterAutomaticHandoffAsync(
                        message.TenantId, conversation, response.Decision.HandoffReason ?? "handoff",
                        conversationRepository, handoffEventRepository, cancellationToken))
                {
                    message.MarkProcessedByAi();
                    await messageRepository.UpdateAsync(message, cancellationToken);
                    return;
                }

                // Send handoff message to client
                var handoffText = ResolveHandoffMessage(botConfig);

                var handoffMsg = Message.CreateOutbound(
                    message.TenantId, conversation.Id, message.ContactId,
                    MessageType.Text,
                    handoffText,
                    AiReplyDeliveryGuard.CreateAutomatedIdempotencyKey(
                        "ai-handoff", message.Id, conversation.Version));
                await messageRepository.AddAsync(handoffMsg, cancellationToken);

                var handoffOutbox = OutboxMessage.Create(message.TenantId, handoffMsg.Id);
                await outboxRepository.AddAsync(handoffOutbox);

                logger.LogInformation("AI handoff for conversation {ConversationId}: {Reason}",
                    conversation.Id, response.Decision.HandoffReason);
            }

            message.MarkProcessedByAi();
            await messageRepository.UpdateAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing inbound message {MessageId} for AI", message.Id);
            var errorText = ex.ToString();

            if (errorText.Contains("429", StringComparison.Ordinal) ||
                errorText.Contains("quota", StringComparison.OrdinalIgnoreCase))
            {
                var botConfig = await botConfigRepository.GetByTenantAsync(message.TenantId, cancellationToken);
                var currentConversation = await conversationRepository.GetByIdAsync(message.ConversationId, cancellationToken);
                if (currentConversation is not null && currentConversation.Mode == ConversationMode.Automatic && currentConversation.IsWindowOpen(DateTime.UtcNow))
                {
                    if (!await RegisterAutomaticHandoffAsync(
                            message.TenantId, currentConversation, "ai_quota_exhausted",
                            conversationRepository, handoffEventRepository, cancellationToken))
                    {
                        message.MarkProcessedByAi();
                        await messageRepository.UpdateAsync(message, cancellationToken);
                        return;
                    }

                    var unavailableMessage = Message.CreateOutbound(
                        message.TenantId, message.ConversationId, message.ContactId,
                        MessageType.Text, ResolveHandoffMessage(botConfig),
                        AiReplyDeliveryGuard.CreateAutomatedIdempotencyKey(
                            "ai-quota", message.Id, currentConversation.Version));
                    await messageRepository.AddAsync(unavailableMessage, cancellationToken);
                    await outboxRepository.AddAsync(OutboxMessage.Create(message.TenantId, unavailableMessage.Id));
                }
                message.MarkProcessedByAi();
                await messageRepository.UpdateAsync(message, cancellationToken);
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
                if (!await RegisterAutomaticHandoffAsync(
                        message.TenantId, failedConversation, "ai_retry_exhausted",
                        conversationRepository, handoffEventRepository, cancellationToken))
                {
                    message.MarkProcessedByAi();
                    await messageRepository.UpdateAsync(message, cancellationToken);
                    return;
                }

                    var handoffMsg = Message.CreateOutbound(
                        message.TenantId, message.ConversationId, message.ContactId,
                        MessageType.Text, ResolveHandoffMessage(botConfig),
                        AiReplyDeliveryGuard.CreateAutomatedIdempotencyKey(
                            "ai-retry-exhausted", message.Id, failedConversation.Version));
                await messageRepository.AddAsync(handoffMsg, cancellationToken);
                await outboxRepository.AddAsync(OutboxMessage.Create(message.TenantId, handoffMsg.Id));
            }

            message.MarkProcessedByAi();
            await messageRepository.UpdateAsync(message, cancellationToken);
            logger.LogWarning("AI retries exhausted; conversation {ConversationId} transferred to human", message.ConversationId);
        }
    }

    private static async Task FinalizeUnavailableAiAsync(
        Message message,
        Conversation conversation,
        BotConfiguration? botConfig,
        IMessageRepository messageRepository,
        IConversationRepository conversationRepository,
        IOutboxMessageRepository outboxRepository,
        IHandoffEventRepository handoffEventRepository,
        CancellationToken cancellationToken)
    {
        if (!conversation.IsWindowOpen(DateTime.UtcNow))
        {
            message.MarkProcessedByAi();
            await messageRepository.UpdateAsync(message, cancellationToken);
            return;
        }

        if (!await RegisterAutomaticHandoffAsync(
                message.TenantId, conversation, "ai_unavailable",
                conversationRepository, handoffEventRepository, cancellationToken))
        {
            message.MarkProcessedByAi();
            await messageRepository.UpdateAsync(message, cancellationToken);
            return;
        }
        var fallbackMessage = ApplyUnavailableAiFallback(message, botConfig, conversation.Version);
        await messageRepository.UpdateAsync(message, cancellationToken);
        await messageRepository.AddAsync(fallbackMessage, cancellationToken);
        await outboxRepository.AddAsync(OutboxMessage.Create(message.TenantId, fallbackMessage.Id));
    }

    private static async Task<long> GetMonthlyAiResponsesUsedAsync(
        IUsageLedgerRepository usageRepository,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return await usageRepository.GetTotalQuantityAsync(
            tenantId,
            UsageMetricNames.AiResponses,
            monthStart,
            monthStart.AddMonths(1),
            cancellationToken);
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

        await RegisterAiQuotaAuditAsync(
            dbContext,
            auditLogRepository,
            message.TenantId,
            monthlyLimit,
            monthlyResponsesUsed,
            transactionAlreadyHeld: false,
            cancellationToken);

        if (!await RegisterAutomaticHandoffAsync(
                message.TenantId,
                conversation,
                "ai_quota_exhausted",
                conversationRepository,
                handoffEventRepository,
                cancellationToken))
        {
            message.MarkProcessedByAi();
            await messageRepository.UpdateAsync(message, cancellationToken);
            return;
        }

        var fallbackMessage = Message.CreateOutbound(
            message.TenantId,
            message.ConversationId,
            message.ContactId,
            MessageType.Text,
            ResolveHandoffMessage(botConfig),
            AiReplyDeliveryGuard.CreateAutomatedIdempotencyKey(
                "ai-quota", message.Id, conversation.Version));
        await messageRepository.AddAsync(fallbackMessage, cancellationToken);
        await outboxRepository.AddAsync(
            OutboxMessage.Create(message.TenantId, fallbackMessage.Id));

        message.MarkProcessedByAi();
        await messageRepository.UpdateAsync(message, cancellationToken);
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

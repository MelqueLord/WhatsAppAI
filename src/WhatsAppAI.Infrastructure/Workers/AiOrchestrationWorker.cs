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
    ILogger<AiOrchestrationWorker> logger,
    IConfiguration configuration) : BackgroundService
{
    private const int MaxAiAttempts = 3;
    private readonly long _monthlyAiTokenBudget = configuration.GetValue("Ai:MonthlyTokenBudget", 100_000L);

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

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var botConfigRepository = scope.ServiceProvider.GetRequiredService<IBotConfigurationRepository>();
        var queueRepository = scope.ServiceProvider.GetRequiredService<IServiceLineRepository>();
        var tagRepository = scope.ServiceProvider.GetRequiredService<IClientTagRepository>();
        var contactTagRepository = scope.ServiceProvider.GetRequiredService<IContactTagRepository>();
        var pendingInbound = await messageRepository.GetUnprocessedInboundAsync(20, cancellationToken);

        foreach (var message in pendingInbound)
        {
            await ProcessSingleInboundAsync(
                message, dbContext, botConfigRepository, queueRepository, tagRepository, contactTagRepository,
                messageRepository, conversationRepository,
                credentialRepository, secretStore, aiProviderResolver,
                contextAssembler, outboxRepository, interactionRepository,
                usageRepository, handoffEventRepository, cancellationToken);
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
                return;

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
                        MessageType.Text, replyContent, $"simple-auto-reply:{message.Id}");
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

            var configuredQueueIds = credential.GetRoutingQueueIds().ToHashSet();
            List<ServiceLine> routingQueues = configuredQueueIds.Count == 0
                ? []
                : (await queueRepository.GetActiveByTenantAsync(message.TenantId, cancellationToken))
                    .Where(queue => configuredQueueIds.Contains(queue.Id))
                    .ToList();

            var configuredTagIds = credential.GetRoutingTagIds().ToHashSet();
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

            var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var tokensUsed = await dbContext.UsageLedger
                .IgnoreQueryFilters()
                .Where(usage => usage.TenantId == message.TenantId &&
                    usage.RecordedAt >= monthStart &&
                    (usage.Metric == "input_tokens" || usage.Metric == "output_tokens"))
                .Select(usage => (long?)usage.Quantity)
                .SumAsync(cancellationToken) ?? 0;
            var estimatedTokens = (long)Math.Ceiling((context.SystemPrompt.Length +
                context.Messages.Sum(item => item.Content.Length)) / 4d) +
                Math.Clamp(credential.MaxTokensPerResponse, 80, 300);
            if (!AiBudgetPolicy.HasAvailableBudget(_monthlyAiTokenBudget, tokensUsed, estimatedTokens))
            {
                await RegisterAutomaticHandoffAsync(
                    message.TenantId, conversation, "ai_budget_exhausted",
                    conversationRepository, handoffEventRepository, cancellationToken);
                message.MarkProcessedByAi();
                await messageRepository.UpdateAsync(message, cancellationToken);
                logger.LogWarning("AI token budget exhausted for tenant {TenantId}", message.TenantId);
                return;
            }

            var request = new AiRequest
            {
                ModelId = credential.ModelId,
                ApiKey = apiKey,
                Messages = context.Messages,
                SystemPrompt = context.SystemPrompt,
                MaxTokens = Math.Clamp(credential.MaxTokensPerResponse, 80, 300)
            };

            var response = await aiProvider.GetResponseAsync(request, cancellationToken);

            // Apply behavior policy
            var sanitizedDecision = BehaviorPolicy.SanitizeDecision(
                response.Decision, botConfig.ConfidenceThreshold);
            var routingResult = QueueRoutingPolicy.Apply(
                sanitizedDecision,
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
            await interactionRepository.AddAsync(interaction, cancellationToken);

            // Persist usage ledger with actual provider name
            if (response.InputTokens > 0)
            {
                var usage = UsageLedger.Create(
                    message.TenantId, credential.Provider, "input_tokens",
                    response.RawResponseId ?? message.Id.ToString(),
                    response.InputTokens, "tokens");
                await usageRepository.AddAsync(usage, cancellationToken);
            }
            if (response.OutputTokens > 0)
            {
                var usage = UsageLedger.Create(
                    message.TenantId, credential.Provider, "output_tokens",
                    response.RawResponseId ?? message.Id.ToString(),
                    response.OutputTokens, "tokens");
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
                    MessageType.Text, queueTransferText, Guid.NewGuid().ToString());
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
                    await RegisterAutomaticHandoffAsync(
                        message.TenantId, conversation, "empty_ai_reply",
                        conversationRepository, handoffEventRepository, cancellationToken);

                    var fallbackMessage = Message.CreateOutbound(
                        message.TenantId, conversation.Id, message.ContactId,
                        MessageType.Text, ResolveHandoffMessage(botConfig),
                        $"ai-empty-reply:{message.Id}");
                    await messageRepository.AddAsync(fallbackMessage, cancellationToken);
                    await outboxRepository.AddAsync(OutboxMessage.Create(message.TenantId, fallbackMessage.Id));
                    message.MarkProcessedByAi();
                    await messageRepository.UpdateAsync(message, cancellationToken);
                    logger.LogWarning("AI returned an empty reply; conversation {ConversationId} transferred to human", message.ConversationId);
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

                conversation.RecordMessage();
                await conversationRepository.UpdateAsync(conversation, cancellationToken);

                logger.LogInformation("AI reply created for message {MessageId}", message.Id);
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

                await RegisterAutomaticHandoffAsync(
                    message.TenantId, conversation, response.Decision.HandoffReason ?? "handoff",
                    conversationRepository, handoffEventRepository, cancellationToken);

                // Send handoff message to client
                var handoffText = ResolveHandoffMessage(botConfig);

                var handoffMsg = Message.CreateOutbound(
                    message.TenantId, conversation.Id, message.ContactId,
                    MessageType.Text, handoffText, Guid.NewGuid().ToString());
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
                    await RegisterAutomaticHandoffAsync(
                        message.TenantId, currentConversation, "ai_quota_exhausted",
                        conversationRepository, handoffEventRepository, cancellationToken);

                    var unavailableMessage = Message.CreateOutbound(
                        message.TenantId, message.ConversationId, message.ContactId,
                        MessageType.Text, ResolveHandoffMessage(botConfig),
                        $"ai-quota:{message.Id}");
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
                await RegisterAutomaticHandoffAsync(
                    message.TenantId, failedConversation, "ai_retry_exhausted",
                    conversationRepository, handoffEventRepository, cancellationToken);

                var handoffMsg = Message.CreateOutbound(
                    message.TenantId, message.ConversationId, message.ContactId,
                    MessageType.Text, ResolveHandoffMessage(botConfig),
                    $"ai-retry-exhausted:{message.Id}");
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

        var fallbackMessage = ApplyUnavailableAiFallback(message, botConfig);
        await messageRepository.UpdateAsync(message, cancellationToken);
        await RegisterAutomaticHandoffAsync(
            message.TenantId, conversation, "ai_unavailable",
            conversationRepository, handoffEventRepository, cancellationToken);
        await messageRepository.AddAsync(fallbackMessage, cancellationToken);
        await outboxRepository.AddAsync(OutboxMessage.Create(message.TenantId, fallbackMessage.Id));
    }

    internal static Message ApplyUnavailableAiFallback(
        Message message,
        BotConfiguration? botConfig)
    {
        message.MarkProcessedByAi();

        return Message.CreateOutbound(
            message.TenantId,
            message.ConversationId,
            message.ContactId,
            MessageType.Text,
            ResolveHandoffMessage(botConfig),
            $"ai-unavailable:{message.Id}");
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

    internal static async Task RegisterAutomaticHandoffAsync(
        Guid tenantId,
        Conversation conversation,
        string reason,
        IConversationRepository conversationRepository,
        IHandoffEventRepository handoffEventRepository,
        CancellationToken cancellationToken)
    {
        if (conversation.Mode == ConversationMode.Human)
            return;

        var previousMode = conversation.SwitchMode(ConversationMode.Human, conversation.Version, null);
        await conversationRepository.UpdateAsync(conversation, cancellationToken);
        await handoffEventRepository.AddAsync(HandoffEvent.Create(
            tenantId, conversation.Id, previousMode, ConversationMode.Human, null, reason));
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

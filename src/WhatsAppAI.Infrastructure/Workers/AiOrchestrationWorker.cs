using Microsoft.EntityFrameworkCore;
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
    ILogger<AiOrchestrationWorker> logger) : BackgroundService
{
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
                usageRepository, cancellationToken);
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
        CancellationToken cancellationToken)
    {
        try
        {
            var conversation = await conversationRepository.GetByIdAsync(message.ConversationId, cancellationToken);
            if (conversation is null)
            {
                logger.LogWarning("Conversation {ConversationId} not found for message {MessageId}",
                    message.ConversationId, message.Id);
                return;
            }

            if (conversation.Mode != ConversationMode.Automatic)
                return;

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
            if (botConfig is null || !botConfig.Enabled)
            {
                logger.LogInformation("Bot not configured or disabled for tenant {TenantId}, skipping", message.TenantId);
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

                    message.MarkProcessedByAi();
                    await messageRepository.UpdateAsync(message, cancellationToken);

                    var outboundMsg = Message.CreateOutbound(
                        message.TenantId, message.ConversationId, message.ContactId,
                        MessageType.Text, replyContent, Guid.NewGuid().ToString());
                    await messageRepository.AddAsync(outboundMsg, cancellationToken);

                    var outboxMsg = OutboxMessage.Create(message.TenantId, outboundMsg.Id);
                    await outboxRepository.AddAsync(outboxMsg);
                    logger.LogInformation("SimpleAutoReply sent for tenant {TenantId}", message.TenantId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "SimpleAutoReply failed for message {MessageId}", message.Id);
                }
                return;
            }

            // Check if tenant plan has AI enabled
            if (!await dbContext.HasAiEnabledAsync(message.TenantId, cancellationToken))
            {
                logger.LogInformation("AI not enabled for tenant {TenantId} plan, skipping", message.TenantId);
                return;
            }

            var credential = await credentialRepository.GetByTenantAsync(message.TenantId, cancellationToken);
            if (credential is null || !credential.IsActive)
            {
                logger.LogInformation("No AI credential for tenant {TenantId}, skipping", message.TenantId);
                return;
            }

            var apiKey = await secretStore.GetAsync(credential.ApiKeyRef, cancellationToken);
            if (string.IsNullOrEmpty(apiKey))
            {
                logger.LogWarning("API key not available for tenant {TenantId}", message.TenantId);
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
            var sanitizedDecision = BehaviorPolicy.SanitizeDecision(response.Decision);
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

            // Revalidate: mode/version may have changed during AI call
            var freshConversation = await conversationRepository.GetByIdAsync(message.ConversationId, cancellationToken);
            if (freshConversation is null || freshConversation.Mode != ConversationMode.Automatic)
            {
                logger.LogInformation("Conversation {ConversationId} changed during AI call, discarding decision", message.ConversationId);
                message.MarkProcessedByAi();
                await messageRepository.UpdateAsync(message, cancellationToken);
                return;
            }
            conversation = freshConversation;

            // Auto-assign queue if AI categorized and conversation has no queue yet
            if (routingResult.QueueId is Guid routingQueueId && conversation.QueueId is null)
            {
                conversation.AssignQueue(routingQueueId);
                await conversationRepository.UpdateAsync(conversation, cancellationToken);
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
            }

            foreach (var tagId in categorizedTagIds)
            {
                if (await contactTagRepository.ExistsAsync(message.TenantId, message.ContactId, tagId, cancellationToken))
                    continue;

                await contactTagRepository.AddAsync(
                    ContactTag.Create(message.ContactId, tagId, message.TenantId),
                    cancellationToken);
            }

            if (response.Decision.Action == AiAction.Reply && !string.IsNullOrWhiteSpace(response.Content))
            {
                var replyMessage = Message.CreateOutbound(
                    message.TenantId,
                    message.ConversationId,
                    message.ContactId,
                    MessageType.Text,
                    response.Content,
                    $"ai:{message.Id}");

                await messageRepository.AddAsync(replyMessage, cancellationToken);

                var outboxMessage = OutboxMessage.Create(message.TenantId, replyMessage.Id);
                await outboxRepository.AddAsync(outboxMessage);

                conversation.RecordMessage();
                await conversationRepository.UpdateAsync(conversation, cancellationToken);

                logger.LogInformation("AI reply created for message {MessageId}", message.Id);
            }
            else if (response.Decision.Action == AiAction.Handoff)
            {
                conversation.SwitchMode(
                    ConversationMode.Human, conversation.Version, null);
                await conversationRepository.UpdateAsync(conversation, cancellationToken);

                // Send handoff message to client
                var handoffText = !string.IsNullOrWhiteSpace(botConfig.HandoffMessage)
                    ? botConfig.HandoffMessage
                    : "Vou encaminhar voce para um atendente.";

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

            if (ex.Message.Contains("429", StringComparison.Ordinal) ||
                ex.Message.Contains("quota", StringComparison.OrdinalIgnoreCase))
            {
                var currentConversation = await conversationRepository.GetByIdAsync(message.ConversationId, cancellationToken);
                if (currentConversation is not null && currentConversation.Mode == ConversationMode.Automatic)
                {
                    currentConversation.SwitchMode(ConversationMode.Human, currentConversation.Version, null);
                    await conversationRepository.UpdateAsync(currentConversation, cancellationToken);

                    var unavailableMessage = Message.CreateOutbound(
                        message.TenantId, message.ConversationId, message.ContactId,
                        MessageType.Text, "No momento nosso atendimento automático está indisponível. Um atendente continuará seu atendimento em breve.",
                        $"ai-quota:{message.Id}");
                    await messageRepository.AddAsync(unavailableMessage, cancellationToken);
                    await outboxRepository.AddAsync(OutboxMessage.Create(message.TenantId, unavailableMessage.Id));
                }
                message.MarkProcessedByAi();
                await messageRepository.UpdateAsync(message, cancellationToken);
                logger.LogWarning("AI quota exhausted; conversation {ConversationId} transferred to human", message.ConversationId);
            }
        }
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

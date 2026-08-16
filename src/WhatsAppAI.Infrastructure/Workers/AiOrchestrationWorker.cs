using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Automation;
using WhatsAppAI.Application.Automation.Context;
using WhatsAppAI.Application.Automation.Policy;
using WhatsAppAI.Domain.Automation;
using WhatsAppAI.Domain.Integrations;
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
        var aiProvider = scope.ServiceProvider.GetRequiredService<IAiProvider>();
        var contextAssembler = scope.ServiceProvider.GetRequiredService<ContextAssembler>();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();
        var interactionRepository = scope.ServiceProvider.GetRequiredService<IAiInteractionRepository>();
        var usageRepository = scope.ServiceProvider.GetRequiredService<IUsageLedgerRepository>();

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var botConfigRepository = scope.ServiceProvider.GetRequiredService<IBotConfigurationRepository>();
        var pendingInbound = await messageRepository.GetUnprocessedInboundAsync(20, cancellationToken);

        foreach (var message in pendingInbound)
        {
            await ProcessSingleInboundAsync(
                message, dbContext, botConfigRepository, messageRepository, conversationRepository,
                credentialRepository, secretStore, aiProvider,
                contextAssembler, outboxRepository, interactionRepository,
                usageRepository, cancellationToken);
        }
    }

    private async Task ProcessSingleInboundAsync(
        Message message,
        AppDbContext dbContext,
        IBotConfigurationRepository botConfigRepository,
        IMessageRepository messageRepository,
        IConversationRepository conversationRepository,
        IAiProviderCredentialRepository credentialRepository,
        ISecretStore secretStore,
        IAiProvider aiProvider,
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

            // Check BotConfiguration mode
            var botConfig = await botConfigRepository.GetByTenantAsync(message.TenantId, cancellationToken);
            if (botConfig is null || !botConfig.Enabled)
            {
                logger.LogInformation("Bot not configured or disabled for tenant {TenantId}, skipping", message.TenantId);
                return;
            }

            if (botConfig.Mode == BotMode.Manual)
            {
                logger.LogInformation("Bot in Manual mode for tenant {TenantId}, skipping", message.TenantId);
                return;
            }

            // Handle SimpleAutoReply mode
            if (botConfig.Mode == BotMode.SimpleAutoReply)
            {
                var replyContent = !string.IsNullOrWhiteSpace(botConfig.FallbackMessage)
                    ? botConfig.FallbackMessage
                    : botConfig.WelcomeMessage;

                if (!string.IsNullOrWhiteSpace(replyContent))
                {
                    var outboundMsg = Message.CreateOutbound(
                        message.TenantId, message.ConversationId, message.ContactId,
                        MessageType.Text, replyContent, Guid.NewGuid().ToString());
                    await messageRepository.AddAsync(outboundMsg, cancellationToken);

                    var outboxMsg = OutboxMessage.Create(message.TenantId, outboundMsg.Id);
                    await outboxRepository.AddAsync(outboxMsg);
                    logger.LogInformation("SimpleAutoReply sent for tenant {TenantId}", message.TenantId);
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

            var context = await contextAssembler.BuildAsync(
                message.TenantId, message.ConversationId, null, cancellationToken);

            var request = new AiRequest
            {
                ModelId = credential.ModelId,
                ApiKey = apiKey,
                Messages = context.Messages,
                SystemPrompt = context.SystemPrompt
            };

            var response = await aiProvider.GetResponseAsync(request, cancellationToken);

            // Apply behavior policy
            var sanitizedDecision = BehaviorPolicy.SanitizeDecision(response.Decision);
            response = response with { Decision = sanitizedDecision };

            // Persist interaction (no prompt/response content)
            var interaction = AiInteraction.Create(
                message.TenantId, message.ConversationId, message.Id,
                credential.ModelId, response.Decision.Action.ToString(),
                response.Decision.HandoffReason, response.Decision.Confidence,
                response.InputTokens, response.OutputTokens, 0, response.RawResponseId);
            await interactionRepository.AddAsync(interaction, cancellationToken);

            // Persist usage ledger
            if (response.InputTokens > 0)
            {
                var usage = UsageLedger.Create(
                    message.TenantId, "openai", "input_tokens",
                    response.RawResponseId ?? message.Id.ToString(),
                    response.InputTokens, "tokens");
                await usageRepository.AddAsync(usage, cancellationToken);
            }
            if (response.OutputTokens > 0)
            {
                var usage = UsageLedger.Create(
                    message.TenantId, "openai", "output_tokens",
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

                logger.LogInformation("AI handoff for conversation {ConversationId}: {Reason}",
                    conversation.Id, response.Decision.HandoffReason);
            }

            message.MarkProcessedByAi();
            await messageRepository.UpdateAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing inbound message {MessageId} for AI", message.Id);
        }
    }
}

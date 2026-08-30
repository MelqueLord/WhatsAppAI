using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Integrations;
using WhatsAppAI.Domain.Integrations;
using WhatsAppAI.Domain.Identity;
using WhatsAppAI.Domain.Messaging;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.Infrastructure.Workers;

public sealed class OutboxProcessingWorker(
    IServiceProvider serviceProvider,
    ILogger<OutboxProcessingWorker> logger) : BackgroundService
{
    private const int MaxConcurrency = 4;
    private const int MaxRetries = 5;
    private const int BatchSize = 20;

    private static readonly TimeSpan[] BackoffDelays =
    [
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15)
    ];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Outbox Processing Worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var hadWork = await ProcessOutboxAsync(stoppingToken);
                await Task.Delay(
                    hadWork ? TimeSpan.FromMilliseconds(100) : TimeSpan.FromSeconds(3),
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in outbox processing worker");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        logger.LogInformation("Outbox Processing Worker stopped");
    }

    private async Task<bool> ProcessOutboxAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();
        var pending = await outboxRepository.GetPendingAsync(BatchSize);
        if (pending.Count == 0)
            return false;

        await Parallel.ForEachAsync(
            pending,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = MaxConcurrency,
                CancellationToken = cancellationToken
            },
            async (outboxMessage, ct) =>
            {
                using var itemScope = serviceProvider.CreateScope();
                var itemOutboxRepository = itemScope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();
                if (!await itemOutboxRepository.TryClaimAsync(outboxMessage.Id, DateTime.UtcNow, ct))
                    return;

                await ProcessSingleAsync(outboxMessage, itemScope.ServiceProvider, ct);
            });

        return true;
    }

    private async Task ProcessSingleAsync(
        OutboxMessage outboxMessage,
        IServiceProvider scopedServices,
        CancellationToken cancellationToken)
    {
        var outboxRepository = scopedServices.GetRequiredService<IOutboxMessageRepository>();
        var messageRepository = scopedServices.GetRequiredService<IMessageRepository>();
        var conversationRepository = scopedServices.GetRequiredService<IConversationRepository>();
        var contactRepository = scopedServices.GetRequiredService<IContactRepository>();
        var dbContext = scopedServices.GetRequiredService<AppDbContext>();
        var whatsAppAccountRepository = scopedServices.GetRequiredService<IWhatsAppAccountRepository>();
        var whatsAppClient = scopedServices.GetRequiredService<IWhatsAppClient>();
        var secretStore = scopedServices.GetRequiredService<ISecretStore>();

        try
        {
            outboxMessage.MarkProcessing();
            await outboxRepository.UpdateAsync(outboxMessage);

            var message = await messageRepository.GetByIdAsync(outboxMessage.MessageId, cancellationToken);
            if (message is null)
            {
                outboxMessage.MarkDead("Message not found");
                await outboxRepository.UpdateAsync(outboxMessage);
                logger.LogWarning("Outbox {OutboxId} references missing message {MessageId}", outboxMessage.Id, outboxMessage.MessageId);
                return;
            }

            var tenant = await dbContext.Tenants.FindAsync([outboxMessage.TenantId], cancellationToken);
            if (tenant?.Status != TenantStatus.Active)
            {
                message.MarkFailed("Tenant suspended");
                await messageRepository.UpdateAsync(message, cancellationToken);
                outboxMessage.MarkDead("Tenant suspended");
                await outboxRepository.UpdateAsync(outboxMessage);
                logger.LogInformation("Outbox {OutboxId} blocked for suspended tenant {TenantId}", outboxMessage.Id, outboxMessage.TenantId);
                return;
            }

            var conversation = await conversationRepository.GetByIdAsync(message.ConversationId, cancellationToken);
            var contact = await contactRepository.GetByIdAsync(message.ContactId, cancellationToken);
            if (contact is null)
            {
                outboxMessage.MarkDead("Contact not found");
                await outboxRepository.UpdateAsync(outboxMessage);
                logger.LogWarning("Outbox {OutboxId} references missing contact {ContactId}", outboxMessage.Id, message.ContactId);
                return;
            }

            var account = conversation is null
                ? await whatsAppAccountRepository.GetByTenantAsync(outboxMessage.TenantId, cancellationToken)
                : await whatsAppAccountRepository.GetByPhoneNumberIdAsync(conversation.PhoneNumberId, cancellationToken);
            if (account is null && conversation?.PhoneNumberId == "manual")
                account = await whatsAppAccountRepository.GetByTenantAndSlotAsync(
                    outboxMessage.TenantId,
                    WhatsAppConnectionType.QrCode,
                    1,
                    cancellationToken);
            var qrPhoneNumberId = conversation?.PhoneNumberId;
            var isQrSession = qrPhoneNumberId?.StartsWith("qr:", StringComparison.OrdinalIgnoreCase) == true ||
                account?.ConnectionType == WhatsAppConnectionType.QrCode;
            if (account is null && !isQrSession)
            {
                outboxMessage.MarkDead("WhatsApp account not found or inactive");
                await outboxRepository.UpdateAsync(outboxMessage);
                logger.LogWarning("No active WhatsApp account for tenant {TenantId}", outboxMessage.TenantId);
                return;
            }

            if (account is not null && !account.IsActive)
            {
                outboxMessage.MarkDead("WhatsApp account not found or inactive");
                await outboxRepository.UpdateAsync(outboxMessage);
                return;
            }

            var outboundPhoneNumberId = account?.PhoneNumberId ?? qrPhoneNumberId;
            if (string.IsNullOrWhiteSpace(outboundPhoneNumberId))
            {
                outboxMessage.MarkDead("WhatsApp phone number not found");
                await outboxRepository.UpdateAsync(outboxMessage);
                return;
            }

            string? token;
            if (isQrSession || account!.ConnectionType == WhatsAppConnectionType.QrCode)
                token = "whatsapp-web";
            else
                token = await secretStore.GetAsync(account!.AccessTokenRef, cancellationToken);
            if (string.IsNullOrEmpty(token))
            {
                outboxMessage.MarkDead("Access token not available");
                await outboxRepository.UpdateAsync(outboxMessage);
                return;
            }

            if (AiReplyDeliveryGuard.IsAutomated(message.IdempotencyKey))
            {
                if (conversation is not null)
                    await dbContext.Entry(conversation).ReloadAsync(cancellationToken);

                if (!AiReplyDeliveryGuard.TryGetExpectedVersion(
                        message.IdempotencyKey, out var expectedVersion) ||
                    (AiReplyDeliveryGuard.IsAiReply(message.IdempotencyKey)
                        ? !AiReplyDeliveryGuard.CanSend(conversation, expectedVersion, DateTime.UtcNow)
                        : !AiReplyDeliveryGuard.CanSendAutomatedNotice(conversation, expectedVersion, DateTime.UtcNow)))
                {
                    message.MarkFailed("Automated reply invalidated by conversation state");
                    await messageRepository.UpdateAsync(message, cancellationToken);
                    outboxMessage.MarkDead("Automated reply invalidated by conversation state");
                    await outboxRepository.UpdateAsync(outboxMessage);
                    logger.LogInformation(
                        "Automated reply {MessageId} discarded because conversation state changed",
                        message.Id);
                    return;
                }
            }

            SendMessageResult result;
            if (message.Type == MessageType.Template)
            {
                if (isQrSession || account?.ConnectionType != WhatsAppConnectionType.OfficialApi ||
                    string.IsNullOrWhiteSpace(message.TemplateName) ||
                    string.IsNullOrWhiteSpace(message.TemplateLanguage))
                {
                    message.MarkFailed("Templates are available only for the official WhatsApp API");
                    await messageRepository.UpdateAsync(message, cancellationToken);
                    outboxMessage.MarkDead("Invalid template channel or configuration");
                    await outboxRepository.UpdateAsync(outboxMessage);
                    return;
                }

                List<string> parameters;
                try
                {
                    parameters = string.IsNullOrWhiteSpace(message.TemplateParametersJson)
                        ? []
                        : JsonSerializer.Deserialize<List<string>>(message.TemplateParametersJson) ?? [];
                }
                catch (JsonException)
                {
                    message.MarkFailed("Invalid template parameters");
                    await messageRepository.UpdateAsync(message, cancellationToken);
                    outboxMessage.MarkDead("Invalid template parameters");
                    await outboxRepository.UpdateAsync(outboxMessage);
                    return;
                }

                if (parameters.Count > 10 || parameters.Exists(parameter =>
                    parameter is null || parameter.Length > 1024))
                {
                    message.MarkFailed("Invalid template parameters");
                    await messageRepository.UpdateAsync(message, cancellationToken);
                    outboxMessage.MarkDead("Invalid template parameters");
                    await outboxRepository.UpdateAsync(outboxMessage);
                    return;
                }

                result = await whatsAppClient.SendTemplateMessageAsync(
                    outboundPhoneNumberId,
                    token,
                    contact.PhoneNumber,
                    message.TemplateName,
                    message.TemplateLanguage,
                    parameters,
                    cancellationToken);
            }
            else
            {
                result = await whatsAppClient.SendTextMessageAsync(
                    outboundPhoneNumberId,
                    token,
                    contact.PhoneNumber,
                    message.Content ?? string.Empty,
                    cancellationToken);
            }

            if (result.IsSuccess)
            {
                message.MarkSent(result.MessageId ?? string.Empty);
                await messageRepository.UpdateAsync(message, cancellationToken);

                outboxMessage.MarkCompleted();
                await outboxRepository.UpdateAsync(outboxMessage);

                logger.LogInformation("Outbox {OutboxId} completed, message {MessageId} sent", outboxMessage.Id, message.Id);
            }
            else
            {
                HandleFailure(outboxMessage, result.ErrorMessage ?? "Send failed");
                await outboxRepository.UpdateAsync(outboxMessage);
                await messageRepository.UpdateAsync(message, cancellationToken);

                logger.LogWarning("Outbox {OutboxId} failed: {Error}", outboxMessage.Id, outboxMessage.LastError);
            }
        }
        catch (Exception ex)
        {
            HandleFailure(outboxMessage, ex.Message);
            await outboxRepository.UpdateAsync(outboxMessage);
            logger.LogError(ex, "Error processing outbox {OutboxId}", outboxMessage.Id);
        }
    }

    private void HandleFailure(OutboxMessage outboxMessage, string error)
    {
        if (outboxMessage.RetryCount >= MaxRetries)
        {
            outboxMessage.MarkDead(error);
            logger.LogError("Outbox {OutboxId} moved to Dead after {Retries} retries: {Error}",
                outboxMessage.Id, outboxMessage.RetryCount, error);
        }
        else
        {
            var delay = BackoffDelays[Math.Min(outboxMessage.RetryCount, BackoffDelays.Length - 1)];
            outboxMessage.MarkFailed(error, delay);
        }
    }
}

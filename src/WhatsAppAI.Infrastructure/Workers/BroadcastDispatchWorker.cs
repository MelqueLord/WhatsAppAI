using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Integrations;
using WhatsAppAI.Domain.Broadcast;
using WhatsAppAI.Domain.Integrations;
using WhatsAppAI.Domain.Messaging;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.Infrastructure.Workers;

/// <summary>
/// Processes pending BroadcastRecipients and sends messages via the QR Code line.
/// Applies a random 1–3 second delay between sends to avoid WhatsApp rate limiting.
/// </summary>
public sealed class BroadcastDispatchWorker(
    IServiceProvider serviceProvider,
    ILogger<BroadcastDispatchWorker> logger) : BackgroundService
{
    private const int BatchSize = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Broadcast Dispatch Worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in broadcast dispatch worker");
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
        }

        logger.LogInformation("Broadcast Dispatch Worker stopped");
    }

    private async Task ProcessBatchAsync(CancellationToken stoppingToken)
    {
        using var scope = serviceProvider.CreateScope();
        var broadcastRepo = scope.ServiceProvider.GetRequiredService<IBroadcastRepository>();
        var contactRepo = scope.ServiceProvider.GetRequiredService<IContactRepository>();
        var conversationRepo = scope.ServiceProvider.GetRequiredService<IConversationRepository>();
        var messageRepo = scope.ServiceProvider.GetRequiredService<IMessageRepository>();
        var whatsAppAccountRepo = scope.ServiceProvider.GetRequiredService<IWhatsAppAccountRepository>();
        var whatsAppClientResolver = scope.ServiceProvider.GetRequiredService<IWhatsAppClientResolver>();
        var secretStore = scope.ServiceProvider.GetRequiredService<ISecretStore>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Find all tenants with active broadcasts
        var activeBroadcasts = await dbContext.BroadcastLists
            .IgnoreQueryFilters()
            .Where(b => b.Status == BroadcastStatus.Sending)
            .ToListAsync(stoppingToken);

        foreach (var broadcast in activeBroadcasts)
        {
            if (stoppingToken.IsCancellationRequested) break;

            var recipients = await broadcastRepo.GetPendingRecipientsAsync(broadcast.Id, BatchSize);
            await ReconcileQueuedRecipientsAsync(broadcast, broadcastRepo, messageRepo, stoppingToken);
            if (recipients.Count == 0) continue;

            foreach (var recipient in recipients)
            {
                if (stoppingToken.IsCancellationRequested) break;

                await ProcessRecipientAsync(
                    broadcast, recipient,
                    broadcastRepo, contactRepo, conversationRepo,
                    messageRepo, whatsAppAccountRepo, whatsAppClientResolver, secretStore,
                    dbContext,
                    stoppingToken);

                // Fixed 2 second delay between sends (avoids CA5394 / rate limiting)
                await Task.Delay(2000, stoppingToken);
            }
        }
    }

    private async Task ProcessRecipientAsync(
        BroadcastList broadcast,
        BroadcastRecipient recipient,
        IBroadcastRepository broadcastRepo,
        IContactRepository contactRepo,
        IConversationRepository conversationRepo,
        IMessageRepository messageRepo,
        IWhatsAppAccountRepository whatsAppAccountRepo,
        IWhatsAppClientResolver whatsAppClientResolver,
        ISecretStore secretStore,
        AppDbContext dbContext,
        CancellationToken ct)
    {
        try
        {
            var contact = await contactRepo.GetByIdAsync(recipient.ContactId, ct);
            if (contact is null)
            {
                await FailRecipientAsync(broadcastRepo, broadcast, recipient, "Contact not found", ct);
                return;
            }

            if (broadcast.DeliveryMode == BroadcastDeliveryMode.OfficialApiTemplate)
            {
                var account = await whatsAppAccountRepo.GetByTenantAndPhoneNumberIdAsync(
                    broadcast.TenantId, broadcast.LinePhoneNumberId, ct);
                if (account is null || !account.IsActive || account.ConnectionType != WhatsAppConnectionType.OfficialApi ||
                    string.IsNullOrWhiteSpace(broadcast.TemplateName) || string.IsNullOrWhiteSpace(broadcast.TemplateLanguage))
                {
                    await FailRecipientAsync(broadcastRepo, broadcast, recipient, "Official API line or template unavailable", ct);
                    return;
                }

                var conversation = await dbContext.Conversations.IgnoreQueryFilters().FirstOrDefaultAsync(
                    item => item.TenantId == broadcast.TenantId && item.ContactId == contact.Id &&
                        item.PhoneNumberId == broadcast.LinePhoneNumberId, ct);
                if (conversation is null)
                {
                    conversation = Conversation.Create(broadcast.TenantId, contact.Id, broadcast.LinePhoneNumberId);
                    conversation.RecordMessage();
                    dbContext.Set<Conversation>().Add(conversation);
                }

                var message = Message.CreateOutboundTemplate(broadcast.TenantId, conversation.Id, contact.Id,
                    broadcast.TemplateName, broadcast.TemplateLanguage, broadcast.TemplateParametersJson ?? "[]",
                    $"broadcast:{broadcast.Id}:recipient:{recipient.Id}:attempt:{recipient.DispatchAttempt}");
                dbContext.Set<Message>().Add(message);
                dbContext.Set<OutboxMessage>().Add(OutboxMessage.Create(broadcast.TenantId, message.Id));
                recipient.MarkQueued(message.Id);
                dbContext.Set<BroadcastRecipient>().Update(recipient);
                await dbContext.SaveChangesAsync(ct);
                return;
            }

            // Find or create conversation
            var conversation = await conversationRepo.GetByContactAndPhoneAsync(
                broadcast.TenantId, contact.Id, broadcast.LinePhoneNumberId, ct);

            if (conversation is null)
            {
                conversation = Conversation.Create(
                    broadcast.TenantId, contact.Id, broadcast.LinePhoneNumberId);
                conversation.RecordMessage();
                await conversationRepo.AddAsync(conversation, ct);
            }
            // A broadcast is an explicit operator action. It is allowed to
            // send regardless of the current automation mode and must not
            // change that mode.

            // Get WhatsApp account for this line
            // Hosted workers have no request tenant context. Always scope this
            // lookup explicitly to the broadcast tenant instead of relying on
            // AppDbContext's global tenant filter.
            var account = await whatsAppAccountRepo.GetByTenantAndPhoneNumberIdAsync(
                broadcast.TenantId,
                broadcast.LinePhoneNumberId,
                ct);

            if (account is null || !account.IsActive
                || account.ConnectionType != WhatsAppConnectionType.QrCode)
            {
                await FailRecipientAsync(broadcastRepo, broadcast, recipient, "QR line not found or inactive", ct);
                return;
            }

            // QR Code always uses "whatsapp-web" as access token
            string? accessToken;
            try { accessToken = await secretStore.GetAsync(account.AccessTokenRef, ct); }
            catch { accessToken = null; }
            accessToken ??= "whatsapp-web";

            // Send message directly (QR has no 24h restriction)
            var result = await whatsAppClientResolver
                .GetClient(WhatsAppConnectionType.QrCode)
                .SendTextMessageAsync(
                account.PhoneNumberId,
                accessToken,
                contact.PhoneNumber,
                broadcast.Message,
                ct);

            if (!result.IsSuccess)
            {
                await FailRecipientAsync(
                    broadcastRepo, broadcast, recipient,
                    result.ErrorMessage ?? "Send failed", ct);

                logger.LogWarning(
                    "Broadcast {BroadcastId} failed to send to {ContactId}: {Error}",
                    broadcast.Id, recipient.ContactId, result.ErrorMessage);
                return;
            }

            // Persist the sent message in the conversation
            var message = Message.CreateOutbound(
                broadcast.TenantId,
                conversation.Id,
                contact.Id,
                MessageType.Text,
                broadcast.Message,
                idempotencyKey: $"broadcast:{broadcast.Id}:{recipient.Id}");

            message.MarkSent(result.MessageId ?? string.Empty);
            await messageRepo.AddAsync(message, ct);

            if (broadcast.QueueId.HasValue && conversation.QueueId is null)
                conversation.AssignQueue(broadcast.QueueId.Value);
            conversation.RecordMessage();
            await conversationRepo.UpdateAsync(conversation, ct);

            recipient.MarkSent();
            await broadcastRepo.UpdateRecipientAsync(recipient);
            broadcast.RecordSent();
            await broadcastRepo.UpdateAsync(broadcast);

            logger.LogInformation(
                "Broadcast {BroadcastId} sent to contact {ContactId}",
                broadcast.Id, recipient.ContactId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Unexpected error processing broadcast recipient {RecipientId}", recipient.Id);
            try
            {
                await FailRecipientAsync(broadcastRepo, broadcast, recipient, ex.Message, ct);
            }
            catch (Exception innerEx)
            {
                logger.LogError(innerEx, "Failed to persist broadcast failure state");
            }
        }
    }

    private static async Task ReconcileQueuedRecipientsAsync(
        BroadcastList broadcast,
        IBroadcastRepository broadcastRepo,
        IMessageRepository messageRepo,
        CancellationToken cancellationToken)
    {
        var queued = await broadcastRepo.GetQueuedRecipientsAsync(broadcast.Id);
        foreach (var recipient in queued)
        {
            if (!recipient.OutboundMessageId.HasValue) continue;
            var message = await messageRepo.GetByIdAsync(recipient.OutboundMessageId.Value, cancellationToken);
            if (message?.Status == MessageStatus.Sent)
            {
                recipient.MarkSent();
                await broadcastRepo.UpdateRecipientAsync(recipient);
                broadcast.RecordSent();
                await broadcastRepo.UpdateAsync(broadcast);
            }
            else if (message?.Status == MessageStatus.Failed)
            {
                recipient.MarkFailed("Template delivery failed");
                await broadcastRepo.UpdateRecipientAsync(recipient);
                broadcast.RecordFailed();
                await broadcastRepo.UpdateAsync(broadcast);
            }
        }
    }

    private static async Task FailRecipientAsync(
        IBroadcastRepository broadcastRepo,
        BroadcastList broadcast,
        BroadcastRecipient recipient,
        string error,
        CancellationToken ct)
    {
        _ = ct; // not used in current repo signatures but kept for future
        recipient.MarkFailed(error);
        await broadcastRepo.UpdateRecipientAsync(recipient);
        broadcast.RecordFailed();
        await broadcastRepo.UpdateAsync(broadcast);
    }
}

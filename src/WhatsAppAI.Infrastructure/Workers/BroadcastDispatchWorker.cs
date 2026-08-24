using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
        var whatsAppClient = scope.ServiceProvider.GetRequiredService<IWhatsAppClient>();
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
            if (recipients.Count == 0) continue;

            foreach (var recipient in recipients)
            {
                if (stoppingToken.IsCancellationRequested) break;

                await ProcessRecipientAsync(
                    broadcast, recipient,
                    broadcastRepo, contactRepo, conversationRepo,
                    messageRepo, whatsAppAccountRepo, whatsAppClient, secretStore,
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
        IWhatsAppClient whatsAppClient,
        ISecretStore secretStore,
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
            else if (conversation.Mode == ConversationMode.Human)
            {
                // BR-BC-004: don't interfere with Human-mode conversations
                await FailRecipientAsync(broadcastRepo, broadcast, recipient, "Conversation is in Human mode", ct);
                return;
            }

            // Get WhatsApp account for this line
            var account = await whatsAppAccountRepo.GetByPhoneNumberIdAsync(
                broadcast.LinePhoneNumberId, ct);

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
            var result = await whatsAppClient.SendTextMessageAsync(
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

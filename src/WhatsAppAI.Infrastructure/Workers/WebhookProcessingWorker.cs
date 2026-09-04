using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Messaging;
using WhatsAppAI.Infrastructure.Meta.Models;
using WhatsAppAI.Infrastructure.Secrets;

namespace WhatsAppAI.Infrastructure.Workers;

public sealed class WebhookProcessingWorker(
    IServiceProvider serviceProvider,
    ILogger<WebhookProcessingWorker> logger) : BackgroundService
{
    private const int MaxConcurrency = 4;
    private static readonly SemaphoreSlim ProcessingLock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Webhook Processing Worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var hadWork = await ProcessEventsAsync(stoppingToken);
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
                logger.LogError(ex, "Error in webhook processing worker");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        logger.LogInformation("Webhook Processing Worker stopped");
    }

    private async Task<bool> ProcessEventsAsync(CancellationToken cancellationToken)
    {
        if (!await ProcessingLock.WaitAsync(TimeSpan.FromSeconds(1), cancellationToken))
        {
            return false;
        }

        try
        {
            using var scope = serviceProvider.CreateScope();
            var webhookEventRepository = scope.ServiceProvider.GetRequiredService<IWebhookEventRepository>();

            // Process pending events
            var pendingEvents = await webhookEventRepository.GetPendingEventsAsync(10, cancellationToken);
            // Process retryable events
            var retryableEvents = await webhookEventRepository.GetRetryableEventsAsync(10, cancellationToken);
            var events = pendingEvents.Concat(retryableEvents).ToArray();
            if (events.Length == 0)
                return false;

            await Parallel.ForEachAsync(
                events.GroupBy(webhookEvent => webhookEvent.PhoneNumberId),
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = MaxConcurrency,
                    CancellationToken = cancellationToken
                },
                async (lineEvents, ct) =>
                {
                    foreach (var webhookEvent in lineEvents)
                        await ProcessSingleEventAsync(webhookEvent, ct);
                });

            return true;
        }
        finally
        {
            ProcessingLock.Release();
        }
    }

    private async Task ProcessSingleEventAsync(
        WebhookEvent webhookEvent,
        CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var webhookEventRepository = scope.ServiceProvider.GetRequiredService<IWebhookEventRepository>();

        try
        {
            webhookEvent.MarkProcessing();
            await webhookEventRepository.UpdateAsync(webhookEvent, cancellationToken);

            // Resolve tenant from phone_number_id
            var whatsAppAccountRepository = scope.ServiceProvider.GetRequiredService<IWhatsAppAccountRepository>();

            var account = await whatsAppAccountRepository.GetByPhoneNumberIdAsync(
                webhookEvent.PhoneNumberId, cancellationToken);

            Guid? tenantId = null;
            if (TryGetWhatsAppWebTenant(webhookEvent.PhoneNumberId, out var webTenantId))
                tenantId = webTenantId;
            else
                tenantId = account?.TenantId;

            if (tenantId is null)
            {
                logger.LogWarning("No WhatsApp account found for {PhoneNumberId}", webhookEvent.PhoneNumberId);
                webhookEvent.MarkFailed("No account found");
                await webhookEventRepository.UpdateAsync(webhookEvent, cancellationToken);
                return;
            }

            // Process the webhook payload
            var success = await ProcessWebhookPayloadAsync(webhookEvent, tenantId.Value, scope.ServiceProvider, cancellationToken);

            if (success)
            {
                webhookEvent.MarkProcessed();
                logger.LogInformation("Webhook event {EventId} processed successfully", webhookEvent.Id);
            }
            else
            {
                webhookEvent.MarkFailed("Processing failed");
                logger.LogWarning("Webhook event {EventId} processing failed", webhookEvent.Id);
            }

            await webhookEventRepository.UpdateAsync(webhookEvent, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing webhook event {EventId}", webhookEvent.Id);
            webhookEvent.MarkFailed(ex.Message);
            await webhookEventRepository.UpdateAsync(webhookEvent, cancellationToken);
        }
    }

    private static bool TryGetWhatsAppWebTenant(string phoneNumberId, out Guid tenantId)
    {
        tenantId = Guid.Empty;
        var parts = phoneNumberId.Split(':', 3);
        return parts.Length == 3 &&
               parts[0].Equals("qr", StringComparison.OrdinalIgnoreCase) &&
               Guid.TryParse(parts[1], out tenantId);
    }

    private async Task<bool> ProcessWebhookPayloadAsync(
        WebhookEvent webhookEvent,
        Guid tenantId,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var contactRepository = serviceProvider.GetRequiredService<IContactRepository>();
        var conversationRepository = serviceProvider.GetRequiredService<IConversationRepository>();
        var messageRepository = serviceProvider.GetRequiredService<IMessageRepository>();
        var encryptionService = serviceProvider.GetRequiredService<IEncryptionService>();
        var notifier = serviceProvider.GetRequiredService<IRealtimeNotifier>();
        var queueRepository = serviceProvider.GetRequiredService<IServiceLineRepository>();
        var tagRepository = serviceProvider.GetRequiredService<IClientTagRepository>();
        var contactTagRepository = serviceProvider.GetRequiredService<IContactTagRepository>();

        try
        {
            // Decrypt and parse the payload
            var decryptedPayload = encryptionService.Decrypt(webhookEvent.EncryptedPayload);
            var payload = JsonSerializer.Deserialize<WebhookPayload>(decryptedPayload, JsonOptions);
            if (payload?.Entry is null || payload.Entry.Count == 0)
            {
                logger.LogWarning("Empty payload for event {EventId}", webhookEvent.Id);
                return true; // Accept empty payloads
            }

            foreach (var entry in payload.Entry)
            {
                if (entry.Changes is null) continue;

                foreach (var change in entry.Changes)
                {
                    if (change.Value is null) continue;

                    // Process messages
                    if (change.Value.Messages is not null)
                    {
                        foreach (var whatsappMessage in change.Value.Messages)
                        {
                            var matchedContact = change.Value.Contacts?.Find(c =>
                                c.WaId is not null &&
                                whatsappMessage.From is not null &&
                                whatsappMessage.From.TrimStart('+').EndsWith(c.WaId.TrimStart('+')))
                                ?? change.Value.Contacts?.Find(_ => true);
                            // Use push_name from the message as fallback when contacts array has no profile name
                            if (matchedContact is not null && matchedContact.Profile?.Name is null && whatsappMessage.PushName is not null)
                                matchedContact = new WebhookContact { WaId = matchedContact.WaId, Profile = new WebhookProfile { Name = whatsappMessage.PushName } };
                            else if (matchedContact is null && whatsappMessage.PushName is not null)
                                matchedContact = new WebhookContact { WaId = whatsappMessage.From, Profile = new WebhookProfile { Name = whatsappMessage.PushName } };
                            await ProcessInboundMessageAsync(
                                tenantId,
                                whatsappMessage,
                                change.Value.Metadata?.PhoneNumberId ?? webhookEvent.PhoneNumberId,
                                matchedContact,
                                contactRepository,
                                conversationRepository,
                                messageRepository,
                                queueRepository,
                                tagRepository,
                                contactTagRepository,
                                notifier,
                                cancellationToken);
                        }
                    }

                    // Process status updates
                    if (change.Value.Statuses is not null)
                    {
                        foreach (var status in change.Value.Statuses)
                        {
                            await ProcessStatusUpdateAsync(
                                status,
                                messageRepository,
                                cancellationToken);
                        }
                    }
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error parsing webhook payload for event {EventId}", webhookEvent.Id);
            return false;
        }
    }

    private async Task ProcessInboundMessageAsync(
        Guid tenantId,
        WebhookMessage whatsappMessage,
        string phoneNumberId,
        WebhookContact? webhookContact,
        IContactRepository contactRepository,
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        IServiceLineRepository queueRepository,
        IClientTagRepository tagRepository,
        IContactTagRepository contactTagRepository,
        IRealtimeNotifier notifier,
        CancellationToken cancellationToken)
    {
        if (whatsappMessage.From is null || whatsappMessage.Id is null)
        {
            logger.LogWarning("Skipping message with missing From or Id");
            return;
        }

        var phoneNumber = NormalizePhoneNumber(whatsappMessage.From);

        // Check for duplicate message
        var existingMessage = await messageRepository.GetByExternalIdAsync(whatsappMessage.Id, cancellationToken);
        if (existingMessage is not null)
        {
            logger.LogInformation("Duplicate message {MessageId}, skipping", whatsappMessage.Id);
            return;
        }

        // Get or create contact
        var contact = await contactRepository.GetByPhoneAsync(tenantId, phoneNumber, cancellationToken);
        if (contact is null)
        {
            contact = Contact.Create(tenantId, phoneNumber, webhookContact?.Profile?.Name);
            await contactRepository.AddAsync(contact, cancellationToken);
            contact = await contactRepository.GetByPhoneAsync(tenantId, phoneNumber, cancellationToken) ?? contact;
        }
        else
        {
            contact.UpdateName(webhookContact?.Profile?.Name);
            contact.RecordMessage();
        }

        // Get or create conversation
        var conversation = await conversationRepository.GetByContactAndPhoneAsync(
            tenantId, contact.Id, phoneNumberId, cancellationToken);

        if (conversation is null)
        {
            conversation = await conversationRepository.GetByContactAndPhoneAsync(
                tenantId, contact.Id, "manual", cancellationToken);
            if (conversation is not null)
            {
                conversation.SetPhoneNumberId(phoneNumberId);
                conversation.RenewWindow();
                conversation.RecordMessage();
            }
            else
            {
                conversation = Conversation.Create(tenantId, contact.Id, phoneNumberId);
                conversation.RenewWindow();
                conversation.RecordMessage();
                await conversationRepository.AddAsync(conversation, cancellationToken);
                conversation = await conversationRepository.GetByContactAndPhoneAsync(
                    tenantId, contact.Id, phoneNumberId, cancellationToken) ?? conversation;
            }
        }
        else
        {
            // Only renew window for inbound customer messages
            conversation.RenewWindow();
            conversation.RecordMessage();
        }

        // Parse message type
        var messageType = ParseMessageType(whatsappMessage.Type);
        var content = whatsappMessage.Text?.Body;
        var mediaId = whatsappMessage.Image?.Id ?? whatsappMessage.Document?.Id ?? whatsappMessage.Audio?.Id;
        var caption = whatsappMessage.Image?.Caption ?? whatsappMessage.Document?.Caption;

        // Create message
        var message = Message.CreateInbound(
            tenantId,
            conversation.Id,
            contact.Id,
            whatsappMessage.Id,
            messageType,
            content,
            mediaId,
            caption);

        await messageRepository.AddAsync(message, cancellationToken);

        logger.LogInformation("Processed inbound message {MessageId} for contact {ContactId}",
            message.Id, contact.Id);

        // Auto-assign queue based on keywords if conversation has no queue yet
        if (conversation.QueueId is null && !string.IsNullOrWhiteSpace(content))
        {
            var queues = await queueRepository.GetActiveByTenantAsync(tenantId, cancellationToken);
            var matchedQueue = queues.FirstOrDefault(q => q.MatchesKeywords(content));
            if (matchedQueue is not null)
            {
                conversation.AssignQueue(matchedQueue.Id);
                await conversationRepository.UpdateAsync(conversation, cancellationToken);
                logger.LogInformation("Conversation {ConversationId} auto-assigned to queue {QueueName} by keyword match",
                    conversation.Id, matchedQueue.Name);

                // Apply tag with same name as the queue (mirrors AI routing behaviour)
                var allTenantTags = await tagRepository.GetActiveByTenantAsync(tenantId, cancellationToken);
                var queueTag = allTenantTags.FirstOrDefault(t =>
                    t.Name.Equals(matchedQueue.Name, StringComparison.OrdinalIgnoreCase));
                if (queueTag is not null &&
                    !await contactTagRepository.ExistsAsync(tenantId, contact.Id, queueTag.Id, cancellationToken))
                {
                    await contactTagRepository.AddAsync(
                        ContactTag.Create(contact.Id, queueTag.Id, tenantId),
                        cancellationToken);
                }
            }
        }

        // Notify frontend via SignalR so the conversation bubbles to the top immediately
        try
        {
            await notifier.NotifyTenantAsync(tenantId, "NewMessage", new { conversationId = conversation.Id }, cancellationToken);
            await notifier.NotifyTenantAsync(tenantId, "ConversationUpdated", new { conversationId = conversation.Id }, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SignalR notification failed for inbound message {MessageId}", message.Id);
        }
    }

    internal static string NormalizePhoneNumber(string phoneNumber)
    {
        var deviceSeparator = phoneNumber.IndexOf(':');
        var phoneIdentity = deviceSeparator >= 0
            ? phoneNumber[..deviceSeparator]
            : phoneNumber;
        var normalized = new string(phoneIdentity.Where(char.IsDigit).ToArray());

        // WhatsApp Web can identify a Brazilian mobile as an eight-digit
        // number followed by its device suffix (for example, :0). The suffix
        // is not part of the phone number; restore the mobile prefix only for
        // that explicit QR identity format.
        return deviceSeparator >= 0 &&
               normalized.Length == 12 &&
               normalized.StartsWith("55", StringComparison.Ordinal)
            ? string.Concat(normalized.AsSpan(0, 4), "9", normalized.AsSpan(4))
            : normalized;
    }

    private async Task ProcessStatusUpdateAsync(
        WebhookStatus status,
        IMessageRepository messageRepository,
        CancellationToken cancellationToken)
    {
        if (status.Id is null) return;

        var message = await messageRepository.GetByExternalIdAsync(status.Id, cancellationToken);
        if (message is null)
        {
            logger.LogWarning("Status update for unknown message {MessageId}", status.Id);
            return;
        }

        switch (status.Status?.ToLowerInvariant())
        {
            case "sent":
                message.MarkSent(status.Id);
                break;
            case "delivered":
                message.MarkDelivered();
                break;
            case "read":
                message.MarkRead();
                break;
            case "failed":
                message.MarkFailed(status.Status ?? "Unknown error");
                break;
        }

        await messageRepository.UpdateAsync(message, cancellationToken);
    }

    private static MessageType ParseMessageType(string? type)
    {
        return type?.ToLowerInvariant() switch
        {
            "text" => MessageType.Text,
            "image" => MessageType.Image,
            "document" => MessageType.Document,
            "audio" => MessageType.Audio,
            "video" => MessageType.Video,
            "sticker" => MessageType.Sticker,
            "location" => MessageType.Location,
            "contacts" => MessageType.Contacts,
            "interactive" => MessageType.Interactive,
            "reaction" => MessageType.Reaction,
            _ => MessageType.Text
        };
    }
}

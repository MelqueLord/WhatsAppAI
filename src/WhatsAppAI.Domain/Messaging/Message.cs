namespace WhatsAppAI.Domain.Messaging;

public sealed class Message
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid ContactId { get; private set; }
    public string? ExternalId { get; private set; }
    public MessageDirection Direction { get; private set; }
    public MessageStatus Status { get; private set; }
    public MessageType Type { get; private set; }
    public string? Content { get; private set; }
    public string? MediaId { get; private set; }
    public string? MediaUrl { get; private set; }
    public string? Caption { get; private set; }
    public string? TemplateName { get; private set; }
    public string? TemplateLanguage { get; private set; }
    public string? TemplateParametersJson { get; private set; }
    public string? QuotedMessageId { get; private set; }
    public Guid? ForwardedFromMessageId { get; private set; }
    public Guid? CorrectsMessageId { get; private set; }
    public DateTime? HiddenFromInboxAt { get; private set; }
    public DateTime? DeletedForEveryoneAt { get; private set; }
    public DateTime? CustomerDeletedAt { get; private set; }
    public DateTime? EditedAt { get; private set; }
    public string? IdempotencyKey { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? SentAt { get; private set; }
    public DateTime? DeliveredAt { get; private set; }
    public DateTime? ReadAt { get; private set; }
    public DateTime? FailedAt { get; private set; }
    public string? FailureReason { get; private set; }
    public bool ProcessedByAi { get; private set; }
    public int AiRetryCount { get; private set; }
    public DateTime? NextAiRetryAt { get; private set; }

    public Conversation Conversation { get; private set; } = null!;
    public Contact Contact { get; private set; } = null!;

    private Message() { }

    public static Message CreateInbound(
        Guid tenantId,
        Guid conversationId,
        Guid contactId,
        string externalId,
        MessageType type,
        string? content,
        string? mediaId = null,
        string? caption = null,
        string? quotedMessageId = null)
    {
        return new Message
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ConversationId = conversationId,
            ContactId = contactId,
            ExternalId = externalId,
            Direction = MessageDirection.Inbound,
            Status = MessageStatus.Received,
            Type = type,
            Content = content,
            MediaId = mediaId,
            Caption = caption,
            QuotedMessageId = ValidateQuotedMessageId(quotedMessageId),
            CreatedAt = DateTime.UtcNow
        };
    }

    public static Message CreateOutbound(
        Guid tenantId,
        Guid conversationId,
        Guid contactId,
        MessageType type,
        string? content,
        string? idempotencyKey,
        string? mediaId = null,
        string? caption = null,
        string? mediaUrl = null,
        string? quotedMessageId = null)
    {
        return new Message
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ConversationId = conversationId,
            ContactId = contactId,
            Direction = MessageDirection.Outbound,
            Status = MessageStatus.Queued,
            Type = type,
            Content = content,
            MediaId = mediaId,
            MediaUrl = mediaUrl,
            Caption = caption,
            QuotedMessageId = ValidateQuotedMessageId(quotedMessageId),
            IdempotencyKey = idempotencyKey,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static Message CreateOutboundTemplate(
        Guid tenantId,
        Guid conversationId,
        Guid contactId,
        string templateName,
        string templateLanguage,
        string? templateParametersJson,
        string? idempotencyKey,
        string? renderedBodyText = null)
    {
        if (string.IsNullOrWhiteSpace(templateName))
            throw new ArgumentException("Template name is required.", nameof(templateName));
        if (string.IsNullOrWhiteSpace(templateLanguage))
            throw new ArgumentException("Template language is required.", nameof(templateLanguage));

        return new Message
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ConversationId = conversationId,
            ContactId = contactId,
            Direction = MessageDirection.Outbound,
            Status = MessageStatus.Queued,
            Type = MessageType.Template,
            Content = string.IsNullOrWhiteSpace(renderedBodyText)
                ? $"Template: {templateName}"
                : renderedBodyText.Trim(),
            TemplateName = templateName.Trim(),
            TemplateLanguage = templateLanguage.Trim(),
            TemplateParametersJson = templateParametersJson,
            IdempotencyKey = idempotencyKey,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkSent(string externalId)
    {
        ExternalId = externalId;
        if (Status is MessageStatus.Delivered or MessageStatus.Read)
            return;

        Status = MessageStatus.Sent;
        SentAt = DateTime.UtcNow;
        FailedAt = null;
        FailureReason = null;
    }

    public void MarkForwardedFrom(Guid sourceMessageId)
    {
        if (Direction != MessageDirection.Outbound || Type != MessageType.Text ||
            sourceMessageId == Guid.Empty)
            throw new InvalidOperationException("Only outbound text messages can be forwarded.");

        ForwardedFromMessageId = sourceMessageId;
    }

    public void MarkCorrectionOf(Guid originalMessageId)
    {
        if (Direction != MessageDirection.Outbound || Type != MessageType.Text ||
            originalMessageId == Guid.Empty || QuotedMessageId is null)
            throw new InvalidOperationException("Corrections must quote a sent text message.");

        CorrectsMessageId = originalMessageId;
    }

    public void HideFromInbox()
    {
        HiddenFromInboxAt ??= DateTime.UtcNow;
    }

    public void MarkDeletedForEveryone()
    {
        if (Direction != MessageDirection.Outbound || string.IsNullOrWhiteSpace(ExternalId))
            throw new InvalidOperationException("Only sent outbound messages can be revoked.");

        DeletedForEveryoneAt ??= DateTime.UtcNow;
        HideFromInbox();
    }

    public void MarkDeletedByCustomer(DateTime deletedAt)
    {
        if (Direction != MessageDirection.Inbound || string.IsNullOrWhiteSpace(ExternalId))
            throw new InvalidOperationException("Only received customer messages can be marked as deleted by the customer.");
        if (deletedAt.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Deletion time must be UTC.", nameof(deletedAt));

        CustomerDeletedAt ??= deletedAt;
    }

    public void ApplyNativeEdit(string content, DateTime editedAt)
    {
        if (Direction != MessageDirection.Outbound || Type != MessageType.Text ||
            Status is not (MessageStatus.Sent or MessageStatus.Delivered or MessageStatus.Read) ||
            string.IsNullOrWhiteSpace(ExternalId) || HiddenFromInboxAt is not null ||
            editedAt < CreatedAt || editedAt - CreatedAt > TimeSpan.FromMinutes(15))
            throw new InvalidOperationException("This message can no longer be edited natively.");

        var value = content?.Trim();
        if (string.IsNullOrWhiteSpace(value) || value.Length > 4000)
            throw new ArgumentException("Edited text must contain 1 to 4000 characters.", nameof(content));

        Content = value;
        EditedAt = editedAt;
    }

    public void MarkQueuedForRetry()
    {
        Status = MessageStatus.Queued;
        FailedAt = null;
        FailureReason = null;
    }

    public void MarkDelivered()
    {
        if (Status == MessageStatus.Read)
            return;

        Status = MessageStatus.Delivered;
        DeliveredAt = DateTime.UtcNow;
    }

    public void MarkRead()
    {
        Status = MessageStatus.Read;
        ReadAt = DateTime.UtcNow;
    }

    public void MarkFailed(string reason)
    {
        if (Status is MessageStatus.Delivered or MessageStatus.Read)
            return;

        Status = MessageStatus.Failed;
        FailureReason = reason;
        FailedAt = DateTime.UtcNow;
    }

    public void MarkProcessedByAi()
    {
        ProcessedByAi = true;
        NextAiRetryAt = null;
    }

    public bool RegisterAiFailure(int maxAttempts, TimeSpan retryDelay)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);

        AiRetryCount++;
        if (AiRetryCount >= maxAttempts)
        {
            NextAiRetryAt = null;
            return false;
        }

        NextAiRetryAt = DateTime.UtcNow.Add(retryDelay);
        return true;
    }

    public void RedactPersonalData()
    {
        ExternalId = null;
        Content = null;
        MediaId = null;
        MediaUrl = null;
        Caption = null;
        TemplateName = null;
        TemplateLanguage = null;
        TemplateParametersJson = null;
        QuotedMessageId = null;
        ForwardedFromMessageId = null;
        CorrectsMessageId = null;
        IdempotencyKey = null;
        FailureReason = null;
    }

    private static string? ValidateQuotedMessageId(string? quotedMessageId)
    {
        if (quotedMessageId is null)
            return null;

        var value = quotedMessageId.Trim();
        if (value.Length is 0 or > 100)
            throw new ArgumentException("Quoted message id must contain 1 to 100 characters.", nameof(quotedMessageId));

        return value;
    }
}

public enum MessageDirection
{
    Inbound = 0,
    Outbound = 1
}

public enum MessageStatus
{
    Queued = 0,
    Sent = 1,
    Delivered = 2,
    Read = 3,
    Failed = 4,
    Received = 5
}

public enum MessageType
{
    Text = 0,
    Image = 1,
    Document = 2,
    Audio = 3,
    Video = 4,
    Sticker = 5,
    Location = 6,
    Contacts = 7,
    Interactive = 8,
    Reaction = 9,
    Template = 10
}

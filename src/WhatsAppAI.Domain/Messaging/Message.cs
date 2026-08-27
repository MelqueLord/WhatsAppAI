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
    public string? QuotedMessageId { get; private set; }
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
        string? caption = null)
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
        string? caption = null)
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
            Caption = caption,
            IdempotencyKey = idempotencyKey,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkSent(string externalId)
    {
        ExternalId = externalId;
        Status = MessageStatus.Sent;
        SentAt = DateTime.UtcNow;
    }

    public void MarkDelivered()
    {
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
        QuotedMessageId = null;
        IdempotencyKey = null;
        FailureReason = null;
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
    Reaction = 9
}

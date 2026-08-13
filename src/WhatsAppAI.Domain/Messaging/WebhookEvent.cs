namespace WhatsAppAI.Domain.Messaging;

public sealed class WebhookEvent
{
    public Guid Id { get; private set; }
    public string PhoneNumberId { get; private set; } = string.Empty;
    public Guid? TenantId { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public WebhookEventStatus Status { get; private set; }
    public string RawPayloadRef { get; private set; } = string.Empty;
    public string EncryptedPayload { get; private set; } = string.Empty;
    public string? Signature { get; private set; }
    public string? ErrorMessage { get; private set; }
    public int RetryCount { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public DateTime? NextRetryAt { get; private set; }

    private WebhookEvent() { }

    public static WebhookEvent Create(
        string phoneNumberId,
        string idempotencyKey,
        string rawPayload,
        string signature,
        Guid? tenantId = null)
    {
        return new WebhookEvent
        {
            Id = Guid.NewGuid(),
            PhoneNumberId = phoneNumberId,
            TenantId = tenantId,
            IdempotencyKey = idempotencyKey,
            Status = WebhookEventStatus.Pending,
            EncryptedPayload = rawPayload, // Will be encrypted by repository
            Signature = signature,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkProcessing()
    {
        Status = WebhookEventStatus.Processing;
    }

    public void MarkProcessed()
    {
        Status = WebhookEventStatus.Processed;
        ProcessedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string error)
    {
        ErrorMessage = error;
        RetryCount++;

        if (RetryCount >= 5)
        {
            Status = WebhookEventStatus.Dead;
        }
        else
        {
            Status = WebhookEventStatus.Failed;
            NextRetryAt = DateTime.UtcNow.AddSeconds(Math.Pow(2, RetryCount) * 10);
        }
    }

    public void MarkDead()
    {
        Status = WebhookEventStatus.Dead;
    }

    public void SetEncryptedPayload(string encryptedPayload)
    {
        EncryptedPayload = encryptedPayload;
    }

    public bool ShouldRetry => Status == WebhookEventStatus.Failed
        && NextRetryAt.HasValue
        && NextRetryAt.Value <= DateTime.UtcNow
        && RetryCount < 5;
}

public enum WebhookEventStatus
{
    Pending = 0,
    Processing = 1,
    Processed = 2,
    Failed = 3,
    Dead = 4,
    Unknown = 5
}

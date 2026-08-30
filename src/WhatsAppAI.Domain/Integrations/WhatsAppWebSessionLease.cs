namespace WhatsAppAI.Domain.Integrations;

public sealed class WhatsAppWebSessionLease
{
    public string SessionId { get; private set; } = string.Empty;
    public Guid TenantId { get; private set; }
    public int LineNumber { get; private set; }
    public string OwnerInstanceId { get; private set; } = string.Empty;
    public string OwnerBaseUrl { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private WhatsAppWebSessionLease() { }

    public static WhatsAppWebSessionLease Create(
        string sessionId,
        Guid tenantId,
        int lineNumber,
        string ownerInstanceId,
        string ownerBaseUrl,
        DateTime expiresAt,
        DateTime utcNow) =>
        new()
        {
            SessionId = sessionId,
            TenantId = tenantId,
            LineNumber = lineNumber,
            OwnerInstanceId = ownerInstanceId,
            OwnerBaseUrl = ownerBaseUrl,
            ExpiresAt = expiresAt,
            UpdatedAt = utcNow
        };

    public bool IsOwnedBy(string instanceId) =>
        string.Equals(OwnerInstanceId, instanceId, StringComparison.Ordinal);

    public bool IsExpired(DateTime utcNow) => ExpiresAt <= utcNow;

    public void Renew(string ownerBaseUrl, DateTime expiresAt, DateTime utcNow)
    {
        OwnerBaseUrl = ownerBaseUrl;
        ExpiresAt = expiresAt;
        UpdatedAt = utcNow;
    }

    public void TransferTo(string ownerInstanceId, string ownerBaseUrl, DateTime expiresAt, DateTime utcNow)
    {
        OwnerInstanceId = ownerInstanceId;
        OwnerBaseUrl = ownerBaseUrl;
        ExpiresAt = expiresAt;
        UpdatedAt = utcNow;
    }
}

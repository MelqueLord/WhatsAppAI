namespace WhatsAppAI.Domain.Integrations;

public sealed class WhatsAppBusinessAccount
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string WabaId { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public uint Version { get; private set; }

    private WhatsAppBusinessAccount() { }

    public static WhatsAppBusinessAccount Create(Guid tenantId, string wabaId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wabaId);
        var normalized = wabaId.Trim();
        if (normalized.Length > 100) throw new ArgumentOutOfRangeException(nameof(wabaId));
        return new WhatsAppBusinessAccount
        {
            Id = Guid.NewGuid(), TenantId = tenantId, WabaId = normalized, CreatedAt = DateTime.UtcNow
        };
    }
}

namespace WhatsAppAI.Domain.Integrations;

public sealed class Secret
{
    public Guid Id { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string EncryptedValue { get; private set; } = string.Empty;
    public Guid? TenantId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private Secret() { }

    public static Secret Create(string key, string encryptedValue, Guid? tenantId = null)
    {
        return new Secret
        {
            Id = Guid.NewGuid(),
            Key = key,
            EncryptedValue = encryptedValue,
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateValue(string encryptedValue)
    {
        EncryptedValue = encryptedValue;
        UpdatedAt = DateTime.UtcNow;
    }
}

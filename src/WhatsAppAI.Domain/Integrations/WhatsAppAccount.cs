namespace WhatsAppAI.Domain.Integrations;

public sealed class WhatsAppAccount
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string WabaId { get; private set; } = string.Empty;
    public string PhoneNumberId { get; private set; } = string.Empty;
    public string AccessTokenRef { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public uint Version { get; private set; }

    private WhatsAppAccount() { }

    public static WhatsAppAccount Create(
        Guid tenantId,
        string wabaId,
        string phoneNumberId,
        string accessTokenRef)
    {
        return new WhatsAppAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WabaId = wabaId,
            PhoneNumberId = phoneNumberId,
            AccessTokenRef = accessTokenRef,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string wabaId, string phoneNumberId, string accessTokenRef)
    {
        WabaId = wabaId;
        PhoneNumberId = phoneNumberId;
        AccessTokenRef = accessTokenRef;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }
}

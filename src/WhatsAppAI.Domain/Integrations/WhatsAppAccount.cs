namespace WhatsAppAI.Domain.Integrations;

public sealed class WhatsAppAccount
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public WhatsAppConnectionType ConnectionType { get; private set; }
    public int LineNumber { get; private set; }
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
        string accessTokenRef,
        WhatsAppConnectionType connectionType = WhatsAppConnectionType.OfficialApi,
        int lineNumber = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(lineNumber, 1);
        return new WhatsAppAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ConnectionType = connectionType,
            LineNumber = lineNumber,
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

public enum WhatsAppConnectionType
{
    OfficialApi = 0,
    QrCode = 1
}

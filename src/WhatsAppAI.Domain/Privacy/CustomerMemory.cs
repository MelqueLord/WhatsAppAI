namespace WhatsAppAI.Domain.Privacy;

public sealed class CustomerMemory
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ContactId { get; private set; }
    public Guid ConsentEvidenceId { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;
    public CustomerMemorySource Source { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public bool IsActive { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private CustomerMemory() { }

    public static CustomerMemory Create(
        Guid tenantId,
        Guid contactId,
        Guid consentEvidenceId,
        string key,
        string value,
        CustomerMemorySource source,
        DateTime expiresAt,
        Guid createdByUserId)
    {
        ValidateIds(tenantId, contactId, consentEvidenceId, createdByUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(expiresAt, DateTime.UtcNow);

        return new CustomerMemory
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            ContactId = contactId,
            ConsentEvidenceId = consentEvidenceId,
            Key = key.Trim(),
            Value = value.Trim(),
            Source = source,
            ExpiresAt = expiresAt,
            IsActive = true,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Replace(
        Guid consentEvidenceId,
        string value,
        CustomerMemorySource source,
        DateTime expiresAt)
    {
        if (consentEvidenceId == Guid.Empty)
            throw new ArgumentException("Consent evidence is required.", nameof(consentEvidenceId));
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(expiresAt, DateTime.UtcNow);

        ConsentEvidenceId = consentEvidenceId;
        Value = value.Trim();
        Source = source;
        ExpiresAt = expiresAt;
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        if (!IsActive)
            return;

        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Redact()
    {
        Key = $"redacted-{Id:N}";
        Value = "[redacted]";
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    private static void ValidateIds(
        Guid tenantId,
        Guid contactId,
        Guid consentEvidenceId,
        Guid createdByUserId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant is required.", nameof(tenantId));
        if (contactId == Guid.Empty)
            throw new ArgumentException("Contact is required.", nameof(contactId));
        if (consentEvidenceId == Guid.Empty)
            throw new ArgumentException("Consent evidence is required.", nameof(consentEvidenceId));
        if (createdByUserId == Guid.Empty)
            throw new ArgumentException("Creator is required.", nameof(createdByUserId));
    }
}

public enum CustomerMemorySource
{
    CustomerConfirmed = 0,
    OperatorConfirmed = 1
}

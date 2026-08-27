namespace WhatsAppAI.Domain.Privacy;

public sealed class ProcessingPurpose
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public LegalBasis LegalBasis { get; private set; }
    public int RetentionDays { get; private set; }
    public bool IsActive { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private ProcessingPurpose() { }

    public static ProcessingPurpose Create(
        Guid tenantId,
        string name,
        string description,
        LegalBasis legalBasis,
        int retentionDays,
        Guid createdByUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        if (retentionDays is < 1 or > 3650)
            throw new ArgumentOutOfRangeException(nameof(retentionDays));

        return new ProcessingPurpose
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            Name = name.Trim(),
            Description = description.Trim(),
            LegalBasis = legalBasis,
            RetentionDays = retentionDays,
            IsActive = true,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow
        };
    }
}

public enum LegalBasis
{
    Consent = 0,
    Contract = 1,
    LegalObligation = 2,
    LegitimateInterest = 3,
    CreditProtection = 4,
    RightsExercise = 5,
    LifeProtection = 6,
    HealthProtection = 7,
    PublicPolicy = 8,
    Research = 9
}

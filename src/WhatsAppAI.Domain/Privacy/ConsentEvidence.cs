namespace WhatsAppAI.Domain.Privacy;

public sealed class ConsentEvidence
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ContactId { get; private set; }
    public Guid ProcessingPurposeId { get; private set; }
    public string Source { get; private set; } = string.Empty;
    public string? EvidenceReference { get; private set; }
    public DateTime GrantedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public Guid RecordedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public ProcessingPurpose ProcessingPurpose { get; private set; } = null!;

    private ConsentEvidence() { }

    public static ConsentEvidence Create(
        Guid tenantId,
        Guid contactId,
        ProcessingPurpose purpose,
        string source,
        string? evidenceReference,
        DateTime grantedAt,
        Guid recordedByUserId)
    {
        if (purpose.TenantId != tenantId || purpose.LegalBasis != LegalBasis.Consent)
            throw new InvalidOperationException("Consent evidence requires a consent purpose from the same tenant.");

        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        return new ConsentEvidence
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            ContactId = contactId,
            ProcessingPurposeId = purpose.Id,
            Source = source.Trim(),
            EvidenceReference = string.IsNullOrWhiteSpace(evidenceReference) ? null : evidenceReference.Trim(),
            GrantedAt = grantedAt,
            RecordedByUserId = recordedByUserId,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Revoke(DateTime revokedAt)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(revokedAt, GrantedAt);

        RevokedAt ??= revokedAt;
    }

    public void RedactReference()
    {
        Source = "redacted";
        EvidenceReference = null;
    }
}

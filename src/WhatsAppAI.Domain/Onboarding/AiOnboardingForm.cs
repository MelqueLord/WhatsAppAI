namespace WhatsAppAI.Domain.Onboarding;

public sealed class AiOnboardingForm
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public AiOnboardingFormStatus Status { get; private set; }
    public string? CompanyName { get; private set; }
    public string? AnswersJson { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? SubmittedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public uint Version { get; private set; }

    private AiOnboardingForm() { }

    public static AiOnboardingForm Create(
        Guid tenantId,
        string tokenHash,
        Guid createdByUserId,
        DateTime expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(expiresAt, DateTime.UtcNow);

        return new AiOnboardingForm
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TokenHash = tokenHash,
            Status = AiOnboardingFormStatus.Pending,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt
        };
    }

    public bool IsUsable =>
        Status == AiOnboardingFormStatus.Pending &&
        RevokedAt is null &&
        ExpiresAt > DateTime.UtcNow;

    public void Submit(string companyName, string answersJson)
    {
        if (!IsUsable)
            throw new InvalidOperationException("The onboarding form is not available.");

        ArgumentException.ThrowIfNullOrWhiteSpace(companyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(answersJson);
        if (companyName.Length > 200)
            throw new ArgumentException("Company name exceeds the allowed length.", nameof(companyName));
        if (answersJson.Length > 50_000)
            throw new ArgumentException("Answers exceed the allowed length.", nameof(answersJson));

        CompanyName = companyName.Trim();
        AnswersJson = answersJson;
        Status = AiOnboardingFormStatus.Submitted;
        SubmittedAt = DateTime.UtcNow;
        Version++;
    }

    public void Revoke()
    {
        if (Status != AiOnboardingFormStatus.Pending)
            return;

        Status = AiOnboardingFormStatus.Revoked;
        RevokedAt = DateTime.UtcNow;
        Version++;
    }
}

public enum AiOnboardingFormStatus
{
    Pending = 0,
    Submitted = 1,
    Revoked = 2
}

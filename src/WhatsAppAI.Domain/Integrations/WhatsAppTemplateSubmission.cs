namespace WhatsAppAI.Domain.Integrations;

public sealed class WhatsAppTemplateSubmission
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid WhatsAppBusinessAccountId { get; private set; }
    public Guid WhatsAppMessageTemplateId { get; private set; }
    public Guid SourceWhatsAppAccountId { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string RequestFingerprint { get; private set; } = string.Empty;
    public WhatsAppTemplateSubmissionStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime? NextAttemptAt { get; private set; }
    public DateTime? ClaimedAt { get; private set; }
    public DateTime? ClaimExpiresAt { get; private set; }
    public string? LastErrorCode { get; private set; }
    public string? LastErrorCategory { get; private set; }
    public string? LastErrorMessage { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
    public DateTime? AcceptedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public uint Version { get; private set; }

    private WhatsAppTemplateSubmission() { }

    public static WhatsAppTemplateSubmission Queue(Guid tenantId, Guid wabaId, Guid templateId, Guid sourceAccountId,
        string idempotencyKey, string requestFingerprint, string correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        if (idempotencyKey.Length > 200) throw new ArgumentOutOfRangeException(nameof(idempotencyKey));
        return new WhatsAppTemplateSubmission
        {
            Id = Guid.NewGuid(), TenantId = tenantId, WhatsAppBusinessAccountId = wabaId,
            WhatsAppMessageTemplateId = templateId, SourceWhatsAppAccountId = sourceAccountId,
            IdempotencyKey = idempotencyKey.Trim(), RequestFingerprint = requestFingerprint,
            CorrelationId = correlationId, Status = WhatsAppTemplateSubmissionStatus.Queued, CreatedAt = DateTime.UtcNow
        };
    }

    public bool TryClaim(DateTime now, TimeSpan lease)
    {
        if (Status is WhatsAppTemplateSubmissionStatus.Accepted or WhatsAppTemplateSubmissionStatus.FailedPermanent or WhatsAppTemplateSubmissionStatus.NeedsAttention) return false;
        if (ClaimExpiresAt > now) return false;
        if (NextAttemptAt > now) return false;
        Status = Status == WhatsAppTemplateSubmissionStatus.OutcomeUnknown ? WhatsAppTemplateSubmissionStatus.Reconciling : WhatsAppTemplateSubmissionStatus.Processing;
        ClaimedAt = now; ClaimExpiresAt = now.Add(lease); AttemptCount++; UpdatedAt = now; Version++;
        return true;
    }

    public void MarkAccepted(DateTime now) { Status = WhatsAppTemplateSubmissionStatus.Accepted; AcceptedAt = now; CompletedAt = now; ClearClaim(now); }
    public void MarkOutcomeUnknown(string? code, string message, DateTime now) { SetError(code, "outcome_unknown", message); Status = WhatsAppTemplateSubmissionStatus.OutcomeUnknown; NextAttemptAt = now.AddSeconds(10); ClearClaim(now); }
    public void ScheduleRetry(string? code, string category, string message, DateTime now) { SetError(code, category, message); Status = WhatsAppTemplateSubmissionStatus.RetryScheduled; NextAttemptAt = now.AddSeconds(Math.Min(300, Math.Pow(2, Math.Min(AttemptCount, 8)))); ClearClaim(now); }
    public void MarkPermanentFailure(string? code, string category, string message, DateTime now) { SetError(code, category, message); Status = WhatsAppTemplateSubmissionStatus.FailedPermanent; CompletedAt = now; ClearClaim(now); }
    public void MarkNeedsAttention(string message, DateTime now) { SetError(null, "conflict", message); Status = WhatsAppTemplateSubmissionStatus.NeedsAttention; CompletedAt = now; ClearClaim(now); }

    private void ClearClaim(DateTime now) { ClaimedAt = null; ClaimExpiresAt = null; UpdatedAt = now; Version++; }
    private void SetError(string? code, string category, string message) { LastErrorCode = code?[..Math.Min(code.Length, 100)]; LastErrorCategory = category[..Math.Min(category.Length, 60)]; LastErrorMessage = message[..Math.Min(message.Length, 500)]; }
}

public enum WhatsAppTemplateSubmissionStatus { Queued, Processing, RetryScheduled, OutcomeUnknown, Reconciling, Accepted, FailedPermanent, NeedsAttention }

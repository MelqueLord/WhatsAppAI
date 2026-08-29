namespace WhatsAppAI.Domain.Automation;

public sealed class ModelEvaluation
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Provider { get; private set; } = "openai";
    public string ModelId { get; private set; } = string.Empty;
    public string EvaluatorUserId { get; private set; } = string.Empty;
    public double QualityScore { get; private set; }
    public double HandoffRate { get; private set; }
    public double SafetyScore { get; private set; }
    public decimal CostPer1kTokens { get; private set; }
    public int P95LatencyMs { get; private set; }
    public bool IsApproved { get; private set; }
    public string? RejectionReason { get; private set; }
    public string? RollbackModelId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private ModelEvaluation() { }

    public static ModelEvaluation Create(
        Guid tenantId,
        string modelId,
        string evaluatorUserId,
        double qualityScore,
        double handoffRate,
        double safetyScore,
        decimal costPer1kTokens,
        int p95LatencyMs,
        string provider = "openai")
    {
        return new ModelEvaluation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Provider = provider.Trim().ToLowerInvariant(),
            ModelId = modelId,
            EvaluatorUserId = evaluatorUserId,
            QualityScore = qualityScore,
            HandoffRate = handoffRate,
            SafetyScore = safetyScore,
            CostPer1kTokens = costPer1kTokens,
            P95LatencyMs = p95LatencyMs,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Approve(string? rollbackModelId = null)
    {
        IsApproved = true;
        RollbackModelId = rollbackModelId;
    }

    public void Reject(string reason)
    {
        IsApproved = false;
        RejectionReason = reason;
    }
}

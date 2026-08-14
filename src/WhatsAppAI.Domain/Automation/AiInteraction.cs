namespace WhatsAppAI.Domain.Automation;

public sealed class AiInteraction
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid MessageId { get; private set; }
    public string ModelId { get; private set; } = string.Empty;
    public string Decision { get; private set; } = string.Empty;
    public string? HandoffReason { get; private set; }
    public double Confidence { get; private set; }
    public int InputTokens { get; private set; }
    public int OutputTokens { get; private set; }
    public int LatencyMs { get; private set; }
    public string? ResponseId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private AiInteraction() { }

    public static AiInteraction Create(
        Guid tenantId,
        Guid conversationId,
        Guid messageId,
        string modelId,
        string decision,
        string? handoffReason,
        double confidence,
        int inputTokens,
        int outputTokens,
        int latencyMs,
        string? responseId)
    {
        return new AiInteraction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ConversationId = conversationId,
            MessageId = messageId,
            ModelId = modelId,
            Decision = decision,
            HandoffReason = handoffReason,
            Confidence = confidence,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            LatencyMs = latencyMs,
            ResponseId = responseId,
            CreatedAt = DateTime.UtcNow
        };
    }
}

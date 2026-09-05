namespace WhatsAppAI.Domain.Automation;

public sealed class AiInteraction
{
    public const int FeedbackNoteMaxLength = 1000;
    public const int CorrectedResponseMaxLength = 160;
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid MessageId { get; private set; }
    public Guid? ResponseMessageId { get; private set; }
    public string ModelId { get; private set; } = string.Empty;
    public string Decision { get; private set; } = string.Empty;
    public string? HandoffReason { get; private set; }
    public double Confidence { get; private set; }
    public int InputTokens { get; private set; }
    public int OutputTokens { get; private set; }
    public int LatencyMs { get; private set; }
    public string? ResponseId { get; private set; }
    public AiFeedbackRating? FeedbackRating { get; private set; }
    public string? FeedbackNote { get; private set; }
    public string? CorrectedResponse { get; private set; }
    public Guid? FeedbackByUserId { get; private set; }
    public DateTime? FeedbackAt { get; private set; }
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

    public void SetResponseMessageId(Guid responseMessageId)
    {
        ResponseMessageId = responseMessageId;
    }

    public void RecordFeedback(
        AiFeedbackRating rating,
        string? note,
        string? correctedResponse,
        Guid operatorUserId)
    {
        if (FeedbackRating.HasValue)
            throw new InvalidOperationException("Feedback has already been recorded for this response.");
        if (note?.Length > FeedbackNoteMaxLength)
            throw new ArgumentOutOfRangeException(nameof(note));
        if (correctedResponse?.Length > CorrectedResponseMaxLength)
            throw new ArgumentOutOfRangeException(nameof(correctedResponse));
        if (rating == AiFeedbackRating.NeedsCorrection &&
            string.IsNullOrWhiteSpace(note) &&
            string.IsNullOrWhiteSpace(correctedResponse))
            throw new ArgumentException("A correction or explanation is required.", nameof(note));

        FeedbackRating = rating;
        FeedbackNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        CorrectedResponse = string.IsNullOrWhiteSpace(correctedResponse) ? null : correctedResponse.Trim();
        FeedbackByUserId = operatorUserId;
        FeedbackAt = DateTime.UtcNow;
    }
}

public enum AiFeedbackRating
{
    Helpful = 0,
    NeedsCorrection = 1
}

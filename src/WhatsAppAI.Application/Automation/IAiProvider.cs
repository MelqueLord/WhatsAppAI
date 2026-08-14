namespace WhatsAppAI.Application.Automation;

public interface IAiProvider
{
    Task<AiResponse> GetResponseAsync(AiRequest request, CancellationToken cancellationToken = default);
}

public sealed record AiRequest
{
    public required string ModelId { get; init; }
    public required string ApiKey { get; init; }
    public required IReadOnlyList<AiMessage> Messages { get; init; }
    public string? SystemPrompt { get; init; }
    public int MaxTokens { get; init; } = 1024;
}

public sealed record AiMessage
{
    public required string Role { get; init; }
    public required string Content { get; init; }
}

public sealed record AiResponse
{
    public required AiDecision Decision { get; init; }
    public string? Content { get; init; }
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
    public string? RawResponseId { get; init; }
}

public sealed record AiDecision
{
    public required AiAction Action { get; init; }
    public string? Text { get; init; }
    public string? HandoffReason { get; init; }
    public double Confidence { get; init; }
}

public enum AiAction
{
    Reply = 0,
    Handoff = 1,
    NoAction = 2
}

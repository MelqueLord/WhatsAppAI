namespace WhatsAppAI.Application.Conversations.Queries;

public sealed record CursorPaginationRequest
{
    public string? Cursor { get; init; }
    public int Limit { get; init; } = 50;
}

public sealed record CursorPaginationResponse<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public string? NextCursor { get; init; }
    public bool HasMore { get; init; }
}

public sealed record ConversationDto
{
    public Guid Id { get; init; }
    public Guid ContactId { get; init; }
    public string ContactName { get; init; } = string.Empty;
    public string ContactPhone { get; init; } = string.Empty;
    public string Mode { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public uint Version { get; init; }
    public string? LastMessage { get; init; }
    public bool LastMessageDeletedByCustomer { get; init; }
    public DateTime? LastMessageAt { get; init; }
    public int UnreadCount { get; init; }
    public Guid? QueueId { get; init; }
    public bool IsQrCode { get; init; }
    public bool IsWindowOpen { get; init; }
    public bool CanUseTemplates { get; init; }
    public IReadOnlyList<ConversationTagDto> Tags { get; init; } = [];
}

public sealed record ConversationTagDto(string Name, string? Color);

public sealed record MessageDto
{
    public Guid Id { get; init; }
    public string Direction { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string? Content { get; init; }
    public string? MediaId { get; init; }
    public string? Caption { get; init; }
    public string? QuotedMessageId { get; init; }
    public MessageQuoteDto? ReplyTo { get; init; }
    public bool CanReply { get; init; }
    public bool IsForwarded { get; init; }
    public bool IsCorrection { get; init; }
    public bool IsDeleted { get; init; }
    public bool IsDeletedForEveryone { get; init; }
    public bool IsDeletedByCustomer { get; init; }
    public DateTime? EditedAt { get; init; }
    public string? FailureReason { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? SenderName { get; init; }
    public Guid? AiInteractionId { get; init; }
    public AiFeedbackDto? AiFeedback { get; init; }
}

public sealed record MessageQuoteDto(
    Guid Id,
    string? Content,
    string Type,
    string Direction);

public sealed record AiFeedbackDto(
    string Rating,
    string? Note,
    string? CorrectedResponse);

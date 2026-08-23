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
    public DateTime? LastMessageAt { get; init; }
    public bool IsQrCode { get; init; }
    public bool IsWindowOpen { get; init; }
}

public sealed record MessageDto
{
    public Guid Id { get; init; }
    public string Direction { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string? Content { get; init; }
    public string? MediaId { get; init; }
    public string? Caption { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? SenderName { get; init; }
}

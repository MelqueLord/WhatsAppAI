namespace WhatsAppAI.Application.Integrations;

public interface IWhatsAppClient
{
    Task<WhatsAppConnectionResult> TestConnectionAsync(
        string phoneNumberId,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<SendMessageResult> SendTextMessageAsync(
        string phoneNumberId,
        string accessToken,
        string recipientPhone,
        string text,
        CancellationToken cancellationToken = default);
}

public sealed record WhatsAppConnectionResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public string? PhoneNumber { get; init; }
    public string? QualityRating { get; init; }
}

public sealed record SendMessageResult
{
    public bool IsSuccess { get; init; }
    public string? MessageId { get; init; }
    public string? ErrorMessage { get; init; }
}

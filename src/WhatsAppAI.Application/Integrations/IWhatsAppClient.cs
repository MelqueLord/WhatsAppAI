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

    // QR Code connection for development/unofficial API
    Task<WhatsAppQrCodeResult> GetQrCodeAsync(
        Guid tenantId,
        int lineNumber = 1,
        CancellationToken cancellationToken = default);

    Task<WhatsAppSessionStatus> GetSessionStatusAsync(
        Guid tenantId,
        int lineNumber = 1,
        CancellationToken cancellationToken = default);

    Task DisconnectSessionAsync(
        Guid tenantId,
        int lineNumber = 1,
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

public sealed record WhatsAppQrCodeResult
{
    public bool IsSuccess { get; init; }
    public string? QrCodeBase64 { get; init; }
    public string? QrCodeData { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed record WhatsAppSessionStatus
{
    public bool IsConnected { get; init; }
    public string? PhoneNumber { get; init; }
    public string? Status { get; init; } // "connected", "disconnected", "qr_pending"
}

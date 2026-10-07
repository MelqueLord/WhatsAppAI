using WhatsAppAI.Domain.Integrations;

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

    Task<SendMessageResult> SendMediaMessageAsync(
        string phoneNumberId,
        string accessToken,
        string recipientPhone,
        Stream mediaStream,
        string contentType,
        long contentLength,
        string contentSha256,
        string? caption,
        string? fileName,
        string? idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<SendMessageResult> SendTemplateMessageAsync(
        string phoneNumberId,
        string accessToken,
        string recipientPhone,
        string templateName,
        string templateLanguage,
        IReadOnlyList<WhatsAppTemplateParameter> parameters,
        CancellationToken cancellationToken = default);

    Task<WhatsAppTemplateListResult> ListTemplatesAsync(
        string wabaId,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<WhatsAppTemplateCreateResult> CreateTemplateAsync(
        string wabaId,
        string accessToken,
        WhatsAppTemplateCreateRequest template,
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

public interface IWhatsAppClientResolver
{
    IWhatsAppClient GetClient(WhatsAppConnectionType connectionType);
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
    public bool IsRetryable { get; init; } = true;
    public string? MessageId { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed record WhatsAppTemplateListResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<WhatsAppTemplateSummary> Templates { get; init; } = [];
}

public sealed record WhatsAppTemplateSummary(
    string Name,
    string Language,
    int BodyParameterCount,
    string Category,
    string Status,
    bool IsCompatible)
{
    public string ParameterFormat { get; init; } = "POSITIONAL";
    public IReadOnlyList<string> BodyParameterNames { get; init; } = [];
    public string? MetaTemplateId { get; init; }
    public string BodyText { get; init; } = string.Empty;
    public string? FooterText { get; init; }
    public string ComponentsJson { get; init; } = "[]";
    public bool CanSendInInbox =>
        string.Equals(Status, "APPROVED", StringComparison.OrdinalIgnoreCase) &&
        (string.Equals(Category, "UTILITY", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(Category, "MARKETING", StringComparison.OrdinalIgnoreCase)) &&
        IsCompatible;

    public bool CanSendInBroadcast =>
        string.Equals(Status, "APPROVED", StringComparison.OrdinalIgnoreCase) &&
        (string.Equals(Category, "UTILITY", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(Category, "MARKETING", StringComparison.OrdinalIgnoreCase)) &&
        IsCompatible &&
        string.Equals(ParameterFormat, "POSITIONAL", StringComparison.OrdinalIgnoreCase);
}

public sealed record WhatsAppTemplateParameter(string Text, string? Name = null);

public sealed record WhatsAppTemplateCreateRequest(
    string Name,
    string Language,
    string Category,
    string BodyText,
    IReadOnlyList<string> BodyExamples,
    string? FooterText);

public sealed record WhatsAppTemplateCreateResult
{
    public bool IsSuccess { get; init; }
    public string? MetaTemplateId { get; init; }
    public string Status { get; init; } = "UNKNOWN";
    public string Category { get; init; } = "UNKNOWN";
    public WhatsAppTemplateFailureKind FailureKind { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
}

public enum WhatsAppTemplateFailureKind
{
    None,
    Validation,
    Duplicate,
    Authorization,
    RateLimit,
    Transient,
    OutcomeUnknown
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

using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Configuration;
using WhatsAppAI.Application.Integrations;

namespace WhatsAppAI.Infrastructure.WhatsApp;

public sealed class WhatsAppWebClient(HttpClient httpClient, IConfiguration configuration) : IWhatsAppClient
{
    private string BaseUrl => configuration["WhatsAppWeb:BaseUrl"] ?? "http://localhost:3020";

    public Task<WhatsAppConnectionResult> TestConnectionAsync(
        string phoneNumberId,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new WhatsAppConnectionResult
        {
            IsSuccess = true,
            PhoneNumber = "WhatsApp Web",
            QualityRating = "GREEN"
        });
    }

    public Task<SendMessageResult> SendTextMessageAsync(
        string phoneNumberId,
        string accessToken,
        string recipientPhone,
        string text,
        CancellationToken cancellationToken = default)
    {
        if (!phoneNumberId.StartsWith("qr:", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new SendMessageResult
            {
                IsSuccess = true,
                MessageId = $"dev-msg-{Guid.NewGuid():N}"
            });
        }

        var parts = phoneNumberId.Split(':', 3);
        if (parts.Length != 3)
        {
            return Task.FromResult(new SendMessageResult
            {
                IsSuccess = false,
                IsRetryable = false,
                ErrorMessage = "Invalid WhatsApp Web session reference."
            });
        }

        return SendWhatsAppWebMessageAsync(parts[1], parts[2], recipientPhone, text, cancellationToken);
    }

    public Task<SendMessageResult> SendMediaMessageAsync(
        string phoneNumberId, string accessToken, string recipientPhone,
        Stream mediaStream, string contentType, long contentLength, string contentSha256,
        string? caption, string? fileName, string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!phoneNumberId.StartsWith("qr:", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(new SendMessageResult { IsSuccess = false, IsRetryable = false, ErrorMessage = "Invalid WhatsApp Web session reference." });

        var parts = phoneNumberId.Split(':', 3);
        if (parts.Length != 3 || contentType is not ("image/jpeg" or "image/png") || contentLength is <= 0 or > 5 * 1024 * 1024 ||
            contentSha256.Length != 64 || string.IsNullOrWhiteSpace(idempotencyKey))
            return Task.FromResult(new SendMessageResult { IsSuccess = false, IsRetryable = false, ErrorMessage = "Invalid image request." });

        return SendWhatsAppWebMediaAsync(parts[1], parts[2], recipientPhone, mediaStream, contentType,
            contentLength, contentSha256, caption, idempotencyKey, cancellationToken);
    }

    public Task<SendMessageResult> SendTemplateMessageAsync(
        string phoneNumberId,
        string accessToken,
        string recipientPhone,
        string templateName,
        string templateLanguage,
        IReadOnlyList<string> parameters,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new SendMessageResult
        {
            IsSuccess = false,
            ErrorMessage = "Templates are available only for the official WhatsApp API."
        });

    public Task<WhatsAppTemplateListResult> ListTemplatesAsync(
        string wabaId,
        string accessToken,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new WhatsAppTemplateListResult
        {
            ErrorMessage = "Templates are available only for the official WhatsApp API."
        });

    private async Task<SendMessageResult> SendWhatsAppWebMessageAsync(
        string tenantId,
        string lineNumber,
        string recipientPhone,
        string text,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await SendToSessionOwnerAsync(
                baseUrl => new HttpRequestMessage(
                    HttpMethod.Post,
                    $"{baseUrl}/sessions/{tenantId}-qr-{lineNumber}/send-message")
                {
                    Content = JsonContent.Create(new { recipientPhone, text })
                },
                cancellationToken);
            var result = await response.Content.ReadFromJsonAsync<BridgeSendResponse>(cancellationToken: cancellationToken);
            return new SendMessageResult
            {
                IsSuccess = response.IsSuccessStatusCode && result?.Success == true,
                IsRetryable = IsRetryableStatus(response.StatusCode),
                MessageId = result?.MessageId,
                ErrorMessage = result?.Error ?? "WhatsApp Web message could not be sent."
            };
        }
        catch
        {
            return new SendMessageResult
            {
                IsSuccess = false,
                ErrorMessage = "Serviço WhatsApp Web indisponível."
            };
        }
    }

    public async Task<WhatsAppQrCodeResult> GetQrCodeAsync(
        Guid tenantId,
        int lineNumber = 1,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await SendToSessionOwnerAsync(
                baseUrl => new HttpRequestMessage(
                    HttpMethod.Get,
                    $"{baseUrl}/sessions/{tenantId:D}-qr-{lineNumber}/qr"),
                cancellationToken);
            var result = response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<BridgeQrResponse>(cancellationToken: cancellationToken)
                : null;

            return new WhatsAppQrCodeResult
            {
                IsSuccess = !string.IsNullOrWhiteSpace(result?.QrCode),
                QrCodeBase64 = result?.QrCode,
                QrCodeData = result?.QrCodeData,
                ErrorMessage = string.IsNullOrWhiteSpace(result?.QrCode) ? "QR ainda não disponível. Aguarde alguns segundos." : null
            };
        }
        catch
        {
            return new WhatsAppQrCodeResult
            {
                IsSuccess = false,
                ErrorMessage = "Serviço WhatsApp Web indisponível. Inicie services/whatsapp-web."
            };
        }
    }

    public async Task<WhatsAppSessionStatus> GetSessionStatusAsync(
        Guid tenantId,
        int lineNumber = 1,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await SendToSessionOwnerAsync(
                baseUrl => new HttpRequestMessage(
                    HttpMethod.Get,
                    $"{baseUrl}/sessions/{tenantId:D}-qr-{lineNumber}/status"),
                cancellationToken);
            var result = response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<BridgeStatusResponse>(cancellationToken: cancellationToken)
                : null;

            return new WhatsAppSessionStatus
            {
                IsConnected = result?.IsConnected ?? false,
                PhoneNumber = result?.PhoneNumber,
                Status = result?.Status ?? "disconnected"
            };
        }
        catch
        {
            return new WhatsAppSessionStatus { IsConnected = false, Status = "bridge_unavailable" };
        }
    }

    public async Task DisconnectSessionAsync(
        Guid tenantId,
        int lineNumber = 1,
        CancellationToken cancellationToken = default)
    {
        using var _ = await SendToSessionOwnerAsync(
            baseUrl => new HttpRequestMessage(
                HttpMethod.Post,
                $"{baseUrl}/sessions/{tenantId:D}-qr-{lineNumber}/logout"),
            cancellationToken);
    }

    private async Task<SendMessageResult> SendWhatsAppWebMediaAsync(
        string tenantId, string lineNumber, string recipientPhone, Stream mediaStream,
        string contentType, long contentLength, string contentSha256, string? caption,
        string idempotencyKey, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await SendToSessionOwnerAsync(baseUrl =>
            {
                if (mediaStream.CanSeek) mediaStream.Position = 0;
                var request = new HttpRequestMessage(HttpMethod.Post,
                    $"{baseUrl}/sessions/{tenantId}-qr-{lineNumber}/send-media");
                var content = new StreamContent(mediaStream);
                content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                content.Headers.ContentLength = contentLength;
                request.Content = content;
                request.Headers.Add("X-WhatsApp-Web-Recipient", recipientPhone);
                request.Headers.Add("X-WhatsApp-Web-Media-Length", contentLength.ToString(System.Globalization.CultureInfo.InvariantCulture));
                request.Headers.Add("X-WhatsApp-Web-Media-Sha256", contentSha256);
                request.Headers.Add("X-WhatsApp-Web-Message-Id", idempotencyKey);
                if (!string.IsNullOrWhiteSpace(caption))
                    request.Headers.Add("X-WhatsApp-Web-Caption", Convert.ToBase64String(Encoding.UTF8.GetBytes(caption)));
                return request;
            }, cancellationToken);
            var result = await response.Content.ReadFromJsonAsync<BridgeSendResponse>(cancellationToken: cancellationToken);
            return new SendMessageResult
            {
                IsSuccess = response.IsSuccessStatusCode && result?.Success == true,
                IsRetryable = IsRetryableStatus(response.StatusCode),
                MessageId = result?.MessageId,
                ErrorMessage = result?.Error ?? "WhatsApp Web image could not be sent."
            };
        }
        catch
        {
            return new SendMessageResult { IsSuccess = false, ErrorMessage = "Serviço WhatsApp Web indisponível." };
        }
    }

    private static bool IsRetryableStatus(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.RequestTimeout ||
        statusCode == HttpStatusCode.TooManyRequests ||
        (int)statusCode >= 500;

    private async Task<HttpResponseMessage> SendToSessionOwnerAsync(
        Func<string, HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken)
    {
        var baseUrl = BaseUrl.TrimEnd('/');
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var request = requestFactory(baseUrl);
            var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode != HttpStatusCode.Conflict)
                return response;

            var owner = await response.Content.ReadFromJsonAsync<BridgeOwnershipResponse>(cancellationToken: cancellationToken);
            response.Dispose();
            if (!TryGetOwnerBaseUrl(owner?.OwnerUrl, out baseUrl))
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        }

        return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
    }

    private static bool TryGetOwnerBaseUrl(string? value, out string baseUrl)
    {
        baseUrl = string.Empty;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") ||
            !string.IsNullOrEmpty(uri.UserInfo))
            return false;

        baseUrl = uri.GetLeftPart(UriPartial.Authority);
        return true;
    }

    private sealed record BridgeQrResponse
    {
        public string? Status { get; init; }
        public string? QrCode { get; init; }
        public string? QrCodeData { get; init; }
    }

    private sealed record BridgeStatusResponse
    {
        public bool IsConnected { get; init; }
        public string? PhoneNumber { get; init; }
        public string? Status { get; init; }
    }

    private sealed record BridgeSendResponse
    {
        public bool Success { get; init; }
        public string? MessageId { get; init; }
        public string? Error { get; init; }
    }

    private sealed record BridgeOwnershipResponse
    {
        public string? OwnerUrl { get; init; }
    }
}

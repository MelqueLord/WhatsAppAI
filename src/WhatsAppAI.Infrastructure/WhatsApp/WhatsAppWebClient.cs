using System.Net.Http.Json;
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
                ErrorMessage = "Invalid WhatsApp Web session reference."
            });
        }

        return SendWhatsAppWebMessageAsync(parts[1], parts[2], recipientPhone, text, cancellationToken);
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

    private async Task<SendMessageResult> SendWhatsAppWebMessageAsync(
        string tenantId,
        string lineNumber,
        string recipientPhone,
        string text,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync(
                $"{BaseUrl}/sessions/{tenantId}-qr-{lineNumber}/send-message",
                new { recipientPhone, text },
                cancellationToken);
            var result = await response.Content.ReadFromJsonAsync<BridgeSendResponse>(cancellationToken: cancellationToken);
            return new SendMessageResult
            {
                IsSuccess = response.IsSuccessStatusCode && result?.Success == true,
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
            var result = await httpClient.GetFromJsonAsync<BridgeQrResponse>(
                $"{BaseUrl}/sessions/{tenantId:D}-qr-{lineNumber}/qr",
                cancellationToken);

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
            var result = await httpClient.GetFromJsonAsync<BridgeStatusResponse>(
                $"{BaseUrl}/sessions/{tenantId:D}-qr-{lineNumber}/status",
                cancellationToken);

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
        await httpClient.PostAsync($"{BaseUrl}/sessions/{tenantId:D}-qr-{lineNumber}/logout", null, cancellationToken);
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
}

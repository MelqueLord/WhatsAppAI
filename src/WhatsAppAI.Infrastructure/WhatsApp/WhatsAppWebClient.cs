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
        return Task.FromResult(new SendMessageResult
        {
            IsSuccess = true,
            MessageId = $"dev-msg-{Guid.NewGuid():N}"
        });
    }

    public async Task<WhatsAppQrCodeResult> GetQrCodeAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await httpClient.GetFromJsonAsync<BridgeQrResponse>(
                $"{BaseUrl}/sessions/{tenantId:N}/qr",
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
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await httpClient.GetFromJsonAsync<BridgeStatusResponse>(
                $"{BaseUrl}/sessions/{tenantId:N}/status",
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
        CancellationToken cancellationToken = default)
    {
        await httpClient.PostAsync($"{BaseUrl}/sessions/{tenantId:N}/logout", null, cancellationToken);
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
}

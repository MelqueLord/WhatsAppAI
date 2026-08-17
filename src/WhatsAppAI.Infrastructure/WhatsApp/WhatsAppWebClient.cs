using System.Collections.Concurrent;
using System.Security.Cryptography;
using WhatsAppAI.Application.Integrations;

namespace WhatsAppAI.Infrastructure.WhatsApp;

/// <summary>
/// Development implementation that simulates WhatsApp Web QR code connection.
/// In production, this would be replaced with a real WhatsApp Web library (Baileys/wwebjs).
/// </summary>
public sealed class WhatsAppWebClient : IWhatsAppClient
{
    // Simulated sessions storage
    private static readonly ConcurrentDictionary<Guid, WhatsAppSession> _sessions = new();

    public Task<WhatsAppConnectionResult> TestConnectionAsync(
        string phoneNumberId,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        // Simulate successful connection for development
        return Task.FromResult(new WhatsAppConnectionResult
        {
            IsSuccess = true,
            PhoneNumber = "+55 11 99999-0000",
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
        // Simulate successful message send
        return Task.FromResult(new SendMessageResult
        {
            IsSuccess = true,
            MessageId = $"dev-msg-{Guid.NewGuid():N}"
        });
    }

    public Task<WhatsAppQrCodeResult> GetQrCodeAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var session = _sessions.GetOrAdd(tenantId, id => new WhatsAppSession
        {
            TenantId = id,
            Status = "qr_pending",
            QrCode = GenerateQrCode(),
            ConnectedAt = null
        });

        if (session.Status == "connected")
        {
            return Task.FromResult(new WhatsAppQrCodeResult
            {
                IsSuccess = false,
                ErrorMessage = "Session already connected"
            });
        }

        // Refresh QR code if expired
        if (session.QrCodeExpiry < DateTime.UtcNow)
        {
            session.QrCode = GenerateQrCode();
            session.QrCodeExpiry = DateTime.UtcNow.AddSeconds(30);
        }

        return Task.FromResult(new WhatsAppQrCodeResult
        {
            IsSuccess = true,
            QrCodeBase64 = session.QrCode,
            QrCodeData = $"whatsapp-web-{tenantId:N}"
        });
    }

    public Task<WhatsAppSessionStatus> GetSessionStatusAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (_sessions.TryGetValue(tenantId, out var session))
        {
            // Simulate connection after 5 seconds (for demo)
            if (session.Status == "qr_pending" && session.CreatedAt.AddSeconds(5) < DateTime.UtcNow)
            {
                session.Status = "connected";
                session.PhoneNumber = "+55 11 99999-0000";
                session.ConnectedAt = DateTime.UtcNow;
            }

            return Task.FromResult(new WhatsAppSessionStatus
            {
                IsConnected = session.Status == "connected",
                PhoneNumber = session.PhoneNumber,
                Status = session.Status
            });
        }

        return Task.FromResult(new WhatsAppSessionStatus
        {
            IsConnected = false,
            Status = "disconnected"
        });
    }

    public Task DisconnectSessionAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        _sessions.TryRemove(tenantId, out _);
        return Task.CompletedTask;
    }

    private static string GenerateQrCode()
    {
        // Generate a random QR code-like string for development
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    private sealed class WhatsAppSession
    {
        public Guid TenantId { get; init; }
        public string Status { get; set; } = "disconnected";
        public string? QrCode { get; set; }
        public DateTime? QrCodeExpiry { get; set; }
        public string? PhoneNumber { get; set; }
        public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
        public DateTime? ConnectedAt { get; set; }
    }
}

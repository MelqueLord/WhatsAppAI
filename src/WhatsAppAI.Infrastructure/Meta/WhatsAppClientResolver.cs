using WhatsAppAI.Application.Integrations;
using WhatsAppAI.Domain.Integrations;
using WhatsAppAI.Infrastructure.WhatsApp;

namespace WhatsAppAI.Infrastructure.Meta;

internal sealed class WhatsAppClientResolver(
    WhatsAppClient officialApiClient,
    WhatsAppWebClient qrCodeClient) : IWhatsAppClientResolver
{
    public IWhatsAppClient GetClient(WhatsAppConnectionType connectionType) => connectionType switch
    {
        WhatsAppConnectionType.OfficialApi => officialApiClient,
        WhatsAppConnectionType.QrCode => qrCodeClient,
        _ => throw new ArgumentOutOfRangeException(nameof(connectionType), connectionType, null)
    };
}

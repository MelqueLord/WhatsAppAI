using WhatsAppAI.Infrastructure.Workers;

namespace WhatsAppAI.UnitTests.Webhooks;

public sealed class WebhookPhoneNumberTests
{
    [Fact]
    public void NormalizePhoneNumber_RestoresBrazilianMobileFromQrDeviceIdentity()
    {
        Assert.Equal("5571996531915", WebhookProcessingWorker.NormalizePhoneNumber("557196531915:0"));
    }

    [Fact]
    public void NormalizePhoneNumber_PreservesOrdinaryPhoneIdentity()
    {
        Assert.Equal("5511999990000", WebhookProcessingWorker.NormalizePhoneNumber("+55 (11) 99999-0000"));
    }
}

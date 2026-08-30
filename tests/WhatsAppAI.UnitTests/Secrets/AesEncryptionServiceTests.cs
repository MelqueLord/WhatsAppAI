using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using WhatsAppAI.Infrastructure.Secrets;

namespace WhatsAppAI.UnitTests.Secrets;

public sealed class AesEncryptionServiceTests
{
    private static AesEncryptionService CreateService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Encryption:Key"] = Convert.ToBase64String(new byte[32])
            })
            .Build();

        return new AesEncryptionService(configuration);
    }

    [Fact]
    public void EncryptDecrypt_RoundTripsPlainText()
    {
        var service = CreateService();

        var encrypted = service.Encrypt("segredo do tenant");

        Assert.Equal("segredo do tenant", service.Decrypt(encrypted));
    }

    [Fact]
    public void Decrypt_WhenCiphertextIsTampered_RejectsBeforeReturningPlainText()
    {
        var service = CreateService();
        var encrypted = Convert.FromBase64String(service.Encrypt("segredo do tenant"));
        encrypted[^1] ^= 0x01;

        Assert.Throws<CryptographicException>(() => service.Decrypt(Convert.ToBase64String(encrypted)));
    }

    [Fact]
    public void Decrypt_WhenCiphertextIsLegacyOrMalformed_RejectsUnauthenticatedPayload()
    {
        var service = CreateService();
        var legacy = new byte[16 + 16];

        Assert.Throws<CryptographicException>(() => service.Decrypt(Convert.ToBase64String(legacy)));
    }
}

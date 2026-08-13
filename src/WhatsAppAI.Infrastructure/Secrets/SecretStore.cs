using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Integrations;

namespace WhatsAppAI.Infrastructure.Secrets;

internal sealed class SecretStore(
    ISecretRepository secretRepository,
    IEncryptionService encryptionService) : ISecretStore
{
    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var secret = await secretRepository.GetByKeyAsync(key, cancellationToken: cancellationToken);
        if (secret is null)
            return null;

        return encryptionService.Decrypt(secret.EncryptedValue);
    }

    public async Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        var encryptedValue = encryptionService.Encrypt(value);
        var existing = await secretRepository.GetByKeyAsync(key, cancellationToken: cancellationToken);

        if (existing is null)
        {
            var secret = Secret.Create(key, encryptedValue);
            await secretRepository.AddAsync(secret, cancellationToken);
        }
        else
        {
            existing.UpdateValue(encryptedValue);
            await secretRepository.UpdateAsync(existing, cancellationToken);
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        await secretRepository.DeleteAsync(key, cancellationToken: cancellationToken);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        var secret = await secretRepository.GetByKeyAsync(key, cancellationToken: cancellationToken);
        return secret is not null;
    }
}

public interface IEncryptionService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
}

internal sealed class AesEncryptionService : IEncryptionService
{
    private readonly byte[] _key;

    public AesEncryptionService(IConfiguration configuration)
    {
        var keyBase64 = configuration["Encryption:Key"]
            ?? throw new InvalidOperationException("Encryption key not configured.");
        _key = Convert.FromBase64String(keyBase64);
    }

    public string Encrypt(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream();
        ms.Write(aes.IV, 0, aes.IV.Length);

        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        using (var writer = new StreamWriter(cs))
        {
            writer.Write(plainText);
        }

        return Convert.ToBase64String(ms.ToArray());
    }

    public string Decrypt(string cipherText)
    {
        var buffer = Convert.FromBase64String(cipherText);

        using var aes = Aes.Create();
        aes.Key = _key;

        var iv = new byte[16];
        Array.Copy(buffer, 0, iv, 0, 16);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream(buffer, 16, buffer.Length - 16);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var reader = new StreamReader(cs);

        return reader.ReadToEnd();
    }
}

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
    private const byte CurrentFormatVersion = 1;
    private const int IvSize = 16;
    private const int AuthenticationTagSize = 32;
    private readonly byte[] _key;
    private readonly byte[] _macKey;

    public AesEncryptionService(IConfiguration configuration)
    {
        var keyBase64 = configuration["Encryption:Key"]
            ?? throw new InvalidOperationException("Encryption key not configured.");
        _key = Convert.FromBase64String(keyBase64);
        if (_key.Length is not (16 or 24 or 32))
            throw new InvalidOperationException("Encryption key must be 128, 192, or 256 bits.");

        // Derive a separate authentication key so encryption and integrity do not
        // share key material directly.
        using var keyDerivation = new HMACSHA256(_key);
        _macKey = keyDerivation.ComputeHash("WhatsAppAI:secret-integrity:v1"u8.ToArray());
    }

    public string Encrypt(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.GenerateIV();

        var plainBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        var authenticatedData = new byte[1 + aes.IV.Length + cipherBytes.Length];
        authenticatedData[0] = CurrentFormatVersion;
        Buffer.BlockCopy(aes.IV, 0, authenticatedData, 1, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, authenticatedData, 1 + aes.IV.Length, cipherBytes.Length);

        using var mac = new HMACSHA256(_macKey);
        var tag = mac.ComputeHash(authenticatedData);
        var payload = new byte[authenticatedData.Length + tag.Length];
        Buffer.BlockCopy(authenticatedData, 0, payload, 0, authenticatedData.Length);
        Buffer.BlockCopy(tag, 0, payload, authenticatedData.Length, tag.Length);

        return Convert.ToBase64String(payload);
    }

    public string Decrypt(string cipherText)
    {
        byte[] buffer;
        try
        {
            buffer = Convert.FromBase64String(cipherText);
        }
        catch (FormatException ex)
        {
            throw new CryptographicException("Ciphertext is invalid.", ex);
        }

        if (buffer.Length < 1 + IvSize + AuthenticationTagSize + 1 ||
            buffer[0] != CurrentFormatVersion)
            throw new CryptographicException("Ciphertext format is invalid.");

        var authenticatedLength = buffer.Length - AuthenticationTagSize;
        var authenticatedData = buffer.AsSpan(0, authenticatedLength);
        var receivedTag = buffer.AsSpan(authenticatedLength, AuthenticationTagSize);

        using var mac = new HMACSHA256(_macKey);
        var expectedTag = mac.ComputeHash(authenticatedData.ToArray());
        if (!CryptographicOperations.FixedTimeEquals(receivedTag, expectedTag))
            throw new CryptographicException("Ciphertext authentication failed.");

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        var iv = authenticatedData.Slice(1, IvSize).ToArray();
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        var cipherBytes = authenticatedData.Slice(1 + IvSize).ToArray();
        try
        {
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return System.Text.Encoding.UTF8.GetString(plainBytes);
        }
        catch (CryptographicException ex)
        {
            throw new CryptographicException("Ciphertext is invalid.", ex);
        }
    }
}

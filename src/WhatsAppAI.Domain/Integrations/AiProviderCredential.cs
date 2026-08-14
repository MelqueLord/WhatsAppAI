namespace WhatsAppAI.Domain.Integrations;

public sealed class AiProviderCredential
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Provider { get; private set; } = "OpenAI";
    public string ModelId { get; private set; } = string.Empty;
    public string ApiKeyRef { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public uint Version { get; private set; }

    private AiProviderCredential() { }

    public static AiProviderCredential Create(
        Guid tenantId,
        string provider,
        string modelId,
        string apiKeyRef)
    {
        return new AiProviderCredential
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Provider = provider.Trim(),
            ModelId = modelId.Trim(),
            ApiKeyRef = apiKeyRef,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string modelId, string apiKeyRef)
    {
        ModelId = modelId.Trim();
        ApiKeyRef = apiKeyRef;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }
}

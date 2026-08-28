namespace WhatsAppAI.Domain.Integrations;

public sealed class AiProviderCredential
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Provider { get; private set; } = "OpenAI";
    public string ModelId { get; private set; } = string.Empty;
    public string ApiKeyRef { get; private set; } = string.Empty;
    public string? SystemPrompt { get; private set; }
    public string? RoutingQueueIdsJson { get; private set; }
    public string? RoutingTagIdsJson { get; private set; }
    public int MaxTokensPerResponse { get; private set; } = 500;
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

    public void UpdateInstructions(
        string? systemPrompt,
        int maxTokensPerResponse,
        uint expectedVersion,
        IEnumerable<Guid>? routingQueueIds = null,
        IEnumerable<Guid>? routingTagIds = null)
    {
        if (Version != expectedVersion)
            throw new ConcurrencyException(
                $"Version conflict: expected {expectedVersion}, actual {Version}.");

        SystemPrompt = systemPrompt?.Trim();
        MaxTokensPerResponse = Math.Clamp(maxTokensPerResponse, 80, 300);
        RoutingQueueIdsJson = SerializeGuidList(routingQueueIds);
        RoutingTagIdsJson = SerializeGuidList(routingTagIds);
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    public IReadOnlyList<Guid> GetRoutingTagIds()
    {
        return ParseGuidList(RoutingTagIdsJson);
    }

    public IReadOnlyList<Guid> GetRoutingQueueIds()
    {
        return ParseGuidList(RoutingQueueIdsJson);
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

    private static string SerializeGuidList(IEnumerable<Guid>? ids)
    {
        return string.Join(',', (ids ?? []).Distinct().OrderBy(id => id).Select(id => id.ToString("D")));
    }

    private static List<Guid> ParseGuidList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        var list = new List<Guid>();
        var parts = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (Guid.TryParse(part, out var id))
            {
                list.Add(id);
            }
        }

        return list;
    }
}

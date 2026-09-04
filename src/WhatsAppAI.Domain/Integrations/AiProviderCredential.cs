namespace WhatsAppAI.Domain.Integrations;

public sealed class AiProviderCredential
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Provider { get; private set; } = "OpenAI";
    public string ModelId { get; private set; } = string.Empty;
    public string ApiKeyRef { get; private set; } = string.Empty;
    public string CredentialScope { get; private set; } = AiCredentialScopes.TenantProject;
    public string? SystemPrompt { get; private set; }
    public string? DraftSystemPrompt { get; private set; }
    public string? RoutingQueueIdsJson { get; private set; }
    public string? DraftRoutingQueueIdsJson { get; private set; }
    public string? RoutingTagIdsJson { get; private set; }
    public string? DraftRoutingTagIdsJson { get; private set; }
    public int MaxTokensPerResponse { get; private set; } = 180;
    public int DraftMaxTokensPerResponse { get; private set; } = 180;
    public double DraftConfidenceThreshold { get; private set; } = 0.5;
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public uint Version { get; private set; }
    public uint DraftVersion { get; private set; }
    public uint PublishedVersion { get; private set; }
    public DateTime? PublishedAt { get; private set; }

    private AiProviderCredential() { }

    public static AiProviderCredential Create(
        Guid tenantId,
        string provider,
        string modelId,
        string apiKeyRef,
        string? credentialScope = null)
    {
        if (!AiCredentialScopes.IsSupported(credentialScope ?? AiCredentialScopes.TenantProject))
            throw new ArgumentException("Unsupported AI credential scope.", nameof(credentialScope));

        return new AiProviderCredential
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Provider = provider.Trim(),
            ModelId = modelId.Trim(),
            ApiKeyRef = apiKeyRef,
            CredentialScope = AiCredentialScopes.Normalize(credentialScope),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            DraftConfidenceThreshold = 0.5
        };
    }

    public void Update(string modelId, string apiKeyRef, string? credentialScope = null)
    {
        if (credentialScope is not null && !AiCredentialScopes.IsSupported(credentialScope))
            throw new ArgumentException("Unsupported AI credential scope.", nameof(credentialScope));

        ModelId = modelId.Trim();
        ApiKeyRef = apiKeyRef;
        if (credentialScope is not null)
            CredentialScope = AiCredentialScopes.Normalize(credentialScope);
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

        DraftSystemPrompt = systemPrompt?.Trim();
        DraftMaxTokensPerResponse = Math.Clamp(maxTokensPerResponse, 80, 240);
        DraftRoutingQueueIdsJson = SerializeGuidList(routingQueueIds);
        DraftRoutingTagIdsJson = SerializeGuidList(routingTagIds);
        UpdatedAt = DateTime.UtcNow;
        DraftVersion = Version + 1;
        Version++;
    }

    public void UpdateDraftConfidenceThreshold(double confidenceThreshold)
    {
        if (double.IsNaN(confidenceThreshold) || confidenceThreshold is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(confidenceThreshold));

        DraftConfidenceThreshold = confidenceThreshold;
    }

    public void PublishDraft(uint expectedVersion)
    {
        if (Version != expectedVersion)
            throw new ConcurrencyException($"Version conflict: expected {expectedVersion}, actual {Version}.");
        if (DraftVersion == 0)
            throw new InvalidOperationException("There is no AI draft to publish.");

        SystemPrompt = DraftSystemPrompt;
        MaxTokensPerResponse = DraftMaxTokensPerResponse;
        RoutingQueueIdsJson = DraftRoutingQueueIdsJson;
        RoutingTagIdsJson = DraftRoutingTagIdsJson;
        PublishedVersion = DraftVersion;
        PublishedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    public IReadOnlyList<Guid> GetRoutingTagIds()
    {
        return ParseGuidList(RoutingTagIdsJson);
    }

    public IReadOnlyList<Guid> GetDraftRoutingTagIds() => ParseGuidList(DraftRoutingTagIdsJson);

    public IReadOnlyList<Guid> GetDraftRoutingQueueIds() => ParseGuidList(DraftRoutingQueueIdsJson);

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

namespace WhatsAppAI.Domain.Integrations;

public sealed class BotConfiguration
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public BotMode Mode { get; private set; }
    public string? WelcomeMessage { get; private set; }
    public string? ReturningMessage { get; private set; }
    public string? OfflineMessage { get; private set; }
    public string? FallbackMessage { get; private set; }
    public string? HandoffMessage { get; private set; }
    public string? MediaMessage { get; private set; }
    public string? FlowStepsJson { get; private set; }
    public int MaxTokensPerResponse { get; private set; }
    public bool Enabled { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public uint Version { get; private set; }

    private BotConfiguration() { }

    public static BotConfiguration Create(Guid tenantId, BotMode mode = BotMode.Manual)
    {
        return new BotConfiguration
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Mode = mode,
            MaxTokensPerResponse = 500,
            Enabled = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateMode(BotMode mode)
    {
        Mode = mode;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    public void UpdateMessages(string? welcome, string? returning, string? offline, string? fallback, string? handoff, string? media)
    {
        WelcomeMessage = welcome?.Trim();
        ReturningMessage = returning?.Trim();
        OfflineMessage = offline?.Trim();
        FallbackMessage = fallback?.Trim();
        HandoffMessage = handoff?.Trim();
        MediaMessage = media?.Trim();
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    public void UpdateMessages(string? welcome, string? offline, string? fallback, string? handoff, string? media)
    {
        UpdateMessages(welcome, ReturningMessage, offline, fallback, handoff, media);
    }

    public void UpdateTokenLimit(int maxTokens)
    {
        MaxTokensPerResponse = Math.Clamp(maxTokens, 50, 2000);
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    public void UpdateFlowSteps(string? flowStepsJson)
    {
        FlowStepsJson = flowStepsJson;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    public void Toggle(bool enabled)
    {
        Enabled = enabled;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }
}

public enum BotMode
{
    Manual = 0,
    SimpleAutoReply = 1,
    AiPowered = 2
}

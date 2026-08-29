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
    public string? QueueTransferMessage { get; private set; }
    public string? MediaMessage { get; private set; }
    public string? FlowStepsJson { get; private set; }
    public bool BusinessHoursEnabled { get; private set; }
    public string TimeZoneId { get; private set; } = "America/Sao_Paulo";
    public string? BusinessHoursJson { get; private set; }
    public double ConfidenceThreshold { get; private set; } = 0.5;
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
            ConfidenceThreshold = 0.5,
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

    public void UpdateMessages(string? welcome, string? returning, string? offline, string? fallback, string? handoff, string? queueTransfer, string? media)
    {
        WelcomeMessage = welcome?.Trim();
        ReturningMessage = returning?.Trim();
        OfflineMessage = offline?.Trim();
        FallbackMessage = fallback?.Trim();
        HandoffMessage = handoff?.Trim();
        QueueTransferMessage = queueTransfer?.Trim();
        MediaMessage = media?.Trim();
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    public void UpdateMessages(string? welcome, string? offline, string? fallback, string? handoff, string? queueTransfer, string? media)
    {
        UpdateMessages(welcome, ReturningMessage, offline, fallback, handoff, queueTransfer, media);
    }

    public void UpdateConfidenceThreshold(double confidenceThreshold)
    {
        if (double.IsNaN(confidenceThreshold) || confidenceThreshold is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(confidenceThreshold));

        ConfidenceThreshold = confidenceThreshold;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    public void UpdateFlowSteps(string? flowStepsJson)
    {
        FlowStepsJson = flowStepsJson;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    public void UpdateBusinessHours(bool enabled, string? timeZoneId, string? businessHoursJson)
    {
        if (!BusinessHoursPolicy.TryValidate(enabled, timeZoneId, businessHoursJson, out var error))
            throw new ArgumentException(error, nameof(businessHoursJson));

        BusinessHoursEnabled = enabled;
        TimeZoneId = string.IsNullOrWhiteSpace(timeZoneId) ? "America/Sao_Paulo" : timeZoneId.Trim();
        BusinessHoursJson = enabled ? businessHoursJson : null;
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

namespace WhatsAppAI.Domain.Integrations;

public sealed class WhatsAppMessageTemplate
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid WhatsAppBusinessAccountId { get; private set; }
    public string? MetaTemplateId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Language { get; private set; } = string.Empty;
    public string? RequestedCategory { get; private set; }
    public string EffectiveCategory { get; private set; } = "UNKNOWN";
    public string ParameterFormat { get; private set; } = "POSITIONAL";
    public string BodyText { get; private set; } = string.Empty;
    public string? FooterText { get; private set; }
    public string BodyExamplesJson { get; private set; } = "[]";
    public int BodyParameterCount { get; private set; }
    public string ComponentsJson { get; private set; } = "[]";
    public WhatsAppTemplateReviewStatus ReviewStatus { get; private set; }
    public string ProviderRawStatus { get; private set; } = "UNKNOWN";
    public string? RejectionReason { get; private set; }
    public string? Recommendation { get; private set; }
    public bool IsInboxCompatible { get; private set; }
    public bool IsBroadcastCompatible { get; private set; }
    public DateTime? ProviderUpdatedAt { get; private set; }
    public DateTime? LastSyncedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public uint Version { get; private set; }

    private WhatsAppMessageTemplate() { }

    public static WhatsAppMessageTemplate CreatePending(Guid tenantId, Guid wabaId, string name, string language,
        string category, string bodyText, string? footerText, IReadOnlyList<string> examples, int parameterCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        ArgumentException.ThrowIfNullOrWhiteSpace(bodyText);
        return new WhatsAppMessageTemplate
        {
            Id = Guid.NewGuid(), TenantId = tenantId, WhatsAppBusinessAccountId = wabaId,
            Name = name.Trim(), Language = language.Trim(), RequestedCategory = category,
            EffectiveCategory = category, BodyText = bodyText, FooterText = string.IsNullOrWhiteSpace(footerText) ? null : footerText,
            BodyExamplesJson = System.Text.Json.JsonSerializer.Serialize(examples), BodyParameterCount = parameterCount,
            ComponentsJson = BuildComponentsJson(bodyText, footerText), ReviewStatus = WhatsAppTemplateReviewStatus.Pending,
            ProviderRawStatus = "PENDING", CreatedAt = DateTime.UtcNow
        };
    }

    public void ApplyProviderSnapshot(string? metaTemplateId, string category, string rawStatus, string bodyText,
        string? footerText, int parameterCount, string componentsJson, DateTime observedAt,
        string? rejectionReason = null, string? recommendation = null)
    {
        MetaTemplateId = string.IsNullOrWhiteSpace(metaTemplateId) ? MetaTemplateId : metaTemplateId.Trim();
        EffectiveCategory = Normalize(category, 40, "UNKNOWN");
        ProviderRawStatus = Normalize(rawStatus, 60, "UNKNOWN");
        ReviewStatus = ParseReviewStatus(ProviderRawStatus);
        BodyText = bodyText.Length <= 1024 ? bodyText : bodyText[..1024];
        FooterText = string.IsNullOrWhiteSpace(footerText) ? null : footerText[..Math.Min(footerText.Length, 60)];
        BodyParameterCount = Math.Clamp(parameterCount, 0, 10);
        ComponentsJson = componentsJson.Length <= 20000 ? componentsJson : "[]";
        RejectionReason = Sanitize(rejectionReason, 500);
        Recommendation = Sanitize(recommendation, 1000);
        ProviderUpdatedAt = observedAt;
        LastSyncedAt = DateTime.UtcNow;
        RecalculateCompatibility();
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    public void TouchSynced() => LastSyncedAt = DateTime.UtcNow;

    private void RecalculateCompatibility()
    {
        var supported = ReviewStatus == WhatsAppTemplateReviewStatus.Approved && BodyParameterCount <= 10 &&
            ComponentsJson.Contains("BODY", StringComparison.OrdinalIgnoreCase) &&
            !ComponentsJson.Contains("HEADER", StringComparison.OrdinalIgnoreCase) &&
            !ComponentsJson.Contains("BUTTONS", StringComparison.OrdinalIgnoreCase);
        IsInboxCompatible = supported && EffectiveCategory is "UTILITY" or "MARKETING";
        IsBroadcastCompatible = supported && EffectiveCategory == "UTILITY";
    }

    private static string BuildComponentsJson(string body, string? footer) =>
        System.Text.Json.JsonSerializer.Serialize(string.IsNullOrWhiteSpace(footer)
            ? new object[] { new { type = "BODY", text = body } }
            : [new { type = "BODY", text = body }, new { type = "FOOTER", text = footer }]);

    private static WhatsAppTemplateReviewStatus ParseReviewStatus(string status) => status switch
    {
        "PENDING" => WhatsAppTemplateReviewStatus.Pending,
        "APPROVED" or "REINSTATED" => WhatsAppTemplateReviewStatus.Approved,
        "REJECTED" => WhatsAppTemplateReviewStatus.Rejected,
        "PAUSED" or "FLAGGED" => WhatsAppTemplateReviewStatus.Paused,
        "DISABLED" or "LOCKED" => WhatsAppTemplateReviewStatus.Disabled,
        "ARCHIVED" => WhatsAppTemplateReviewStatus.Archived,
        "DELETED" or "PENDING_DELETION" => WhatsAppTemplateReviewStatus.Deleted,
        _ => WhatsAppTemplateReviewStatus.Unknown
    };

    private static string Normalize(string? value, int max, string fallback)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized)) return fallback;
        return normalized[..Math.Min(normalized.Length, max)];
    }

    private static string? Sanitize(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = new string(value.Where(c => !char.IsControl(c) || c is '\r' or '\n' or '\t').ToArray()).Trim();
        return normalized[..Math.Min(normalized.Length, max)];
    }
}

public enum WhatsAppTemplateReviewStatus { Unknown, Pending, Approved, Rejected, Paused, Disabled, Archived, Deleted }

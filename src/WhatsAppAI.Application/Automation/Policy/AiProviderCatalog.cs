namespace WhatsAppAI.Application.Automation.Policy;

public sealed record AiModelDefinition(string Id, string Name);

public sealed record AiProviderDefinition(
    string Id,
    string Name,
    IReadOnlyList<AiModelDefinition> Models);

/// <summary>
/// The single source of truth for provider identifiers and selectable models.
/// </summary>
public static class AiProviderCatalog
{
    private static readonly IReadOnlyList<AiProviderDefinition> Definitions =
    [
        new("openai", "OpenAI", [
            new("gpt-4o", "GPT-4o"),
            new("gpt-4o-mini", "GPT-4o Mini"),
            new("gpt-4.1-mini", "GPT-4.1 Mini")
        ]),
        new("gemini", "Google Gemini", [
            new("gemini-3.1-pro-preview", "Gemini 3.1 Pro Preview"),
            new("gemini-3.6-flash", "Gemini 3.6 Flash")
        ]),
        new("anthropic", "Anthropic", [
            new("claude-sonnet-4-20250514", "Claude Sonnet 4"),
            new("claude-haiku-3-5-20241022", "Claude Haiku 3.5")
        ]),
        new("xiaomi", "Xiaomi MiMo", [
            new("mimo-v2.5-pro", "MiMo v2.5 Pro"),
            new("mimo-v2.5", "MiMo v2.5")
        ]),
        new("grok", "xAI Grok", [
            new("grok-4.6", "Grok 4.6"),
            new("grok-4.5", "Grok 4.5"),
            new("grok-4.3", "Grok 4.3")
        ]),
        new("groq", "Groq", [
            new("openai/gpt-oss-120b", "GPT-OSS 120B"),
            new("openai/gpt-oss-20b", "GPT-OSS 20B"),
            new("qwen/qwen3.6-27b", "Qwen 3.6 27B")
        ])
    ];

    public static IReadOnlyList<AiProviderDefinition> Providers => Definitions;

    public static string NormalizeProvider(string? provider)
        => provider?.Trim().ToLowerInvariant() ?? string.Empty;

    public static string NormalizeModelId(string? modelId)
    {
        var normalized = modelId?.Trim() ?? string.Empty;
        return normalized.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
            ? normalized["models/".Length..]
            : normalized;
    }

    public static bool IsSupported(string? provider)
        => Definitions.Any(definition => string.Equals(definition.Id, NormalizeProvider(provider), StringComparison.Ordinal));

    public static bool IsModelAllowed(string? provider, string? modelId)
    {
        var definition = Definitions.FirstOrDefault(item =>
            string.Equals(item.Id, NormalizeProvider(provider), StringComparison.Ordinal));
        var normalizedModelId = NormalizeModelId(modelId);
        return definition?.Models.Any(model =>
            string.Equals(model.Id, normalizedModelId, StringComparison.OrdinalIgnoreCase)) == true;
    }

    public static AiProviderDefinition? Find(string? provider)
        => Definitions.FirstOrDefault(definition =>
            string.Equals(definition.Id, NormalizeProvider(provider), StringComparison.Ordinal));
}

namespace WhatsAppAI.Application.Automation.Policy;

public static class AiModelPolicy
{
    private static readonly IReadOnlyDictionary<string, HashSet<string>> AllowedModels =
        new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["openai"] = new(StringComparer.OrdinalIgnoreCase) { "gpt-4o", "gpt-4o-mini", "gpt-4.1-mini" },
            ["gemini"] = new(StringComparer.OrdinalIgnoreCase) { "gemini-3.1-pro-preview", "gemini-3.6-flash" },
            ["anthropic"] = new(StringComparer.OrdinalIgnoreCase) { "claude-sonnet-4-20250514", "claude-haiku-3-5-20241022" },
            ["xiaomi"] = new(StringComparer.OrdinalIgnoreCase) { "mimo-v2.5-pro", "mimo-v2.5" },
            ["grok"] = new(StringComparer.OrdinalIgnoreCase) { "grok-4.6", "grok-4.5", "grok-4.3" },
            ["groq"] = new(StringComparer.OrdinalIgnoreCase) { "openai/gpt-oss-120b", "openai/gpt-oss-20b", "qwen/qwen3.6-27b" }
        };

    public static bool IsAllowed(string? provider, string? modelId)
    {
        if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(modelId) ||
            !AllowedModels.TryGetValue(provider, out var models))
            return false;

        var normalizedModelId = modelId.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
            ? modelId["models/".Length..]
            : modelId;
        return models.Contains(normalizedModelId);
    }
}

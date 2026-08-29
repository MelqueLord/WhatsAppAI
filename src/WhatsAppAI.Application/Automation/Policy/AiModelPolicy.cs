namespace WhatsAppAI.Application.Automation.Policy;

public static class AiModelPolicy
{
    public static bool IsAllowed(string? provider, string? modelId)
        => AiProviderCatalog.IsModelAllowed(provider, modelId);
}

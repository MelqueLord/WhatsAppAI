using WhatsAppAI.Application.Automation;
using WhatsAppAI.Application.Automation.Policy;

namespace WhatsAppAI.Infrastructure;

public sealed class AiProviderResolver : IAiProviderResolver
{
    private readonly Dictionary<string, IAiProvider> _providers;

    public AiProviderResolver(IEnumerable<KeyValuePair<string, IAiProvider>> providers)
    {
        _providers = new Dictionary<string, IAiProvider>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, provider) in providers)
        {
            var normalizedName = AiProviderCatalog.NormalizeProvider(name);
            if (AiProviderCatalog.IsSupported(normalizedName))
                _providers[normalizedName] = provider;
        }
    }

    public IAiProvider Resolve(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            throw new InvalidOperationException("Provider name cannot be empty.");

        var normalizedName = AiProviderCatalog.NormalizeProvider(providerName);
        if (_providers.TryGetValue(normalizedName, out var provider))
            return provider;

        throw new InvalidOperationException(
            $"AI provider '{providerName}' is not registered. Available: {string.Join(", ", _providers.Keys)}");
    }

    public IReadOnlyList<string> GetRegisteredProviders()
        => AiProviderCatalog.Providers
            .Select(definition => definition.Id)
            .Where(_providers.ContainsKey)
            .ToList()
            .AsReadOnly();
}

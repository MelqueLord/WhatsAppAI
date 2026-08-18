using WhatsAppAI.Application.Automation;

namespace WhatsAppAI.Infrastructure;

public sealed class AiProviderResolver : IAiProviderResolver
{
    private readonly Dictionary<string, IAiProvider> _providers;

    public AiProviderResolver(IEnumerable<KeyValuePair<string, IAiProvider>> providers)
    {
        _providers = new Dictionary<string, IAiProvider>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, provider) in providers)
        {
            _providers[name] = provider;
        }
    }

    public IAiProvider Resolve(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            throw new InvalidOperationException("Provider name cannot be empty.");

        if (_providers.TryGetValue(providerName, out var provider))
            return provider;

        throw new InvalidOperationException(
            $"AI provider '{providerName}' is not registered. Available: {string.Join(", ", _providers.Keys)}");
    }

    public IReadOnlyList<string> GetRegisteredProviders()
        => _providers.Keys.ToList().AsReadOnly();
}

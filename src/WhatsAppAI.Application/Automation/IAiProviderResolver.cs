namespace WhatsAppAI.Application.Automation;

/// <summary>
/// Resolves IAiProvider implementations by provider name.
/// </summary>
public interface IAiProviderResolver
{
    /// <summary>
    /// Resolves the AI provider for the given provider name.
    /// </summary>
    /// <param name="providerName">Provider identifier (openai, gemini, anthropic, xiaomi). Case-insensitive.</param>
    /// <returns>The IAiProvider implementation for the requested provider.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the provider name is not registered.</exception>
    IAiProvider Resolve(string providerName);

    /// <summary>
    /// Lists all registered provider names.
    /// </summary>
    IReadOnlyList<string> GetRegisteredProviders();
}

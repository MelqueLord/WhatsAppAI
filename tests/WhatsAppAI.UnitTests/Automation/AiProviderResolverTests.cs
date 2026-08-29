using WhatsAppAI.Application.Automation;
using WhatsAppAI.Infrastructure;
using Xunit;

namespace WhatsAppAI.UnitTests.Automation;

public sealed class AiProviderResolverTests
{
    private static readonly IAiProvider Dummy = new DummyProvider();

    private static AiProviderResolver CreateResolver(params (string name, IAiProvider provider)[] providers)
        => new(providers.Select(p => new KeyValuePair<string, IAiProvider>(p.name, p.provider)));

    [Fact]
    public void Resolve_ReturnsCorrectProvider()
    {
        var resolver = CreateResolver(("openai", Dummy));
        Assert.Same(Dummy, resolver.Resolve("openai"));
    }

    [Theory]
    [InlineData("OpenAI")]
    [InlineData("OPENAI")]
    [InlineData("openai")]
    public void Resolve_IsCaseInsensitive(string name)
    {
        var resolver = CreateResolver(("openai", Dummy));
        Assert.Same(Dummy, resolver.Resolve(name));
    }

    [Fact]
    public void Resolve_ThrowsOnUnknownProvider()
    {
        var resolver = CreateResolver(("openai", Dummy));
        var ex = Assert.Throws<InvalidOperationException>(() => resolver.Resolve("gemini"));
        Assert.Contains("gemini", ex.Message);
        Assert.Contains("openai", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Resolve_ThrowsOnEmptyName(string? name)
    {
        var resolver = CreateResolver(("openai", Dummy));
        Assert.Throws<InvalidOperationException>(() => resolver.Resolve(name!));
    }

    [Fact]
    public void GetRegisteredProviders_ReturnsAllNames()
    {
        var resolver = CreateResolver(("openai", Dummy), ("gemini", Dummy), ("anthropic", Dummy));
        var providers = resolver.GetRegisteredProviders();

        Assert.Equal(3, providers.Count);
        Assert.Contains("openai", providers);
        Assert.Contains("gemini", providers);
        Assert.Contains("anthropic", providers);
    }

    [Fact]
    public void GetRegisteredProviders_ExcludesProvidersOutsideCatalog()
    {
        var resolver = CreateResolver(("custom", Dummy), ("openai", Dummy));

        Assert.Equal(["openai"], resolver.GetRegisteredProviders());
        Assert.Throws<InvalidOperationException>(() => resolver.Resolve("custom"));
    }

    private sealed class DummyProvider : IAiProvider
    {
        public Task<AiResponse> GetResponseAsync(AiRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new AiResponse { Decision = new AiDecision { Action = AiAction.NoAction } });
    }
}

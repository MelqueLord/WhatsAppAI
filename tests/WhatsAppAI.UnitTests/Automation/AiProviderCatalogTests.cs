using WhatsAppAI.Application.Automation.Policy;
using Xunit;

namespace WhatsAppAI.UnitTests.Automation;

public sealed class AiProviderCatalogTests
{
    [Fact]
    public void Catalog_ContainsEveryRegisteredProviderInStableOrder()
    {
        Assert.Equal(["openai", "gemini", "anthropic", "xiaomi", "grok", "groq"],
            AiProviderCatalog.Providers.Select(provider => provider.Id));
    }

    [Theory]
    [InlineData("OpenAI", "gpt-4o-mini")]
    [InlineData("GEMINI", "models/gemini-3.6-flash")]
    [InlineData("groq", "openai/gpt-oss-120b")]
    public void IsModelAllowed_NormalizesProviderAndModel(string provider, string model)
        => Assert.True(AiProviderCatalog.IsModelAllowed(provider, model));

    [Fact]
    public void IsModelAllowed_RejectsUnknownProviderOrModel()
    {
        Assert.False(AiProviderCatalog.IsModelAllowed("unknown", "gpt-4o"));
        Assert.False(AiProviderCatalog.IsModelAllowed("openai", "gpt-5"));
    }
}

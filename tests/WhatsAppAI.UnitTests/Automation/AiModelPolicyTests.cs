using WhatsAppAI.Application.Automation.Policy;
using Xunit;

namespace WhatsAppAI.UnitTests.Automation;

public sealed class AiModelPolicyTests
{
    [Theory]
    [InlineData("openai", "gpt-4o-mini")]
    [InlineData("gemini", "models/gemini-3.6-flash")]
    public void IsAllowed_AcceptsCatalogModel(string provider, string modelId)
    {
        Assert.True(AiModelPolicy.IsAllowed(provider, modelId));
    }

    [Fact]
    public void IsAllowed_RejectsModelOutsideCatalog()
    {
        Assert.False(AiModelPolicy.IsAllowed("openai", "gpt-5"));
    }
}

using WhatsAppAI.Domain;
using WhatsAppAI.Domain.Integrations;

namespace WhatsAppAI.UnitTests.Integrations;

public sealed class AiProviderCredentialTests
{
    [Fact]
    public void UpdateInstructions_PersistsDistinctRoutingQueues()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var tagId = Guid.NewGuid();
        var credential = AiProviderCredential.Create(
            Guid.NewGuid(), "openai", "gpt-4o-mini", "secret-ref");

        credential.UpdateInstructions(
            "Atenda brevemente", 300, credential.Version, [first, second, first], [tagId, tagId]);

        Assert.Equal(new[] { first, second }.OrderBy(id => id), credential.GetRoutingQueueIds());
        Assert.Equal(new[] { tagId }, credential.GetRoutingTagIds());
    }

    [Fact]
    public void UpdateInstructions_WithStaleVersion_DoesNotOverwriteCurrentInstructions()
    {
        var credential = AiProviderCredential.Create(
            Guid.NewGuid(), "openai", "gpt-4o-mini", "secret-ref");
        credential.UpdateInstructions("Versão atual", 180, credential.Version);
        var currentVersion = credential.Version;

        Assert.Throws<ConcurrencyException>(() =>
            credential.UpdateInstructions("Versão obsoleta", 300, 0));

        Assert.Equal("Versão atual", credential.SystemPrompt);
        Assert.Equal(180, credential.MaxTokensPerResponse);
        Assert.Equal(currentVersion, credential.Version);
    }

    [Fact]
    public void UpdateInstructions_EnforcesCostEffectiveOutputTokenRange()
    {
        var credential = AiProviderCredential.Create(
            Guid.NewGuid(), "openai", "gpt-4o-mini", "secret-ref");

        credential.UpdateInstructions("Atenda brevemente", 1_000, credential.Version);

        Assert.Equal(240, credential.MaxTokensPerResponse);
    }
}

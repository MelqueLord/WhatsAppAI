using WhatsAppAI.Domain;
using WhatsAppAI.Domain.Integrations;

namespace WhatsAppAI.UnitTests.Integrations;

public sealed class AiProviderCredentialTests
{
    [Fact]
    public void Create_UsesTenantProjectScopeByDefault()
    {
        var credential = AiProviderCredential.Create(
            Guid.NewGuid(), "openai", "gpt-4o-mini", "ai:tenant:openai:apikey");

        Assert.Equal(AiCredentialScopes.TenantProject, credential.CredentialScope);
    }

    [Fact]
    public void Create_AllowsSharedPlatformScope()
    {
        var credential = AiProviderCredential.Create(
            Guid.NewGuid(), "openai", "gpt-4o-mini", "ai:platform:openai:apikey", AiCredentialScopes.SharedPlatform);

        Assert.Equal(AiCredentialScopes.SharedPlatform, credential.CredentialScope);
    }

    [Fact]
    public void Create_RejectsUnsupportedScope()
    {
        Assert.Throws<ArgumentException>(() => AiProviderCredential.Create(
            Guid.NewGuid(), "openai", "gpt-4o-mini", "secret-ref", "External"));
    }

    [Fact]
    public void UpdateInstructions_PersistsDistinctDraftRoutingQueues()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var tagId = Guid.NewGuid();
        var credential = AiProviderCredential.Create(
            Guid.NewGuid(), "openai", "gpt-4o-mini", "secret-ref");

        credential.UpdateInstructions(
            "Atenda brevemente", 300, credential.Version, [first, second, first], [tagId, tagId]);

        Assert.Equal(new[] { first, second }.OrderBy(id => id), credential.GetDraftRoutingQueueIds());
        Assert.Equal(new[] { tagId }, credential.GetDraftRoutingTagIds());
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

        Assert.Equal("Versão atual", credential.DraftSystemPrompt);
        Assert.Equal(180, credential.DraftMaxTokensPerResponse);
        Assert.Equal(currentVersion, credential.Version);
    }

    [Fact]
    public void UpdateInstructions_EnforcesCostEffectiveOutputTokenRange()
    {
        var credential = AiProviderCredential.Create(
            Guid.NewGuid(), "openai", "gpt-4o-mini", "secret-ref");

        credential.UpdateInstructions("Atenda brevemente", 1_000, credential.Version);

        Assert.Equal(240, credential.DraftMaxTokensPerResponse);
    }

    [Fact]
    public void PublishDraft_MakesOnlyTheDraftLive()
    {
        var credential = AiProviderCredential.Create(Guid.NewGuid(), "openai", "gpt-4o-mini", "secret-ref");
        credential.UpdateInstructions("Versão de teste", 180, credential.Version);
        credential.UpdateDraftConfidenceThreshold(0.8);

        Assert.Null(credential.SystemPrompt);
        credential.PublishDraft(credential.Version);

        Assert.Equal("Versão de teste", credential.SystemPrompt);
        Assert.Equal(credential.DraftVersion, credential.PublishedVersion);
        Assert.True(credential.PublishedAt.HasValue);
    }

    [Fact]
    public void PublishDraft_WithStaleVersionDoesNotOverwriteLiveConfiguration()
    {
        var credential = AiProviderCredential.Create(Guid.NewGuid(), "openai", "gpt-4o-mini", "secret-ref");
        credential.UpdateInstructions("Rascunho", 180, credential.Version);
        var version = credential.Version;
        credential.UpdateInstructions("Outro rascunho", 180, credential.Version);

        Assert.Throws<ConcurrencyException>(() => credential.PublishDraft(version));
        Assert.Null(credential.SystemPrompt);
    }
}

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

        credential.UpdateInstructions("Atenda brevemente", 300, [first, second, first], [tagId, tagId]);

        Assert.Equal(new[] { first, second }.OrderBy(id => id), credential.GetRoutingQueueIds());
        Assert.Equal(new[] { tagId }, credential.GetRoutingTagIds());
    }
}

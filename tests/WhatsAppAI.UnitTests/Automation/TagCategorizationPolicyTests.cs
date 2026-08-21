using WhatsAppAI.Application.Automation.Policy;

namespace WhatsAppAI.UnitTests.Automation;

public sealed class TagCategorizationPolicyTests
{
    [Fact]
    public void ResolveAuthorizedTagIds_ReturnsOnlyConfiguredTagsWithoutDuplicates()
    {
        var vipId = Guid.NewGuid();
        var result = TagCategorizationPolicy.ResolveAuthorizedTagIds(
            ["VIP", "vip", "Não autorizada"],
            [new RoutingTagCandidate(vipId, "VIP")]);

        Assert.Equal(new[] { vipId }, result);
    }
}

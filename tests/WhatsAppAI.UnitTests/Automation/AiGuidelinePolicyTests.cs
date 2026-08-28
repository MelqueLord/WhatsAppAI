using WhatsAppAI.Application.Automation.Policy;

namespace WhatsAppAI.UnitTests.Automation;

public sealed class AiGuidelinePolicyTests
{
    [Fact]
    public void BuildSystemInstructions_ContainsStructuredBehaviorSecurityAndHandoffRules()
    {
        var instructions = AiGuidelinePolicy.BuildSystemInstructions();

        Assert.NotEmpty(AiGuidelinePolicy.Rules.Behavior);
        Assert.NotEmpty(AiGuidelinePolicy.Rules.Security);
        Assert.NotEmpty(AiGuidelinePolicy.Rules.Handoff);
        Assert.Contains("não invente", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("nunca revele", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("action \"handoff\"", instructions, StringComparison.Ordinal);

        foreach (var reason in BehaviorPolicy.RequiredHandoffReasons)
        {
            Assert.Contains(reason, instructions, StringComparison.Ordinal);
            Assert.Contains(AiGuidelinePolicy.Rules.Handoff, rule => rule.Code == reason);
        }
    }
}

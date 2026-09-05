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
        Assert.Contains("mantém a IA ativa", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("linguagem humana", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("não repita", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("não invente para soar simpático", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Segurança factual", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cada preço", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("quando selecionar uma fila autorizada", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(AiGuidelinePolicy.Rules.Behavior, rule => rule.Code == NaturalResponsePolicy.RuleCode);
        Assert.Contains(AiGuidelinePolicy.Rules.Security, rule => rule.Code == AiGroundingPolicy.RuleCode);

        foreach (var reason in BehaviorPolicy.RequiredHandoffReasons)
        {
            Assert.Contains(reason, instructions, StringComparison.Ordinal);
            Assert.Contains(AiGuidelinePolicy.Rules.Handoff, rule => rule.Code == reason);
        }
    }
}

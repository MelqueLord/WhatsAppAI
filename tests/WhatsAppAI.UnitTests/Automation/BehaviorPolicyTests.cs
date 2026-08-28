using WhatsAppAI.Application.Automation;
using WhatsAppAI.Application.Automation.Policy;

namespace WhatsAppAI.UnitTests.Automation;

public sealed class BehaviorPolicyTests
{
    [Theory]
    [InlineData(0.69, AiAction.Handoff)]
    [InlineData(0.70, AiAction.Reply)]
    [InlineData(0.71, AiAction.Reply)]
    public void SanitizeDecision_UsesConfiguredConfidenceThreshold(
        double confidence,
        AiAction expectedAction)
    {
        var decision = new AiDecision
        {
            Action = AiAction.Reply,
            Text = "Resposta válida",
            Confidence = confidence,
            HandoffReason = "low_confidence"
        };

        var result = BehaviorPolicy.SanitizeDecision(decision, 0.70);

        Assert.Equal(expectedAction, result.Action);
    }
}

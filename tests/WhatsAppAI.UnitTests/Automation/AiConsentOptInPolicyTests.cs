using WhatsAppAI.Application.Automation.Policy;

namespace WhatsAppAI.UnitTests.Automation;

public sealed class AiConsentOptInPolicyTests
{
    [Theory]
    [InlineData("SIM")]
    [InlineData(" sim ")]
    [InlineData("sIm")]
    public void IsAccepted_AcceptsOnlyTheExplicitOptInWord(string content)
    {
        Assert.True(AiConsentOptInPolicy.IsAccepted(content));
    }

    [Theory]
    [InlineData("")]
    [InlineData("sim, quero saber os horários")]
    [InlineData("não")]
    [InlineData("simulado")]
    public void IsAccepted_RejectsAmbiguousOrUnrelatedContent(string content)
    {
        Assert.False(AiConsentOptInPolicy.IsAccepted(content));
    }
}

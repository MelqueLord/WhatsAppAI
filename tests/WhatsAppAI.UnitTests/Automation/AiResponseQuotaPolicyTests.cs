using WhatsAppAI.Application.Automation.Policy;

namespace WhatsAppAI.UnitTests.Automation;

public sealed class AiResponseQuotaPolicyTests
{
    [Fact]
    public void HasAvailableResponse_BelowLimit_ReturnsTrue()
    {
        Assert.True(AiResponseQuotaPolicy.HasAvailableResponse(1_500, 1_499));
    }

    [Fact]
    public void HasAvailableResponse_AtLimit_ReturnsFalse()
    {
        Assert.False(AiResponseQuotaPolicy.HasAvailableResponse(1_500, 1_500));
    }

    [Fact]
    public void HasAvailableResponse_AboveLimit_ReturnsFalse()
    {
        Assert.False(AiResponseQuotaPolicy.HasAvailableResponse(1_500, 1_501));
    }

    [Fact]
    public void HasAvailableResponse_LegacyUnlimited_ReturnsTrue()
    {
        Assert.True(AiResponseQuotaPolicy.HasAvailableResponse(null, long.MaxValue));
    }
}

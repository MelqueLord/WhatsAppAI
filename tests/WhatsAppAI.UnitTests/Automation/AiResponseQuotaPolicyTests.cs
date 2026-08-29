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

    [Theory]
    [InlineData(1_500, 0, 1_500)]
    [InlineData(1_500, 500, 2_000)]
    [InlineData(5_000, 1_000, 6_000)]
    public void EffectiveLimitAddsCurrentMonthTopUps(int baseLimit, long topUps, int expected)
    {
        Assert.Equal(expected, AiResponseQuotaPolicy.GetEffectiveMonthlyLimit(baseLimit, topUps));
    }

    [Fact]
    public void UnlimitedQuotaRemainsUnlimitedAfterTopUp()
    {
        Assert.Null(AiResponseQuotaPolicy.GetEffectiveMonthlyLimit(null, 500));
    }
}

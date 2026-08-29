using WhatsAppAI.Application.Automation.Policy;

namespace WhatsAppAI.UnitTests.Automation;

public sealed class AiQuotaAlertPolicyTests
{
    [Theory]
    [InlineData(1500, 1199)]
    [InlineData(1500, 0)]
    public void Below_warning_threshold_has_no_alert(int limit, long used)
    {
        Assert.Null(AiQuotaAlertPolicy.GetLevel(limit, used));
    }

    [Fact]
    public void At_warning_threshold_returns_warning()
    {
        Assert.Equal(AiQuotaAlertLevel.Warning, AiQuotaAlertPolicy.GetLevel(1500, 1200));
    }

    [Fact]
    public void At_limit_returns_exhausted()
    {
        Assert.Equal(AiQuotaAlertLevel.Exhausted, AiQuotaAlertPolicy.GetLevel(1500, 1500));
    }

    [Fact]
    public void Unlimited_tenant_has_no_alert()
    {
        Assert.Null(AiQuotaAlertPolicy.GetLevel(null, 100_000));
    }

    [Theory]
    [InlineData(1500, 100, AiQuotaStatus.Normal)]
    [InlineData(1500, 1200, AiQuotaStatus.Warning)]
    [InlineData(1500, 1500, AiQuotaStatus.Exhausted)]
    [InlineData(null, 100_000, AiQuotaStatus.Unlimited)]
    public void Status_is_the_single_source_for_consumption_state(
        int? limit,
        long used,
        AiQuotaStatus expected)
    {
        Assert.Equal(expected, AiQuotaAlertPolicy.GetStatus(limit, used));
    }
}

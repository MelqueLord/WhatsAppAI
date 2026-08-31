using WhatsAppAI.Application.Automation.Policy;
using Xunit;

namespace WhatsAppAI.UnitTests.Automation;

public sealed class AiBudgetPolicyTests
{
    [Fact]
    public void HasAvailableBudget_RejectsEstimateThatExceedsReservedCapacity()
    {
        Assert.False(AiBudgetPolicy.HasAvailableBudget(1_000, 400, 500, 101));
    }

    [Fact]
    public void HasAvailableBudget_AllowsUnlimitedTenant()
    {
        Assert.True(AiBudgetPolicy.HasAvailableBudget(null, 20_000, 50_000, 1_000));
    }

    [Fact]
    public void EstimateInputTokensIncludesSystemAndOutputBudget()
    {
        Assert.Equal(124, AiBudgetPolicy.EstimateInputTokens(["1234567890"], "123456", 120));
    }

    [Fact]
    public void HasAvailableBudget_RejectsEstimatedUsageAboveLimit()
    {
        Assert.False(AiBudgetPolicy.HasAvailableBudget(1_000, 900, 101));
    }

    [Fact]
    public void HasAvailableBudget_AcceptsUsageWithinLimit()
    {
        Assert.True(AiBudgetPolicy.HasAvailableBudget(1_000, 900, 100));
    }
}

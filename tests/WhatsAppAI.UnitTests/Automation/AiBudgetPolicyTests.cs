using WhatsAppAI.Application.Automation.Policy;
using Xunit;

namespace WhatsAppAI.UnitTests.Automation;

public sealed class AiBudgetPolicyTests
{
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

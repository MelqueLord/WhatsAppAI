using WhatsAppAI.Application.Automation.Policy;

namespace WhatsAppAI.UnitTests.Automation;

public sealed class PlanQuotaPolicyTests
{
    [Fact]
    public void Custom_limit_is_preserved_when_plan_changes()
    {
        Assert.Equal(6500, PlanQuotaPolicy.ResolveMonthlyAiResponseLimit(6500, 1500, 5000));
    }

    [Fact]
    public void Default_limit_moves_with_the_new_plan()
    {
        Assert.Equal(5000, PlanQuotaPolicy.ResolveMonthlyAiResponseLimit(1500, 1500, 5000));
    }

    [Fact]
    public void Legacy_unlimited_tenant_receives_new_plan_default()
    {
        Assert.Equal(1500, PlanQuotaPolicy.ResolveMonthlyAiResponseLimit(null, null, 1500));
    }
}

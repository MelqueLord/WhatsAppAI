using WhatsAppAI.Domain.Identity;
using Xunit;

namespace WhatsAppAI.UnitTests.Identity;

public class TenantTests
{
    [Fact]
    public void Create_SetsPlanId()
    {
        var planId = Guid.NewGuid();
        var tenant = Tenant.Create("Test", "test", planId);

        Assert.Equal(planId, tenant.PlanId);
    }

    [Fact]
    public void ChangePlan_UpdatesPlanId()
    {
        var originalPlanId = Guid.NewGuid();
        var newPlanId = Guid.NewGuid();
        var tenant = Tenant.Create("Test", "test", originalPlanId);

        tenant.ChangePlan(newPlanId);

        Assert.Equal(newPlanId, tenant.PlanId);
    }

    [Fact]
    public void ChangePlan_IncrementsVersion()
    {
        var planId = Guid.NewGuid();
        var tenant = Tenant.Create("Test", "test", planId);
        var originalVersion = tenant.Version;

        tenant.ChangePlan(Guid.NewGuid());

        Assert.Equal(originalVersion + 1, tenant.Version);
    }

    [Fact]
    public void Activate_SetsStatusToActive()
    {
        var tenant = Tenant.Create("Test", "test", Guid.NewGuid());
        tenant.Activate();

        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.NotNull(tenant.ActivatedAt);
    }

    [Fact]
    public void Suspend_SetsStatusToSuspended()
    {
        var tenant = Tenant.Create("Test", "test", Guid.NewGuid());
        tenant.Activate();
        tenant.Suspend("Test reason");

        Assert.Equal(TenantStatus.Suspended, tenant.Status);
        Assert.Equal("Test reason", tenant.SuspensionReason);
    }

    [Fact]
    public void Reactivate_SetsStatusToActive()
    {
        var tenant = Tenant.Create("Test", "test", Guid.NewGuid());
        tenant.Activate();
        tenant.Suspend("Test reason");
        tenant.Reactivate();

        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.Null(tenant.SuspensionReason);
    }

    [Fact]
    public void Create_SetsStatusToPending()
    {
        var tenant = Tenant.Create("Test", "test", Guid.NewGuid());

        Assert.Equal(TenantStatus.Pending, tenant.Status);
    }

    [Fact]
    public void Create_TrimsNameAndSlug()
    {
        var tenant = Tenant.Create("  Test  ", "  test-slug  ", Guid.NewGuid());

        Assert.Equal("Test", tenant.Name);
        Assert.Equal("test-slug", tenant.Slug);
    }
}

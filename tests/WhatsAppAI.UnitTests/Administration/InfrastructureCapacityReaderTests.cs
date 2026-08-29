using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Identity;
using WhatsAppAI.Domain.Integrations;
using WhatsAppAI.Infrastructure.Administration;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.UnitTests.Administration;

public sealed class InfrastructureCapacityReaderTests
{
    [Fact]
    public async Task GetCountsAsync_CountsHostedActiveResourcesAndExcludesClosedTenant()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        await using var context = new AppDbContext(options, new EmptyTenantContext());
        await context.Database.OpenConnectionAsync();
        await context.Database.EnsureCreatedAsync();

        var plan = SubscriptionPlan.CreateBot();
        var activeTenant = Tenant.Create("Active", "active", plan.Id);
        activeTenant.Activate();
        var suspendedTenant = Tenant.Create("Suspended", "suspended", plan.Id);
        suspendedTenant.Activate();
        suspendedTenant.Suspend("billing");
        var closedTenant = Tenant.Create("Closed", "closed", plan.Id);
        closedTenant.Close();
        context.AddRange(plan, activeTenant, suspendedTenant, closedTenant);

        context.WhatsAppAccounts.AddRange(
            WhatsAppAccount.Create(activeTenant.Id, "waba-1", "phone-1", "secret-1"),
            WhatsAppAccount.Create(suspendedTenant.Id, "waba-2", "phone-2", "secret-2"),
            WhatsAppAccount.Create(closedTenant.Id, "waba-3", "phone-3", "secret-3"));

        var activeOperator = TenantMembership.Create(
            activeTenant.Id,
            User.Create("active-operator@example.com"),
            MembershipRole.Operator);
        activeOperator.Activate();
        var inactiveOperator = TenantMembership.Create(
            activeTenant.Id,
            User.Create("inactive-operator@example.com"),
            MembershipRole.Operator);
        inactiveOperator.Activate();
        inactiveOperator.Deactivate();
        var closedOperator = TenantMembership.Create(
            closedTenant.Id,
            User.Create("closed-operator@example.com"),
            MembershipRole.Operator);
        closedOperator.Activate();
        context.TenantMemberships.AddRange(activeOperator, inactiveOperator, closedOperator);

        await context.SaveChangesAsync();

        var result = await new InfrastructureCapacityReader(context).GetCountsAsync();

        Assert.Equal(2, result.Customers);
        Assert.Equal(2, result.Lines);
        Assert.Equal(1, result.Operators);
    }

    private sealed class EmptyTenantContext : ICurrentTenant
    {
        public Guid? TenantId => null;
        public Guid? UserId => null;
        public string? UserRole => null;
        public bool IsPlatformAdmin => false;
        public bool IsAuthenticated => false;
        public SupportSessionInfo? SupportSession => null;
        public void SetContext(Guid? tenantId, Guid userId, string role, bool isPlatformAdmin) { }
        public void EnterSupportSession(Guid tenantId, string reason) { }
        public void ExitSupportSession() { }
        public void Clear() { }
    }
}

using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Identity;
using WhatsAppAI.Domain.Messaging;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.UnitTests.Identity;

public sealed class TenantMembershipConfigurationTests
{
    [Fact]
    public void UserId_IndexIsGloballyUnique()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var context = new AppDbContext(options, new EmptyTenantContext());
        var membership = context.Model.FindEntityType(typeof(TenantMembership));
        var userIndex = membership!.GetIndexes()
            .Single(index => index.Properties.Count == 1 && index.Properties[0].Name == nameof(TenantMembership.UserId));

        Assert.True(userIndex.IsUnique);
    }

    [Fact]
    public async Task Operator_CannotBelongToTwoTenants()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        await using var context = new AppDbContext(options, new EmptyTenantContext());
        await context.Database.OpenConnectionAsync();
        await context.Database.EnsureCreatedAsync();

        var planId = Guid.NewGuid();
        var firstTenant = Tenant.Create("First company", "first-company", planId);
        var secondTenant = Tenant.Create("Second company", "second-company", planId);
        var user = User.Create("operator@example.com");
        context.AddRange(firstTenant, secondTenant, user);
        context.TenantMemberships.Add(TenantMembership.Create(firstTenant.Id, user, MembershipRole.Operator));
        context.TenantMemberships.Add(TenantMembership.Create(secondTenant.Id, user, MembershipRole.Operator));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public void AssignedQueue_UsesNullableRestrictedForeignKey()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var context = new AppDbContext(options, new EmptyTenantContext());
        var membership = context.Model.FindEntityType(typeof(TenantMembership))!;
        var property = membership.FindProperty(nameof(TenantMembership.AssignedQueueId))!;
        var foreignKey = membership.GetForeignKeys()
            .Single(key => key.Properties.Contains(property));

        Assert.True(property.IsNullable);
        Assert.Equal(typeof(ServiceLine), foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
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

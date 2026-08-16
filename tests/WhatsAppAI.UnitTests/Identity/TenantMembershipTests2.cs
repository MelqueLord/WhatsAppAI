using WhatsAppAI.Domain.Identity;
using Xunit;

namespace WhatsAppAI.UnitTests.Identity;

public class TenantMembershipTestsExtended
{
    private static User CreateTestUser(bool isPlatformAdmin = false)
    {
        var user = User.Create("test@example.com");
        user.Activate("hashed-password");
        if (isPlatformAdmin) user.GrantPlatformAdmin();
        return user;
    }

    [Fact]
    public void Create_SetsStatusPending()
    {
        var user = CreateTestUser();
        var membership = TenantMembership.Create(Guid.NewGuid(), user, MembershipRole.Operator);

        Assert.Equal(MembershipStatus.Pending, membership.Status);
        Assert.Equal(MembershipRole.Operator, membership.Role);
    }

    [Fact]
    public void Create_SetsTenantAndUserId()
    {
        var tenantId = Guid.NewGuid();
        var user = CreateTestUser();
        var membership = TenantMembership.Create(tenantId, user, MembershipRole.TenantOwner);

        Assert.Equal(tenantId, membership.TenantId);
        Assert.Equal(user.Id, membership.UserId);
    }

    [Fact]
    public void Create_ThrowsForPlatformAdmin()
    {
        var user = CreateTestUser(isPlatformAdmin: true);

        Assert.Throws<InvalidOperationException>(() =>
            TenantMembership.Create(Guid.NewGuid(), user, MembershipRole.Operator));
    }

    [Fact]
    public void Create_ThrowsForNullUser()
    {
        Assert.Throws<ArgumentNullException>(() =>
            TenantMembership.Create(Guid.NewGuid(), null!, MembershipRole.Operator));
    }

    [Fact]
    public void Activate_SetsStatusActive()
    {
        var user = CreateTestUser();
        var membership = TenantMembership.Create(Guid.NewGuid(), user, MembershipRole.Operator);
        membership.Activate();

        Assert.Equal(MembershipStatus.Active, membership.Status);
    }

    [Fact]
    public void Activate_IncrementsVersion()
    {
        var user = CreateTestUser();
        var membership = TenantMembership.Create(Guid.NewGuid(), user, MembershipRole.Operator);
        membership.Activate();

        Assert.Equal(1u, membership.Version);
    }

    [Fact]
    public void Activate_ThrowsWhenAlreadyActive()
    {
        var user = CreateTestUser();
        var membership = TenantMembership.Create(Guid.NewGuid(), user, MembershipRole.Operator);
        membership.Activate();

        Assert.Throws<InvalidOperationException>(() => membership.Activate());
    }

    [Fact]
    public void Deactivate_SetsStatusInactive()
    {
        var user = CreateTestUser();
        var membership = TenantMembership.Create(Guid.NewGuid(), user, MembershipRole.Operator);
        membership.Activate();
        membership.Deactivate();

        Assert.Equal(MembershipStatus.Inactive, membership.Status);
        Assert.NotNull(membership.DeactivatedAt);
    }

    [Fact]
    public void Deactivate_ThrowsWhenAlreadyInactive()
    {
        var user = CreateTestUser();
        var membership = TenantMembership.Create(Guid.NewGuid(), user, MembershipRole.Operator);
        membership.Activate();
        membership.Deactivate();

        Assert.Throws<InvalidOperationException>(() => membership.Deactivate());
    }

    [Fact]
    public void Reactivate_SetsStatusActive()
    {
        var user = CreateTestUser();
        var membership = TenantMembership.Create(Guid.NewGuid(), user, MembershipRole.Operator);
        membership.Activate();
        membership.Deactivate();
        membership.Reactivate();

        Assert.Equal(MembershipStatus.Active, membership.Status);
        Assert.NotNull(membership.ReactivatedAt);
    }

    [Fact]
    public void Reactivate_ThrowsWhenNotInactive()
    {
        var user = CreateTestUser();
        var membership = TenantMembership.Create(Guid.NewGuid(), user, MembershipRole.Operator);
        membership.Activate();

        Assert.Throws<InvalidOperationException>(() => membership.Reactivate());
    }
}

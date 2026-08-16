using WhatsAppAI.Domain.Identity;

namespace WhatsAppAI.UnitTests.Identity;

public sealed class TenantMembershipTests
{
    [Fact]
    public void Create_RejectsPlatformAdmin()
    {
        var admin = User.Create("admin@example.com");
        admin.GrantPlatformAdmin();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            TenantMembership.Create(Guid.NewGuid(), admin, MembershipRole.TenantOwner));

        Assert.Equal("Platform administrators cannot belong to a tenant.", exception.Message);
    }

    [Fact]
    public void TenantOwner_RoleUsesContractName()
    {
        Assert.Equal("TenantOwner", MembershipRole.TenantOwner.ToString());
    }
}

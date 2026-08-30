using System.Security.Claims;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Identity;
using WhatsAppAI.Infrastructure.Identity;

namespace WhatsAppAI.UnitTests.Identity;

public sealed class AuthenticationServiceTests
{
    [Fact]
    public async Task ValidatePrincipalAsync_RejectsDeactivatedUserCookie()
    {
        var user = User.CreateWithTemporaryPassword("admin@test.com", "hash");
        user.GrantPlatformAdmin();
        var securityStamp = user.SecurityStamp;
        user.Deactivate();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim("security_stamp", securityStamp),
            new Claim("platform_admin", "true")
        ], "Cookies"));

        var service = new AuthenticationService(
            new StubUserRepository(user),
            new StubMembershipRepository());

        Assert.False(await service.ValidatePrincipalAsync(principal));
    }

    [Fact]
    public async Task ValidatePrincipalAsync_AcceptsActiveTenantCookieBeforeTenantContextIsSet()
    {
        var user = User.CreateWithTemporaryPassword("owner@test.com", "hash");
        var tenant = Tenant.Create("Tenant", "tenant", Guid.NewGuid());
        var membership = TenantMembership.Create(tenant.Id, user, MembershipRole.TenantOwner);
        membership.Activate();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim("security_stamp", user.SecurityStamp),
            new Claim("tenant_id", tenant.Id.ToString()),
            new Claim("membership_id", membership.Id.ToString()),
            new Claim(ClaimTypes.Role, MembershipRole.TenantOwner.ToString())
        ], "Cookies"));

        var service = new AuthenticationService(
            new StubUserRepository(user),
            new StubMembershipRepository(membership));

        Assert.True(await service.ValidatePrincipalAsync(principal));
    }

    private sealed class StubUserRepository(User user) : IUserRepository
    {
        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(id == user.Id ? user : null);

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(null);

        public Task AddAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubMembershipRepository(TenantMembership? membership = null) : ITenantMembershipRepository
    {
        public Task<TenantMembership?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<TenantMembership?>(null);

        public Task<TenantMembership?> GetByUserAndTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(membership is not null && membership.UserId == userId && membership.TenantId == tenantId
                ? membership
                : null);

        public Task<IReadOnlyList<TenantMembership>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TenantMembership>>([]);

        public Task<IReadOnlyList<TenantMembership>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TenantMembership>>([]);

        public Task AddAsync(TenantMembership membership, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(TenantMembership membership, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}

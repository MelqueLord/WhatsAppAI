using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Identity;

namespace WhatsAppAI.Infrastructure.Identity;

public interface IAuthenticationService
{
    Task SignInAsync(HttpContext httpContext, User user, TenantMembership? membership,
        bool isPlatformAdmin = false,
        Guid? supportTenantId = null, string? supportReason = null);
    Task SignOutAsync(HttpContext httpContext);
    Task<ClaimsPrincipal?> ValidateAsync(HttpContext httpContext);
    Task<bool> ValidatePrincipalAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);
}

internal sealed class AuthenticationService(
    IUserRepository userRepository,
    ITenantMembershipRepository membershipRepository) : IAuthenticationService
{
    public async Task SignInAsync(HttpContext httpContext, User user, TenantMembership? membership,
        bool isPlatformAdmin = false,
        Guid? supportTenantId = null, string? supportReason = null)
    {
        if (isPlatformAdmin && membership is not null)
            throw new InvalidOperationException("Platform administrators cannot sign in with a tenant membership.");

        if (!isPlatformAdmin && membership is null)
            throw new InvalidOperationException("Tenant users must sign in with a membership.");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new("security_stamp", user.SecurityStamp)
        };

        if (isPlatformAdmin)
        {
            claims.Add(new Claim("platform_admin", "true"));
            claims.Add(new Claim(ClaimTypes.Role, "PlatformAdmin"));

            if (supportTenantId.HasValue && supportReason is not null)
            {
                claims.Add(new Claim("support_tenant_id", supportTenantId.Value.ToString()));
                claims.Add(new Claim("support_reason", supportReason));
            }
        }
        else
        {
            claims.Add(new Claim("tenant_id", membership!.TenantId.ToString()));
            claims.Add(new Claim("membership_id", membership.Id.ToString()));
            claims.Add(new Claim(ClaimTypes.Role, membership.Role.ToString()));
        }

        if (user.DisplayName is not null)
        {
            claims.Add(new(ClaimTypes.Name, user.DisplayName));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30)
            });

        user.RecordLogin();
        await userRepository.UpdateAsync(user);
    }

    public async Task SignOutAsync(HttpContext httpContext)
    {
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    public async Task<ClaimsPrincipal?> ValidateAsync(HttpContext httpContext)
    {
        var authenticateResult = await httpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        if (!authenticateResult.Succeeded || authenticateResult.Principal is null)
            return null;

        return await ValidatePrincipalAsync(authenticateResult.Principal, httpContext.RequestAborted)
            ? authenticateResult.Principal
            : null;
    }

    public async Task<bool> ValidatePrincipalAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var securityStamp = principal.FindFirstValue("security_stamp");

        if (!Guid.TryParse(userIdValue, out var userId) || string.IsNullOrWhiteSpace(securityStamp))
            return false;

        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive)
            return false;

        if (user.SecurityStamp != securityStamp)
            return false;

        var isPlatformAdmin = principal.HasClaim("platform_admin", "true");
        if (isPlatformAdmin != user.IsPlatformAdmin)
            return false;

        if (isPlatformAdmin)
            return true;

        var membershipIdValue = principal.FindFirstValue("membership_id");
        var tenantIdValue = principal.FindFirstValue("tenant_id");
        if (!Guid.TryParse(membershipIdValue, out var membershipId) ||
            !Guid.TryParse(tenantIdValue, out var tenantId))
            return false;

        // Cookie validation runs before CurrentTenantMiddleware establishes the
        // tenant context, so avoid the tenant-scoped query filter here.
        var membership = await membershipRepository.GetByUserAndTenantAsync(
            userId,
            tenantId,
            cancellationToken);
        return membership is not null &&
            membership.Id == membershipId &&
            membership.Status == MembershipStatus.Active &&
            membership.UserId == userId &&
            membership.TenantId == tenantId;
    }

}

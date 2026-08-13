using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Identity;

namespace WhatsAppAI.Infrastructure.Identity;

public interface IAuthenticationService
{
    Task SignInAsync(HttpContext httpContext, User user, TenantMembership membership, bool isPlatformAdmin = false);
    Task SignOutAsync(HttpContext httpContext);
    Task<ClaimsPrincipal?> ValidateAsync(HttpContext httpContext);
}

internal sealed class AuthenticationService(
    IUserRepository userRepository,
    ITenantMembershipRepository membershipRepository) : IAuthenticationService
{
    public async Task SignInAsync(HttpContext httpContext, User user, TenantMembership membership, bool isPlatformAdmin = false)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new("security_stamp", user.SecurityStamp),
            new("tenant_id", membership.TenantId.ToString()),
            new("membership_id", membership.Id.ToString()),
            new(ClaimTypes.Role, membership.Role.ToString())
        };

        if (isPlatformAdmin)
        {
            claims.Add(new Claim("platform_admin", "true"));
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

        var userId = authenticateResult.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var securityStamp = authenticateResult.Principal.FindFirstValue("security_stamp");

        if (userId is null || securityStamp is null)
            return null;

        var user = await userRepository.GetByIdAsync(Guid.Parse(userId));
        if (user is null || !user.IsActive)
            return null;

        if (user.SecurityStamp != securityStamp)
            return null;

        var membershipId = authenticateResult.Principal.FindFirstValue("membership_id");
        if (membershipId is not null)
        {
            var membership = await membershipRepository.GetByIdAsync(Guid.Parse(membershipId));
            if (membership is null || membership.Status != MembershipStatus.Active)
                return null;
        }

        return authenticateResult.Principal;
    }
}

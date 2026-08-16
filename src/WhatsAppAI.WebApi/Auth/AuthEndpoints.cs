using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Identity;
using WhatsAppAI.Infrastructure.Identity;

namespace WhatsAppAI.WebApi.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/login", LoginAsync)
            .WithName("Login")
            .AllowAnonymous()
            ;

        group.MapPost("/logout", LogoutAsync)
            .WithName("Logout")
            ;

        group.MapGet("/me", GetCurrentUserAsync)
            .WithName("GetCurrentUser");

        group.MapGet("/access-denied", () => Results.Unauthorized())
            .WithName("AccessDenied")
            .AllowAnonymous();

        return app;
    }

    private static async Task<IResult> LoginAsync(
        [FromBody] LoginRequest request,
        IUserRepository userRepository,
        ITenantMembershipRepository membershipRepository,
        IAuthenticationService authenticationService,
        HttpContext httpContext)
    {
        var user = await userRepository.GetByEmailAsync(request.Email);
        if (user is null || !user.IsActive)
            return Results.Unauthorized();

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Results.Unauthorized();

        var memberships = await membershipRepository.GetByUserAsync(user.Id);
        if (user.IsPlatformAdmin)
        {
            if (memberships.Count != 0)
                return Results.Unauthorized();

            await authenticationService.SignInAsync(httpContext, user, null, true);

            return Results.Ok(new UserResponse
            {
                Id = user.Id,
                Email = user.Email,
                DisplayName = user.DisplayName,
                TenantId = null,
                Role = "PlatformAdmin",
                IsPlatformAdmin = true
            });
        }

        var activeMemberships = memberships
            .Where(m => m.Status == MembershipStatus.Active)
            .ToArray();

        if (memberships.Count > 1 || activeMemberships.Length != 1)
            return Results.Unauthorized();

        var activeMembership = activeMemberships[0];
        await authenticationService.SignInAsync(httpContext, user, activeMembership);

        return Results.Ok(new UserResponse
        {
            Id = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            TenantId = activeMembership.TenantId,
            Role = activeMembership.Role.ToString(),
            IsPlatformAdmin = user.IsPlatformAdmin
        });
    }

    private static async Task<IResult> LogoutAsync(
        IAuthenticationService authenticationService,
        HttpContext httpContext)
    {
        await authenticationService.SignOutAsync(httpContext);
        return Results.Ok();
    }

    private static async Task<IResult> GetCurrentUserAsync(
        HttpContext httpContext,
        IUserRepository userRepository,
        ITenantMembershipRepository membershipRepository,
        ICurrentTenant currentTenant)
    {
        if (!currentTenant.IsAuthenticated || currentTenant.UserId is null)
            return Results.Unauthorized();

        var user = await userRepository.GetByIdAsync(currentTenant.UserId.Value);
        if (user is null)
            return Results.Unauthorized();

        return Results.Ok(new UserResponse
        {
            Id = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            TenantId = currentTenant.TenantId,
            Role = currentTenant.UserRole,
            IsPlatformAdmin = currentTenant.IsPlatformAdmin
        });
    }
}

public sealed class LoginRequest
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

public sealed class UserResponse
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public Guid? TenantId { get; init; }
    public string? Role { get; init; }
    public bool IsPlatformAdmin { get; init; }
}

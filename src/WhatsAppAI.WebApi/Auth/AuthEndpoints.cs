using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Identity;
using WhatsAppAI.Infrastructure.Identity;
using WhatsAppAI.Infrastructure.Persistence;
using WhatsAppAI.WebApi.Operators;

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

        group.MapPost("/change-password", ChangePasswordAsync)
            .WithName("ChangePassword");

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
        IJwtTokenService jwtTokenService,
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
            var token = jwtTokenService.Generate(user, null, isPlatformAdmin: true);

            return Results.Ok(new LoginResponse
            {
                Token = token,
                User = new UserResponse
                {
                    Id = user.Id,
                    Email = user.Email,
                    DisplayName = user.DisplayName,
                    TenantId = null,
                    Role = "PlatformAdmin",
                    IsPlatformAdmin = true,
                    MustChangePassword = user.MustChangePassword
                }
            });
        }

        var activeMemberships = memberships
            .Where(m => m.Status == MembershipStatus.Active)
            .ToArray();

        if (memberships.Count > 1 || activeMemberships.Length != 1)
            return Results.Unauthorized();

        var activeMembership = activeMemberships[0];
        await authenticationService.SignInAsync(httpContext, user, activeMembership);
        var memberToken = jwtTokenService.Generate(user, activeMembership);

        return Results.Ok(new LoginResponse
        {
            Token = memberToken,
            User = new UserResponse
            {
                Id = user.Id,
                Email = user.Email,
                DisplayName = user.DisplayName,
                TenantId = activeMembership.TenantId,
                Role = activeMembership.Role.ToString(),
                IsPlatformAdmin = user.IsPlatformAdmin,
                MustChangePassword = user.MustChangePassword
            }
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
        ICurrentTenant currentTenant,
        AppDbContext dbContext)
    {
        if (!currentTenant.IsAuthenticated || currentTenant.UserId is null)
            return Results.Unauthorized();

        var user = await userRepository.GetByIdAsync(currentTenant.UserId.Value);
        if (user is null)
            return Results.Unauthorized();

        string? planCode = null;
        bool? aiEnabled = null;
        int? officialApiLineCount = null;
        int? qrCodeLineCount = null;
        int? operatorLimit = null;
        DateTime? dueDate = null;
        string? tenantStatus = null;
        string? assignedConnectionType = null;
        int? assignedLineNumber = null;
        List<LineAssignmentResponse> assignedLines = [];
        if (currentTenant.TenantId is not null)
        {
            var membership = await membershipRepository.GetByUserAndTenantAsync(
                user.Id,
                currentTenant.TenantId.Value);
            assignedConnectionType = membership?.AssignedConnectionType?.ToString();
            assignedLineNumber = membership?.AssignedLineNumber;
            if (membership is not null)
            {
                membership.LoadAssignedLinesFromJson();
                assignedLines = membership.AssignedLines
                    .Select(l => new LineAssignmentResponse
                    {
                        ConnectionType = l.ConnectionType.ToString(),
                        LineNumber = l.LineNumber
                    })
                    .ToList();
            }

            var tenant = await dbContext.Tenants.FindAsync(currentTenant.TenantId.Value);
            if (tenant is not null)
            {
            officialApiLineCount = tenant.OfficialApiLineCount;
            qrCodeLineCount = tenant.QrCodeLineCount;
                operatorLimit = tenant.OperatorLimit;
                dueDate = tenant.DueDate;
                tenantStatus = tenant.Status.ToString();
                var plan = await dbContext.SubscriptionPlans.FindAsync(tenant.PlanId);
                if (plan is not null)
                {
                    planCode = plan.Code;
                    aiEnabled = plan.AiEnabled;
                }
            }
        }

        return Results.Ok(new UserResponse
        {
            Id = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            TenantId = currentTenant.TenantId,
            Role = currentTenant.UserRole,
            IsPlatformAdmin = currentTenant.IsPlatformAdmin,
            MustChangePassword = user.MustChangePassword,
            PlanCode = planCode,
            AiEnabled = aiEnabled,
            OfficialApiLineCount = officialApiLineCount,
            QrCodeLineCount = qrCodeLineCount,
            OperatorLimit = operatorLimit,
            DueDate = dueDate,
            TenantStatus = tenantStatus,
            AssignedConnectionType = assignedConnectionType,
            AssignedLineNumber = assignedLineNumber,
            AssignedLines = assignedLines
        });
    }

    private static async Task<IResult> ChangePasswordAsync(
        [FromBody] ChangePasswordRequest request,
        ICurrentTenant currentTenant,
        IUserRepository userRepository,
        IAuthenticationService authenticationService,
        HttpContext httpContext)
    {
        if (!currentTenant.IsAuthenticated || currentTenant.UserId is null)
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            return Results.BadRequest(new { error = "Password must be at least 8 characters." });

        var user = await userRepository.GetByIdAsync(currentTenant.UserId.Value);
        if (user is null)
            return Results.Unauthorized();

        if (!string.IsNullOrEmpty(user.PasswordHash) && !BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            return Results.BadRequest(new { error = "Current password is incorrect." });

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.UpdatePassword(passwordHash);
        await userRepository.UpdateAsync(user);

        return Results.Ok(new { message = "Password changed successfully.", mustChangePassword = false });
    }
}

public sealed class LoginResponse
{
    public string Token { get; init; } = string.Empty;
    public UserResponse User { get; init; } = new();
}

public sealed class LoginRequest
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

public sealed class ChangePasswordRequest
{
    public string? CurrentPassword { get; init; }
    public string NewPassword { get; init; } = string.Empty;
}

public sealed class UserResponse
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public Guid? TenantId { get; init; }
    public string? Role { get; init; }
    public bool IsPlatformAdmin { get; init; }
    public bool MustChangePassword { get; init; }
    public string? PlanCode { get; init; }
    public bool? AiEnabled { get; init; }
    public int? OfficialApiLineCount { get; init; }
    public int? QrCodeLineCount { get; init; }
    public int? OperatorLimit { get; init; }
    public DateTime? DueDate { get; init; }
    public string? TenantStatus { get; init; }
    public string? AssignedConnectionType { get; init; }
    public int? AssignedLineNumber { get; init; }
    public List<LineAssignmentResponse> AssignedLines { get; init; } = [];
}

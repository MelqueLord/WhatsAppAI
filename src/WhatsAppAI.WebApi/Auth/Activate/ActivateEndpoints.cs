using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Identity;
using WhatsAppAI.Infrastructure.Identity;

namespace WhatsAppAI.WebApi.Auth.Activate;

public static class ActivateEndpoints
{
    public static IEndpointRouteBuilder MapActivateEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth/activate")
            .WithTags("Auth - Activation")
            .AllowAnonymous();

        group.MapPost("/", ActivateAccountAsync)
            .WithName("ActivateAccount")
            .RequireRateLimiting("auth");

        group.MapGet("/invitation/{invitationId:guid}", GetInvitationInfoAsync)
            .WithName("GetInvitationInfo");

        return app;
    }

    private static async Task<IResult> ActivateAccountAsync(
        [FromBody] ActivateRequest request,
        IInvitationRepository invitationRepository,
        IUserRepository userRepository,
        ITenantMembershipRepository membershipRepository,
        IAuthenticationService authenticationService,
        HttpContext httpContext)
    {
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            return Results.BadRequest(new { error = "Password must be at least 8 characters." });

        var invitation = await invitationRepository.GetByIdAsync(request.InvitationId);
        if (invitation is null)
            return Results.BadRequest(new { error = "Invalid invitation." });

        if (!invitation.IsUsable)
            return Results.BadRequest(new { error = "Invitation is not usable." });

        if (!BCrypt.Net.BCrypt.Verify(request.Token, invitation.TokenHash))
            return Results.BadRequest(new { error = "Invalid invitation." });

        var user = invitation.UserId is not null
            ? await userRepository.GetByIdAsync(invitation.UserId.Value)
            : await userRepository.GetByEmailAsync(invitation.Email);

        if (user is null)
            return Results.BadRequest(new { error = "User not found." });

        if (user.IsActive)
            return Results.BadRequest(new { error = "Account is already active." });

        var membership = await membershipRepository.GetByUserAndTenantAsync(user.Id, invitation.TenantId);
        if (membership is null)
        {
            var existingMemberships = await membershipRepository.GetByUserAsync(user.Id);
            if (existingMemberships.Count != 0)
                return Results.Conflict(new { error = "User already belongs to another tenant." });

            var role = invitation.Purpose == InvitationPurpose.TenantOwner
                ? MembershipRole.TenantOwner
                : MembershipRole.Operator;

            membership = TenantMembership.Create(invitation.TenantId, user, role);
            await membershipRepository.AddAsync(membership);
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        user.Activate(passwordHash);
        await userRepository.UpdateAsync(user);

        membership.Activate();
        await membershipRepository.UpdateAsync(membership);

        invitation.Consume();
        await invitationRepository.UpdateAsync(invitation);

        await authenticationService.SignInAsync(httpContext, user, membership);

        return Results.Ok(new ActivateResponse
        {
            UserId = user.Id,
            Email = user.Email,
            TenantId = invitation.TenantId,
            Role = membership.Role.ToString()
        });
    }

    private static async Task<IResult> GetInvitationInfoAsync(
        Guid invitationId,
        IInvitationRepository invitationRepository)
    {
        var invitation = await invitationRepository.GetByIdAsync(invitationId);
        if (invitation is null)
            return Results.NotFound();

        return Results.Ok(new InvitationInfoResponse
        {
            Id = invitation.Id,
            Email = invitation.Email,
            Purpose = invitation.Purpose.ToString(),
            IsUsable = invitation.IsUsable,
            ExpiresAt = invitation.ExpiresAt
        });
    }
}

public sealed class ActivateRequest
{
    public Guid InvitationId { get; init; }
    public string Token { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

public sealed class ActivateResponse
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public Guid TenantId { get; init; }
    public string Role { get; init; } = string.Empty;
}

public sealed class InvitationInfoResponse
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string Purpose { get; init; } = string.Empty;
    public bool IsUsable { get; init; }
    public DateTime ExpiresAt { get; init; }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Identity;
using WhatsAppAI.Infrastructure.Identity;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.WebApi.Operators;

public static class OperatorEndpoints
{
    public static IEndpointRouteBuilder MapOperatorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/operators")
            .WithTags("Operators")
            .RequireAuthorization("RequireTenantContext");

        group.MapGet("/", ListOperatorsAsync)
            .WithName("ListOperators");

        group.MapPost("/", InviteOperatorAsync)
            .WithName("InviteOperator")
            ;

        group.MapPost("/{operatorId:guid}/deactivate", DeactivateOperatorAsync)
            .WithName("DeactivateOperator")
            ;

        group.MapPost("/{operatorId:guid}/reactivate", ReactivateOperatorAsync)
            .WithName("ReactivateOperator")
            ;

        group.MapPost("/{operatorId:guid}/resend-invite", ResendInviteAsync)
            .WithName("ResendInvite")
            ;

        return app;
    }

    private static async Task<IResult> ListOperatorsAsync(
        ICurrentTenant currentTenant,
        ITenantMembershipRepository membershipRepository)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();

        if (currentTenant.UserRole != "TenantOwner")
            return Results.Forbid();

        var memberships = await membershipRepository.GetByTenantAsync(currentTenant.TenantId.Value);
        var operators = memberships
            .Where(m => m.Role == MembershipRole.Operator)
            .Select(m => new OperatorResponse
            {
                Id = m.Id,
                UserId = m.UserId,
                Email = m.User.Email,
                DisplayName = m.User.DisplayName,
                Status = m.Status.ToString(),
                CreatedAt = m.CreatedAt,
                DeactivatedAt = m.DeactivatedAt,
                ReactivatedAt = m.ReactivatedAt
            })
            .ToList();

        return Results.Ok(operators);
    }

    private static async Task<IResult> InviteOperatorAsync(
        [FromBody] InviteOperatorRequest request,
        ICurrentTenant currentTenant,
        AppDbContext dbContext)
    {
        if (currentTenant.TenantId is null || currentTenant.UserId is null)
            return Results.Unauthorized();

        if (currentTenant.UserRole != "TenantOwner")
            return Results.Forbid();

        var email = request.Email.Trim().ToLowerInvariant();

        var existingUser = await dbContext.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(user => user.Email == email);

        if (existingUser is not null)
        {
            if (existingUser.IsPlatformAdmin)
                return Results.Conflict(new { error = "User cannot be assigned to a tenant." });

            var alreadyBelongsToTenant = await dbContext.TenantMemberships
                .IgnoreQueryFilters()
                .AnyAsync(membership => membership.UserId == existingUser.Id);
            if (alreadyBelongsToTenant)
                return Results.Conflict(new { error = "User already belongs to a tenant." });
        }

        var hasPendingInvitation = await dbContext.Invitations
            .IgnoreQueryFilters()
            .AnyAsync(invitation => invitation.Email == email
                && invitation.Status == InvitationStatus.Pending
                && invitation.ExpiresAt > DateTime.UtcNow);
        if (hasPendingInvitation)
            return Results.Conflict(new { error = "User already has a pending tenant invitation." });

        var tokenBytes = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(tokenBytes);
        var token = Convert.ToBase64String(tokenBytes);
        var tokenHash = BCrypt.Net.BCrypt.HashPassword(token);

        var user = existingUser;
        if (user is null)
        {
            user = User.Create(email, request.DisplayName);
            dbContext.Users.Add(user);
        }

        var membership = TenantMembership.Create(
            currentTenant.TenantId.Value,
            user,
            MembershipRole.Operator);
        dbContext.TenantMemberships.Add(membership);

        var invitation = Invitation.Create(
            currentTenant.TenantId.Value,
            email,
            tokenHash,
            InvitationPurpose.Operator,
            currentTenant.UserId.Value,
            user.Id);

        dbContext.Invitations.Add(invitation);

        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Results.Conflict(new { error = "User cannot be assigned to another tenant." });
        }

        return Results.Created($"/api/operators/{membership.Id}", new InviteOperatorResponse
        {
            MembershipId = membership.Id,
            Email = email,
            ActivationUrl = $"/activate?token={token}&invitation={invitation.Id}",
            ExpiresAt = invitation.ExpiresAt
        });
    }

    private static async Task<IResult> DeactivateOperatorAsync(
        Guid operatorId,
        ICurrentTenant currentTenant,
        ITenantMembershipRepository membershipRepository,
        IUserRepository userRepository)
    {
        if (currentTenant.TenantId is null || currentTenant.UserId is null)
            return Results.Unauthorized();

        if (currentTenant.UserRole != "TenantOwner")
            return Results.Forbid();

        var membership = await membershipRepository.GetByIdAsync(operatorId);
        if (membership is null || membership.TenantId != currentTenant.TenantId)
            return Results.NotFound();

        if (membership.Role != MembershipRole.Operator)
            return Results.BadRequest(new { error = "Can only deactivate operators." });

        membership.Deactivate();
        await membershipRepository.UpdateAsync(membership);

        var user = await userRepository.GetByIdAsync(membership.UserId);
        if (user is not null)
        {
            user.Deactivate();
            await userRepository.UpdateAsync(user);
        }

        return Results.Ok(new OperatorResponse
        {
            Id = membership.Id,
            UserId = membership.UserId,
            Email = membership.User.Email,
            DisplayName = membership.User.DisplayName,
            Status = membership.Status.ToString(),
            DeactivatedAt = membership.DeactivatedAt
        });
    }

    private static async Task<IResult> ReactivateOperatorAsync(
        Guid operatorId,
        ICurrentTenant currentTenant,
        ITenantMembershipRepository membershipRepository,
        IUserRepository userRepository)
    {
        if (currentTenant.TenantId is null || currentTenant.UserId is null)
            return Results.Unauthorized();

        if (currentTenant.UserRole != "TenantOwner")
            return Results.Forbid();

        var membership = await membershipRepository.GetByIdAsync(operatorId);
        if (membership is null || membership.TenantId != currentTenant.TenantId)
            return Results.NotFound();

        if (membership.Role != MembershipRole.Operator)
            return Results.BadRequest(new { error = "Can only reactivate operators." });

        membership.Reactivate();
        await membershipRepository.UpdateAsync(membership);

        var user = await userRepository.GetByIdAsync(membership.UserId);
        if (user is not null && !user.IsActive)
        {
            user.Activate(user.PasswordHash ?? string.Empty);
            await userRepository.UpdateAsync(user);
        }

        return Results.Ok(new OperatorResponse
        {
            Id = membership.Id,
            UserId = membership.UserId,
            Email = membership.User.Email,
            DisplayName = membership.User.DisplayName,
            Status = membership.Status.ToString(),
            ReactivatedAt = membership.ReactivatedAt
        });
    }

    private static async Task<IResult> ResendInviteAsync(
        Guid operatorId,
        ICurrentTenant currentTenant,
        ITenantMembershipRepository membershipRepository,
        IInvitationRepository invitationRepository,
        IUserRepository userRepository)
    {
        if (currentTenant.TenantId is null || currentTenant.UserId is null)
            return Results.Unauthorized();

        if (currentTenant.UserRole != "TenantOwner")
            return Results.Forbid();

        var membership = await membershipRepository.GetByIdAsync(operatorId);
        if (membership is null || membership.TenantId != currentTenant.TenantId)
            return Results.NotFound();

        if (membership.Role != MembershipRole.Operator)
            return Results.BadRequest(new { error = "Can only resend invites for operators." });

        var pendingInvites = await invitationRepository.GetPendingByTenantAndEmailAsync(
            currentTenant.TenantId.Value, membership.User.Email);

        foreach (var invite in pendingInvites)
        {
            invite.Revoke(currentTenant.UserId.Value);
            await invitationRepository.UpdateAsync(invite);
        }

        var tokenBytes = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(tokenBytes);
        var token = Convert.ToBase64String(tokenBytes);
        var tokenHash = BCrypt.Net.BCrypt.HashPassword(token);

        var invitation = Invitation.Create(
            currentTenant.TenantId.Value,
            membership.User.Email,
            tokenHash,
            InvitationPurpose.Operator,
            currentTenant.UserId.Value,
            membership.UserId);

        await invitationRepository.AddAsync(invitation);

        return Results.Ok(new InviteOperatorResponse
        {
            MembershipId = membership.Id,
            Email = membership.User.Email,
            ActivationUrl = $"/activate?token={token}&invitation={invitation.Id}",
            ExpiresAt = invitation.ExpiresAt
        });
    }
}

public sealed class InviteOperatorRequest
{
    public string Email { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
}

public sealed class InviteOperatorResponse
{
    public Guid MembershipId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string ActivationUrl { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
}

public sealed class OperatorResponse
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? DeactivatedAt { get; init; }
    public DateTime? ReactivatedAt { get; init; }
}

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

        group.MapPost("/", CreateOperatorAsync)
            .WithName("CreateOperator");

        group.MapPost("/{operatorId:guid}/deactivate", DeactivateOperatorAsync)
            .WithName("DeactivateOperator");

        group.MapPost("/{operatorId:guid}/reactivate", ReactivateOperatorAsync)
            .WithName("ReactivateOperator");

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

    private static async Task<IResult> CreateOperatorAsync(
        [FromBody] CreateOperatorRequest request,
        ICurrentTenant currentTenant,
        AppDbContext dbContext)
    {
        if (currentTenant.TenantId is null || currentTenant.UserId is null)
            return Results.Unauthorized();

        if (currentTenant.UserRole != "TenantOwner")
            return Results.Forbid();

        if (string.IsNullOrWhiteSpace(request.Email))
            return Results.BadRequest(new { error = "Email is required." });

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            return Results.BadRequest(new { error = "Temporary password must be at least 8 characters." });

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

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        var user = existingUser;
        if (user is null)
        {
            user = User.CreateWithTemporaryPassword(email, passwordHash, request.DisplayName);
            dbContext.Users.Add(user);
        }
        else
        {
            user.Activate(passwordHash);
            user.SetMustChangePassword(true);
            dbContext.Users.Update(user);
        }

        var membership = TenantMembership.Create(
            currentTenant.TenantId.Value,
            user,
            MembershipRole.Operator);
        membership.Activate();
        dbContext.TenantMemberships.Add(membership);

        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Results.Conflict(new { error = "User cannot be assigned to another tenant." });
        }

        return Results.Created($"/api/operators/{membership.Id}", new CreateOperatorResponse
        {
            MembershipId = membership.Id,
            Email = email,
            DisplayName = request.DisplayName,
            TemporaryPassword = request.Password,
            Message = "Operator created. User must change password on first login."
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
        if (user is not null)
        {
            user.Activate(user.PasswordHash!);
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
}

public sealed class CreateOperatorRequest
{
    public string Email { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public string Password { get; init; } = string.Empty;
}

public sealed class CreateOperatorResponse
{
    public Guid MembershipId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public string TemporaryPassword { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
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

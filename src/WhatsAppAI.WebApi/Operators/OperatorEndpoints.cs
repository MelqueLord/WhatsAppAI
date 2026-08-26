using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Identity;
using WhatsAppAI.Domain.Integrations;
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

        group.MapPost("/{operatorId:guid}/reset-password", ResetPasswordAsync)
            .WithName("ResetOperatorPassword");

        group.MapPut("/{operatorId:guid}", UpdateOperatorAsync)
            .WithName("UpdateOperator");

        group.MapPut("/{operatorId:guid}/line", AssignOperatorLineAsync)
            .WithName("AssignOperatorLine");

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
            .Select(m =>
            {
                m.LoadAssignedLinesFromJson();
                return new OperatorResponse
                {
                    Id = m.Id,
                    UserId = m.UserId,
                    Email = m.User.Email,
                    DisplayName = m.User.DisplayName,
                    Status = m.Status.ToString(),
                    CreatedAt = m.CreatedAt,
                    DeactivatedAt = m.DeactivatedAt,
                    ReactivatedAt = m.ReactivatedAt,
                    AssignedConnectionType = m.AssignedConnectionType?.ToString(),
                    AssignedLineNumber = m.AssignedLineNumber,
                    AssignedLines = m.AssignedLines.Select(l => new LineAssignmentResponse
                    {
                        ConnectionType = l.ConnectionType.ToString(),
                        LineNumber = l.LineNumber
                    }).ToList()
                };
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

        var tenant = await dbContext.Tenants.FindAsync(currentTenant.TenantId.Value);
        if (tenant is null)
            return Results.NotFound(new { error = "Tenant not found." });

        if (tenant.OperatorLimit > 0)
        {
            var operatorCount = await dbContext.TenantMemberships
                .IgnoreQueryFilters()
                .CountAsync(membership => membership.TenantId == tenant.Id &&
                                          membership.Role == MembershipRole.Operator &&
                                          membership.Status != MembershipStatus.Inactive);
            if (operatorCount >= tenant.OperatorLimit)
                return Results.Conflict(new { error = $"Operator limit reached ({tenant.OperatorLimit})." });
        }

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

    private static async Task<IResult> UpdateOperatorAsync(
        Guid operatorId,
        [FromBody] UpdateOperatorRequest request,
        ICurrentTenant currentTenant,
        ITenantMembershipRepository membershipRepository,
        IUserRepository userRepository,
        AppDbContext dbContext)
    {
        if (currentTenant.TenantId is null || currentTenant.UserId is null)
            return Results.Unauthorized();
        if (currentTenant.UserRole != "TenantOwner")
            return Results.Forbid();

        var membership = await membershipRepository.GetByIdAsync(operatorId);
        if (membership is null || membership.TenantId != currentTenant.TenantId || membership.Role != MembershipRole.Operator)
            return Results.NotFound();

        var user = await userRepository.GetByIdAsync(membership.UserId);
        if (user is null)
            return Results.NotFound();

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var email = request.Email.Trim().ToLowerInvariant();
            if (await dbContext.Users.AnyAsync(u => u.Id != user.Id && u.Email == email))
                return Results.Conflict(new { error = "This email is already in use." });
            user.UpdateEmail(email);
        }

        user.UpdateDisplayName(request.DisplayName);
        await userRepository.UpdateAsync(user);

        membership.LoadAssignedLinesFromJson();
        return Results.Ok(new OperatorResponse
        {
            Id = membership.Id,
            UserId = membership.UserId,
            Email = user.Email,
            DisplayName = user.DisplayName,
            Status = membership.Status.ToString(),
            CreatedAt = membership.CreatedAt,
            DeactivatedAt = membership.DeactivatedAt,
            ReactivatedAt = membership.ReactivatedAt,
            AssignedConnectionType = membership.AssignedConnectionType?.ToString(),
            AssignedLineNumber = membership.AssignedLineNumber,
            AssignedLines = membership.AssignedLines.Select(l => new LineAssignmentResponse
            {
                ConnectionType = l.ConnectionType.ToString(),
                LineNumber = l.LineNumber
            }).ToList()
        });
    }

    private static async Task<IResult> AssignOperatorLineAsync(
        Guid operatorId,
        [FromBody] AssignOperatorLineRequest request,
        ICurrentTenant currentTenant,
        ITenantMembershipRepository membershipRepository,
        AppDbContext dbContext)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();
        if (currentTenant.UserRole != "TenantOwner")
            return Results.Forbid();

        var membership = await membershipRepository.GetByIdAsync(operatorId);
        if (membership is null || membership.TenantId != currentTenant.TenantId || membership.Role != MembershipRole.Operator)
            return Results.NotFound();

        membership.LoadAssignedLinesFromJson();

        // Support both single line (legacy) and multiple lines
        if (request.Lines is not null && request.Lines.Count > 0)
        {
            var tenant = await dbContext.Tenants.FindAsync(currentTenant.TenantId.Value);
            if (tenant is null)
                return Results.NotFound();

            var newLines = new List<LineAssignment>();
            foreach (var line in request.Lines)
            {
                if (!Enum.TryParse<WhatsAppConnectionType>(line.ConnectionType, true, out var connectionType) ||
                    line.LineNumber < 1)
                    return Results.BadRequest(new { error = "A valid connection type and line number are required." });

                var limit = connectionType == WhatsAppConnectionType.OfficialApi
                    ? tenant.OfficialApiLineCount
                    : tenant.QrCodeLineCount;
                if (limit > 0 && line.LineNumber > limit)
                    return Results.BadRequest(new { error = $"Line {line.LineNumber} is outside the tenant quota for {line.ConnectionType}." });

                newLines.Add(new LineAssignment(connectionType, line.LineNumber));
            }

            membership.SetAssignedLines(newLines);

            // Keep legacy fields in sync with first line
            if (newLines.Count > 0)
            {
                membership.AssignLine(newLines[0].ConnectionType, newLines[0].LineNumber);
            }
            else
            {
                membership.ClearLineAssignment();
            }
        }
        else if (string.IsNullOrWhiteSpace(request.ConnectionType) && request.LineNumber is null)
        {
            membership.ClearLineAssignment();
            membership.ClearAssignedLines();
        }
        else
        {
            if (!Enum.TryParse<WhatsAppConnectionType>(request.ConnectionType, true, out var connectionType) ||
                request.LineNumber is null || request.LineNumber < 1)
                return Results.BadRequest(new { error = "A valid connection type and line number are required." });

            var tenant = await dbContext.Tenants.FindAsync(currentTenant.TenantId.Value);
            if (tenant is null)
                return Results.NotFound();

            var limit = connectionType == WhatsAppConnectionType.OfficialApi
                ? tenant.OfficialApiLineCount
                : tenant.QrCodeLineCount;
            if (limit > 0 && request.LineNumber > limit)
                return Results.BadRequest(new { error = "The selected line is outside the tenant quota." });

            membership.AssignLine(connectionType, request.LineNumber.Value);
            membership.SetAssignedLines([new LineAssignment(connectionType, request.LineNumber.Value)]);
        }

        await membershipRepository.UpdateAsync(membership);

        return Results.Ok(new OperatorResponse
        {
            Id = membership.Id,
            UserId = membership.UserId,
            Email = membership.User.Email,
            DisplayName = membership.User.DisplayName,
            Status = membership.Status.ToString(),
            CreatedAt = membership.CreatedAt,
            AssignedConnectionType = membership.AssignedConnectionType?.ToString(),
            AssignedLineNumber = membership.AssignedLineNumber,
            AssignedLines = membership.AssignedLines.Select(l => new LineAssignmentResponse
            {
                ConnectionType = l.ConnectionType.ToString(),
                LineNumber = l.LineNumber
            }).ToList()
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

    private static async Task<IResult> ResetPasswordAsync(
        Guid operatorId,
        [FromBody] ResetPasswordRequest request,
        ICurrentTenant currentTenant,
        ITenantMembershipRepository membershipRepository,
        IUserRepository userRepository)
    {
        if (currentTenant.TenantId is null || currentTenant.UserId is null)
            return Results.Unauthorized();

        if (currentTenant.UserRole != "TenantOwner")
            return Results.Forbid();

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            return Results.BadRequest(new { error = "Password must be at least 8 characters." });

        var membership = await membershipRepository.GetByIdAsync(operatorId);
        if (membership is null || membership.TenantId != currentTenant.TenantId)
            return Results.NotFound();

        if (membership.Role != MembershipRole.Operator)
            return Results.BadRequest(new { error = "Can only reset password for operators." });

        var user = await userRepository.GetByIdAsync(membership.UserId);
        if (user is null)
            return Results.NotFound();

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.UpdatePassword(passwordHash);
        user.SetMustChangePassword(true);
        await userRepository.UpdateAsync(user);

        return Results.Ok(new ResetPasswordResponse
        {
            Email = user.Email,
            TemporaryPassword = request.NewPassword,
            Message = "Password reset. Operator must change password on next login."
        });
    }
}

public sealed class UpdateOperatorRequest
{
    public string? Email { get; init; }
    public string? DisplayName { get; init; }
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
    public string? AssignedConnectionType { get; init; }
    public int? AssignedLineNumber { get; init; }
    public List<LineAssignmentResponse> AssignedLines { get; init; } = [];
}

public sealed class LineAssignmentResponse
{
    public string ConnectionType { get; init; } = string.Empty;
    public int LineNumber { get; init; }
}

public sealed class AssignOperatorLineRequest
{
    public string? ConnectionType { get; init; }
    public int? LineNumber { get; init; }
    public List<LineAssignmentItem>? Lines { get; init; }
}

public sealed class LineAssignmentItem
{
    public string ConnectionType { get; init; } = string.Empty;
    public int LineNumber { get; init; }
}

public sealed class ResetPasswordRequest
{
    public string NewPassword { get; init; } = string.Empty;
}

public sealed class ResetPasswordResponse
{
    public string Email { get; init; } = string.Empty;
    public string TemporaryPassword { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

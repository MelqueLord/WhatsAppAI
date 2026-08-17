using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Identity;
using WhatsAppAI.Infrastructure.Identity;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.WebApi.Admin;

public static class AdminTenantEndpoints
{
    public static IEndpointRouteBuilder MapAdminTenantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/tenants")
            .WithTags("Admin - Tenants")
            .RequireAuthorization("PlatformAdmin");

        group.MapPost("/", CreateTenantAsync)
            .WithName("CreateTenant");

        group.MapGet("/", GetAllTenantsAsync)
            .WithName("GetAllTenants");

        group.MapGet("/{tenantId:guid}", GetTenantByIdAsync)
            .WithName("GetTenantById");

        group.MapPost("/{tenantId:guid}/suspend", SuspendTenantAsync)
            .WithName("SuspendTenant");

        group.MapPost("/{tenantId:guid}/reactivate", ReactivateTenantAsync)
            .WithName("ReactivateTenant");

        group.MapPut("/{tenantId:guid}/plan", UpdateTenantPlanAsync)
            .WithName("UpdateTenantPlan");

        return app;
    }

    private static async Task<IResult> CreateTenantAsync(
        [FromBody] CreateTenantRequest request,
        ICurrentTenant currentTenant,
        AppDbContext dbContext)
    {
        try
        {
            var existingTenant = await dbContext.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Name == request.Name);
            if (existingTenant is not null)
                return Results.Conflict(new { error = "Tenant with this name already exists." });

            var ownerEmail = request.OwnerEmail.Trim().ToLowerInvariant();
            var existingUser = await dbContext.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Email == ownerEmail);

            if (existingUser is not null)
            {
                var alreadyBelongsToTenant = await dbContext.TenantMemberships
                    .IgnoreQueryFilters()
                    .AnyAsync(m => m.UserId == existingUser.Id);

                if (existingUser.IsPlatformAdmin || alreadyBelongsToTenant)
                    return Results.Conflict(new { error = "User cannot be assigned to another tenant." });
            }

            var hasPendingInvitation = await dbContext.Invitations
                .IgnoreQueryFilters()
                .AnyAsync(invitation => invitation.Email == ownerEmail
                    && invitation.Status == InvitationStatus.Pending
                    && invitation.ExpiresAt > DateTime.UtcNow);
            if (hasPendingInvitation)
                return Results.Conflict(new { error = "User already has a pending tenant invitation." });

            var slug = TenantSlugHelper.GenerateSlug(request.Name);
            var existingBySlug = await dbContext.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Slug == slug);
            if (existingBySlug is not null)
                return Results.Conflict(new { error = "Tenant with this slug already exists." });

            var planCode = request.PlanCode.Trim().ToUpperInvariant();
            var plan = await dbContext.SubscriptionPlans
                .FirstOrDefaultAsync(p => p.Code == planCode && p.IsActive);
            if (plan is null)
                return Results.BadRequest(new { error = "Invalid or inactive plan code." });

            var tenant = Tenant.Create(request.Name, slug, plan.Id);
            tenant.Activate();
            dbContext.Tenants.Add(tenant);

            User owner;
            if (existingUser is not null)
            {
                owner = existingUser;
            }
            else
            {
                owner = User.Create(ownerEmail, request.OwnerDisplayName);
                dbContext.Users.Add(owner);
            }

            var membership = TenantMembership.Create(tenant.Id, owner, MembershipRole.TenantOwner);
            dbContext.TenantMemberships.Add(membership);

            var tokenBytes = new byte[32];
            System.Security.Cryptography.RandomNumberGenerator.Fill(tokenBytes);
            var token = Convert.ToBase64String(tokenBytes);
            var tokenHash = BCrypt.Net.BCrypt.HashPassword(token);

            var invitation = Invitation.Create(
                tenant.Id,
                ownerEmail,
                tokenHash,
                InvitationPurpose.TenantOwner,
                currentTenant.UserId ?? Guid.Empty,
                owner.Id);

            dbContext.Invitations.Add(invitation);

            await dbContext.SaveChangesAsync();

            return Results.Created($"/api/admin/tenants/{tenant.Id}", new CreateTenantResponse
            {
                TenantId = tenant.Id,
                TenantName = tenant.Name,
                Slug = tenant.Slug,
                OwnerEmail = ownerEmail,
                ActivationLink = $"/activate?token={token}&invitation={invitation.Id}",
                Message = "Save this activation link. It will not be shown again."
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Failed to create tenant",
                detail: ex.InnerException?.Message ?? ex.Message,
                statusCode: 500);
        }
    }

    private static async Task<IResult> GetAllTenantsAsync(
        ITenantRepository tenantRepository)
    {
        var tenants = await tenantRepository.GetAllAsync();
        return Results.Ok(tenants.Select(t => new TenantResponse
        {
            Id = t.Id,
            Name = t.Name,
            Slug = t.Slug,
            PlanId = t.PlanId,
            Status = t.Status.ToString(),
            Version = t.Version,
            CreatedAt = t.CreatedAt,
            SuspendedAt = t.SuspendedAt
        }));
    }

    private static async Task<IResult> GetTenantByIdAsync(
        Guid tenantId,
        ITenantRepository tenantRepository)
    {
        var tenant = await tenantRepository.GetByIdAsync(tenantId);
        if (tenant is null)
            return Results.NotFound();

        return Results.Ok(new TenantResponse
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Slug = tenant.Slug,
            PlanId = tenant.PlanId,
            Status = tenant.Status.ToString(),
            Version = tenant.Version,
            CreatedAt = tenant.CreatedAt,
            SuspendedAt = tenant.SuspendedAt,
            SuspensionReason = tenant.SuspensionReason
        });
    }

    private static async Task<IResult> SuspendTenantAsync(
        Guid tenantId,
        [FromBody] SuspendTenantRequest request,
        ITenantRepository tenantRepository,
        HttpContext httpContext)
    {
        var tenant = await tenantRepository.GetByIdAsync(tenantId);
        if (tenant is null)
            return Results.NotFound();

        if (!TryGetIfMatchVersion(httpContext, out var expectedVersion))
            return Results.BadRequest(new { error = "If-Match header with current version is required." });

        if (tenant.Version != expectedVersion)
            return Results.Conflict(new { error = "Tenant was modified by another request. Please refresh and try again." });

        try
        {
            tenant.Suspend(request.Reason);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }

        await tenantRepository.UpdateAsync(tenant);

        return Results.Ok(new TenantResponse
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Slug = tenant.Slug,
            PlanId = tenant.PlanId,
            Status = tenant.Status.ToString(),
            Version = tenant.Version,
            SuspendedAt = tenant.SuspendedAt,
            SuspensionReason = tenant.SuspensionReason
        });
    }

    private static async Task<IResult> ReactivateTenantAsync(
        Guid tenantId,
        ITenantRepository tenantRepository,
        HttpContext httpContext)
    {
        var tenant = await tenantRepository.GetByIdAsync(tenantId);
        if (tenant is null)
            return Results.NotFound();

        if (!TryGetIfMatchVersion(httpContext, out var expectedVersion))
            return Results.BadRequest(new { error = "If-Match header with current version is required." });

        if (tenant.Version != expectedVersion)
            return Results.Conflict(new { error = "Tenant was modified by another request. Please refresh and try again." });

        try
        {
            tenant.Reactivate();
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }

        await tenantRepository.UpdateAsync(tenant);

        return Results.Ok(new TenantResponse
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Slug = tenant.Slug,
            PlanId = tenant.PlanId,
            Status = tenant.Status.ToString(),
            Version = tenant.Version,
            ReactivatedAt = tenant.ReactivatedAt
        });
    }

    private static async Task<IResult> UpdateTenantPlanAsync(
        Guid tenantId,
        [FromBody] UpdatePlanRequest request,
        ITenantRepository tenantRepository,
        AppDbContext dbContext)
    {
        var tenant = await tenantRepository.GetByIdAsync(tenantId);
        if (tenant is null)
            return Results.NotFound();

        var planCode = request.PlanCode.Trim().ToUpperInvariant();
        var plan = await dbContext.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Code == planCode && p.IsActive);
        if (plan is null)
            return Results.BadRequest(new { error = "Invalid or inactive plan code." });

        if (tenant.PlanId == plan.Id)
            return Results.Ok(new { message = "Tenant already on this plan." });

        tenant.ChangePlan(plan.Id);
        await tenantRepository.UpdateAsync(tenant);

        return Results.Ok(new TenantResponse
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Slug = tenant.Slug,
            PlanId = tenant.PlanId,
            Status = tenant.Status.ToString(),
            Version = tenant.Version
        });
    }

    private static bool TryGetIfMatchVersion(HttpContext httpContext, out uint version)
    {
        version = 0;
        var ifMatch = httpContext.Request.Headers.IfMatch.ToString();
        if (string.IsNullOrWhiteSpace(ifMatch))
            return false;

        return uint.TryParse(ifMatch.Trim('"'), out version);
    }
}

public sealed class CreateTenantRequest
{
    public string Name { get; init; } = string.Empty;
    public string OwnerEmail { get; init; } = string.Empty;
    public string? OwnerDisplayName { get; init; }
    public string PlanCode { get; init; } = "BOT";
}

public sealed class CreateTenantResponse
{
    public Guid TenantId { get; init; }
    public string TenantName { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string OwnerEmail { get; init; } = string.Empty;
    public string ActivationLink { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

public sealed class SuspendTenantRequest
{
    public string Reason { get; init; } = string.Empty;
}

public sealed class UpdatePlanRequest
{
    public string PlanCode { get; init; } = string.Empty;
}

public sealed class TenantResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public Guid PlanId { get; init; }
    public string Status { get; init; } = string.Empty;
    public uint Version { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? SuspendedAt { get; init; }
    public DateTime? ReactivatedAt { get; init; }
    public string? SuspensionReason { get; init; }
}

internal static class TenantSlugHelper
{
    internal static string GenerateSlug(string name)
    {
        var slug = name.Trim().ToLowerInvariant();
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"[\s-]+", "-");
        return slug.Trim('-');
    }
}

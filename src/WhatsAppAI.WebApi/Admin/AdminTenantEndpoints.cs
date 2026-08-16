using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Identity;
using WhatsAppAI.Infrastructure.Identity;

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

        return app;
    }

    private static async Task<IResult> CreateTenantAsync(
        [FromBody] CreateTenantRequest request,
        ITenantRepository tenantRepository,
        IUserRepository userRepository,
        ITenantMembershipRepository membershipRepository,
        IInvitationRepository invitationRepository,
        ICurrentTenant currentTenant)
    {
        var existingTenant = await tenantRepository.GetByNameAsync(request.Name);
        if (existingTenant is not null)
            return Results.Conflict(new { error = "Tenant with this name already exists." });

        var ownerEmail = request.OwnerEmail.Trim().ToLowerInvariant();
        var existingUser = await userRepository.GetByEmailAsync(ownerEmail);

        var slug = TenantSlugHelper.GenerateSlug(request.Name);
        var existingBySlug = await tenantRepository.GetBySlugAsync(slug);
        if (existingBySlug is not null)
            return Results.Conflict(new { error = "Tenant with this slug already exists." });

        var tenant = Tenant.Create(request.Name, slug);
        await tenantRepository.AddAsync(tenant);

        User owner;
        if (existingUser is not null)
        {
            owner = existingUser;
        }
        else
        {
            owner = User.Create(ownerEmail, request.OwnerDisplayName);
            await userRepository.AddAsync(owner);
        }

        var membership = TenantMembership.Create(tenant.Id, owner.Id, MembershipRole.Owner);
        await membershipRepository.AddAsync(membership);

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

        await invitationRepository.AddAsync(invitation);

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

    private static async Task<IResult> GetAllTenantsAsync(
        ITenantRepository tenantRepository)
    {
        var tenants = await tenantRepository.GetAllAsync();
        return Results.Ok(tenants.Select(t => new TenantResponse
        {
            Id = t.Id,
            Name = t.Name,
            Slug = t.Slug,
            Status = t.Status.ToString(),
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
            Status = tenant.Status.ToString(),
            CreatedAt = tenant.CreatedAt,
            SuspendedAt = tenant.SuspendedAt,
            SuspensionReason = tenant.SuspensionReason
        });
    }

    private static async Task<IResult> SuspendTenantAsync(
        Guid tenantId,
        [FromBody] SuspendTenantRequest request,
        ITenantRepository tenantRepository)
    {
        var tenant = await tenantRepository.GetByIdAsync(tenantId);
        if (tenant is null)
            return Results.NotFound();

        tenant.Suspend(request.Reason);
        await tenantRepository.UpdateAsync(tenant);

        return Results.Ok(new TenantResponse
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Slug = tenant.Slug,
            Status = tenant.Status.ToString(),
            SuspendedAt = tenant.SuspendedAt,
            SuspensionReason = tenant.SuspensionReason
        });
    }

    private static async Task<IResult> ReactivateTenantAsync(
        Guid tenantId,
        ITenantRepository tenantRepository)
    {
        var tenant = await tenantRepository.GetByIdAsync(tenantId);
        if (tenant is null)
            return Results.NotFound();

        tenant.Reactivate();
        await tenantRepository.UpdateAsync(tenant);

        return Results.Ok(new TenantResponse
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Slug = tenant.Slug,
            Status = tenant.Status.ToString(),
            ReactivatedAt = tenant.ReactivatedAt
        });
    }
}

public sealed class CreateTenantRequest
{
    public string Name { get; init; } = string.Empty;
    public string OwnerEmail { get; init; } = string.Empty;
    public string? OwnerDisplayName { get; init; }
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

public sealed class TenantResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
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
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[\s-]+", "-");
        return slug.Trim('-');
    }
}

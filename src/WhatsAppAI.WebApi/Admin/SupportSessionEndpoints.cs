using Microsoft.AspNetCore.Mvc;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Audit;
using WhatsAppAI.Infrastructure.Identity;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.WebApi.Admin;

public static class SupportSessionEndpoints
{
    public static IEndpointRouteBuilder MapSupportSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/support-session")
            .WithTags("Admin - Support Session")
            .RequireAuthorization("PlatformAdmin");

        group.MapPost("/", EnterSupportSessionAsync)
            .WithName("EnterSupportSession");

        group.MapDelete("/", ExitSupportSessionAsync)
            .WithName("ExitSupportSession");

        group.MapGet("/", GetSupportSessionAsync)
            .WithName("GetSupportSession");

        return app;
    }

    private static async Task<IResult> EnterSupportSessionAsync(
        [FromBody] EnterSupportSessionRequest request,
        ICurrentTenant currentTenant,
        ITenantRepository tenantRepository,
        AppDbContext dbContext,
        HttpContext httpContext)
    {
        var tenant = await tenantRepository.GetByIdAsync(request.TenantId);
        if (tenant is null)
            return Results.NotFound(new { error = "Tenant not found." });

        if (tenant.Status != Domain.Identity.TenantStatus.Active)
            return Results.BadRequest(new { error = "Can only enter support session for active tenants." });

        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length < 10)
            return Results.BadRequest(new { error = "Reason is required and must be at least 10 characters." });

        currentTenant.EnterSupportSession(request.TenantId, reason);

        // Persist support session in cookie claims
        var identity = httpContext.User.Identity as System.Security.Claims.ClaimsIdentity;
        if (identity is not null)
        {
            identity.AddClaim(new System.Security.Claims.Claim("support_tenant_id", request.TenantId.ToString()));
            identity.AddClaim(new System.Security.Claims.Claim("support_reason", reason));

            // Re-sign in to persist the updated claims
            var authService = httpContext.RequestServices.GetRequiredService<IAuthenticationService>();
            var userRepository = httpContext.RequestServices.GetRequiredService<IUserRepository>();

            var userId = currentTenant.UserId!.Value;
            var user = await userRepository.GetByIdAsync(userId);
            if (user is not null)
            {
                await authService.SignInAsync(httpContext, user, null, isPlatformAdmin: true,
                    supportTenantId: request.TenantId, supportReason: reason);
            }
        }

        // Audit
        var audit = AuditLog.Create(
            request.TenantId,
            currentTenant.UserId,
            "SupportSession.Enter",
            "Tenant",
            request.TenantId.ToString(),
            $"Reason: {reason}",
            httpContext.Connection.RemoteIpAddress?.ToString());

        dbContext.AuditLogs.Add(audit);
        await dbContext.SaveChangesAsync();

        return Results.Ok(new
        {
            tenantId = request.TenantId,
            tenantName = tenant.Name,
            reason,
            message = "Support session active. You now have access to this tenant's resources."
        });
    }

    private static async Task<IResult> ExitSupportSessionAsync(
        ICurrentTenant currentTenant,
        AppDbContext dbContext,
        HttpContext httpContext)
    {
        if (currentTenant.SupportSession is null)
            return Results.BadRequest(new { error = "No active support session." });

        var previousTenantId = currentTenant.SupportSession.TenantId;

        // Audit before exiting
        var audit = AuditLog.Create(
            previousTenantId,
            currentTenant.UserId,
            "SupportSession.Exit",
            "Tenant",
            previousTenantId.ToString(),
            $"Session duration: {DateTime.UtcNow - currentTenant.SupportSession.StartedAt}",
            httpContext.Connection.RemoteIpAddress?.ToString());

        dbContext.AuditLogs.Add(audit);
        await dbContext.SaveChangesAsync();

        currentTenant.ExitSupportSession();

        // Re-sign in without support session claims
        var authService = httpContext.RequestServices.GetRequiredService<IAuthenticationService>();
        var userRepository = httpContext.RequestServices.GetRequiredService<IUserRepository>();

        var userId = currentTenant.UserId!.Value;
        var user = await userRepository.GetByIdAsync(userId);
        if (user is not null)
        {
            await authService.SignInAsync(httpContext, user, null, isPlatformAdmin: true);
        }

        return Results.Ok(new { message = "Support session ended." });
    }

    private static IResult GetSupportSessionAsync(ICurrentTenant currentTenant)
    {
        if (currentTenant.SupportSession is null)
            return Results.Ok(new { active = false });

        return Results.Ok(new
        {
            active = true,
            tenantId = currentTenant.SupportSession.TenantId,
            reason = currentTenant.SupportSession.Reason,
            startedAt = currentTenant.SupportSession.StartedAt
        });
    }
}

public sealed class EnterSupportSessionRequest
{
    public Guid TenantId { get; init; }
    public string? Reason { get; init; }
}

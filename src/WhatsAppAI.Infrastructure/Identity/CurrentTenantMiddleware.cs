using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using WhatsAppAI.Application.Abstractions;

namespace WhatsAppAI.Infrastructure.Identity;

internal sealed class CurrentTenantMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var currentTenant = context.RequestServices.GetRequiredService<ICurrentTenant>();

        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var tenantId = context.User.FindFirstValue("tenant_id");
            var role = context.User.FindFirstValue(ClaimTypes.Role);
            var isPlatformAdmin = context.User.HasClaim("platform_admin", "true");

            if (userId is not null && role is not null &&
                (isPlatformAdmin || tenantId is not null))
            {
                currentTenant.SetContext(
                    tenantId is not null ? Guid.Parse(tenantId) : null,
                    Guid.Parse(userId),
                    role,
                    isPlatformAdmin);
            }

            // Restore support session from claims if present
            var supportTenantId = context.User.FindFirstValue("support_tenant_id");
            var supportReason = context.User.FindFirstValue("support_reason");
            if (isPlatformAdmin && supportTenantId is not null && supportReason is not null)
            {
                currentTenant.EnterSupportSession(Guid.Parse(supportTenantId), supportReason);
            }
        }
        else
        {
            currentTenant.Clear();
        }

        await next(context);
    }
}

public static class CurrentTenantMiddlewareExtensions
{
    public static IApplicationBuilder UseCurrentTenant(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CurrentTenantMiddleware>();
    }
}

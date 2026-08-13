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

            if (userId is not null && tenantId is not null && role is not null)
            {
                currentTenant.SetContext(
                    Guid.Parse(tenantId),
                    Guid.Parse(userId),
                    role,
                    isPlatformAdmin);
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

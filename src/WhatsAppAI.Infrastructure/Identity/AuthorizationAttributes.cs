using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using WhatsAppAI.Application.Abstractions;

namespace WhatsAppAI.Infrastructure.Identity;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireTenantAccessAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var currentTenant = context.HttpContext.RequestServices.GetRequiredService<ICurrentTenant>();

        if (!currentTenant.IsAuthenticated)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // Admin must have an active support session to access tenant routes
        if (currentTenant.IsPlatformAdmin && currentTenant.SupportSession is null)
        {
            context.Result = new ObjectResult(new
            {
                error = "Platform administrators must enter a support session to access tenant resources.",
                code = "ADMIN_REQUIRES_SUPPORT_SESSION"
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        if (!currentTenant.TenantId.HasValue)
        {
            context.Result = new ForbidResult();
            return;
        }

        await Task.CompletedTask;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequirePlatformAdminAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var currentTenant = context.HttpContext.RequestServices.GetRequiredService<ICurrentTenant>();

        if (!currentTenant.IsAuthenticated)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (!currentTenant.IsPlatformAdmin)
        {
            context.Result = new ForbidResult();
            return;
        }

        await Task.CompletedTask;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireTenantOwnerAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var currentTenant = context.HttpContext.RequestServices.GetRequiredService<ICurrentTenant>();

        if (!currentTenant.IsAuthenticated)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // Admin in support session can act as owner
        if (currentTenant.IsPlatformAdmin && currentTenant.SupportSession is not null)
        {
            await Task.CompletedTask;
            return;
        }

        if (currentTenant.UserRole != "TenantOwner")
        {
            context.Result = new ForbidResult();
            return;
        }

        await Task.CompletedTask;
    }
}

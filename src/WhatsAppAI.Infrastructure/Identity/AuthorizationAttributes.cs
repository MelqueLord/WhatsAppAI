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

        if (currentTenant.UserRole != "Owner" && !currentTenant.IsPlatformAdmin)
        {
            context.Result = new ForbidResult();
            return;
        }

        await Task.CompletedTask;
    }
}

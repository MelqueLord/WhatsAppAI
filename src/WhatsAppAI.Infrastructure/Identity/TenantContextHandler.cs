using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using WhatsAppAI.Application.Abstractions;

namespace WhatsAppAI.Infrastructure.Identity;

public sealed class TenantContextRequirement : IAuthorizationRequirement { }

public sealed class TenantContextHandler : AuthorizationHandler<TenantContextRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantContextRequirement requirement)
    {
        if (context.Resource is not HttpContext httpContext)
        {
            context.Fail(new AuthorizationFailureReason(this, "No HTTP context."));
            return Task.CompletedTask;
        }

        var currentTenant = httpContext.RequestServices.GetService<ICurrentTenant>();

        if (currentTenant is null || !currentTenant.IsAuthenticated)
        {
            context.Fail(new AuthorizationFailureReason(this, "Not authenticated."));
            return Task.CompletedTask;
        }

        if (!currentTenant.TenantId.HasValue)
        {
            context.Fail(new AuthorizationFailureReason(this,
                "Platform administrators must enter a support session to access tenant resources."));
            return Task.CompletedTask;
        }

        context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

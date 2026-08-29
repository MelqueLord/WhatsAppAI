using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Infrastructure.Identity;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.WebApi;

public static class PlanFeatureEndpointFilters
{
    public static RouteGroupBuilder RequirePlanFeature(
        this RouteGroupBuilder group,
        PlanFeature feature)
    {
        group.AddEndpointFilter(async (context, next) =>
        {
            var services = context.HttpContext.RequestServices;
            var currentTenant = services.GetRequiredService<ICurrentTenant>();
            if (currentTenant.TenantId is null)
                return Results.Unauthorized();

            var dbContext = services.GetRequiredService<AppDbContext>();
            if (!await dbContext.HasFeatureAsync(
                    currentTenant.TenantId.Value,
                    feature,
                    context.HttpContext.RequestAborted))
                return Results.Forbid();

            return await next(context);
        });

        return group;
    }
}

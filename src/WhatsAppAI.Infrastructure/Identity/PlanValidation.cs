using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.Infrastructure.Identity;

public static class PlanValidation
{
    public static async Task<bool> HasAiEnabledAsync(
        this AppDbContext dbContext, Guid tenantId, CancellationToken ct = default)
    {
        var tenant = await dbContext.Tenants.FindAsync([tenantId], ct);
        if (tenant is null) return false;

        var plan = await dbContext.SubscriptionPlans.FindAsync([tenant.PlanId], ct);
        return plan is not null && plan.AiEnabled;
    }
}

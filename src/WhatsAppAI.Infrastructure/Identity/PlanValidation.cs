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

    public static Task<bool> HasBotEnabledAsync(
        this AppDbContext dbContext, Guid tenantId, CancellationToken ct = default) =>
        HasFeatureAsync(dbContext, tenantId, PlanFeature.Bot, ct);

    public static Task<bool> HasTagsEnabledAsync(
        this AppDbContext dbContext, Guid tenantId, CancellationToken ct = default) =>
        HasFeatureAsync(dbContext, tenantId, PlanFeature.Tags, ct);

    public static Task<bool> HasAutomaticDistributionEnabledAsync(
        this AppDbContext dbContext, Guid tenantId, CancellationToken ct = default) =>
        HasFeatureAsync(dbContext, tenantId, PlanFeature.AutomaticDistribution, ct);

    public static async Task<bool> HasFeatureAsync(
        this AppDbContext dbContext,
        Guid tenantId,
        PlanFeature feature,
        CancellationToken ct)
    {
        var tenant = await dbContext.Tenants.FindAsync([tenantId], ct);
        if (tenant is null)
            return false;

        var plan = await dbContext.SubscriptionPlans.FindAsync([tenant.PlanId], ct);
        if (plan is null)
            return false;

        return feature switch
        {
            PlanFeature.Bot => plan.BotEnabled,
            PlanFeature.Tags => plan.TagsEnabled,
            PlanFeature.AutomaticDistribution => plan.AutomaticDistributionEnabled,
            _ => false
        };
    }

}

public enum PlanFeature
{
    Bot,
    Tags,
    AutomaticDistribution
}

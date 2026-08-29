using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WhatsAppAI.Domain.Identity;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.Infrastructure.Persistence;

public static class SubscriptionPlanSeeder
{
    public static async Task SeedDefaultPlansAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var defaults = new List<SubscriptionPlan>
        {
            SubscriptionPlan.CreateBot(),
            SubscriptionPlan.CreateAiBot(),
            SubscriptionPlan.CreateStar(),
            SubscriptionPlan.CreateFlow(),
            SubscriptionPlan.CreateScala()
        };

        var existingCodes = await db.SubscriptionPlans
            .Select(plan => plan.Code)
            .ToListAsync();
        var plans = defaults
            .Where(plan => !existingCodes.Contains(plan.Code, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (plans.Count == 0)
            return;

        await db.SubscriptionPlans.AddRangeAsync(plans);
        await db.SaveChangesAsync();
    }
}

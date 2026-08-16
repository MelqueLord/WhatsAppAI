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

        if (await db.SubscriptionPlans.AnyAsync())
            return;

        var plans = new List<SubscriptionPlan>
        {
            SubscriptionPlan.CreateBot(),
            SubscriptionPlan.CreateAiBot()
        };

        await db.SubscriptionPlans.AddRangeAsync(plans);
        await db.SaveChangesAsync();
    }
}

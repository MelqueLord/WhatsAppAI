using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WhatsAppAI.Domain.Identity;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.Infrastructure.Workers;

public sealed class TenantSuspensionWorker(
    IServiceProvider serviceProvider,
    ILogger<TenantSuspensionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var overdueBefore = DateTime.UtcNow.AddDays(-35);
                var tenants = await dbContext.Tenants
                    .Where(tenant => tenant.Status == TenantStatus.Active && tenant.DueDate < overdueBefore)
                    .ToListAsync(stoppingToken);

                foreach (var tenant in tenants)
                {
                    tenant.Suspend("Payment overdue for more than 35 days.");
                    logger.LogWarning("Tenant {TenantId} suspended for payment overdue", tenant.Id);
                }

                if (tenants.Count > 0)
                    await dbContext.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error checking overdue tenant payments");
            }

            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }
}
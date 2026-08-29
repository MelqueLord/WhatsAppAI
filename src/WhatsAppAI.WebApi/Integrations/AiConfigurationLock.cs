using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.WebApi.Integrations;

internal static class AiConfigurationLock
{
    public static async Task AcquireAsync(
        AppDbContext dbContext,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsNpgsql())
            return;

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({tenantId.ToString()}))",
            cancellationToken);
    }
}

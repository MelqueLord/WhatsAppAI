using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Integrations;

namespace WhatsAppAI.Infrastructure.Persistence.Repositories;

public sealed class BotConfigurationRepository(AppDbContext context) : IBotConfigurationRepository
{
    public async Task<BotConfiguration?> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        var configurations = await context.Set<BotConfiguration>()
            .IgnoreQueryFilters()
            .ToListAsync(ct);
        return configurations.Find(configuration => configuration.TenantId == tenantId);
    }

    public async Task AddAsync(BotConfiguration config, CancellationToken ct = default)
    {
        context.Set<BotConfiguration>().Add(config);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(BotConfiguration config, CancellationToken ct = default)
    {
        context.Set<BotConfiguration>().Update(config);
        await context.SaveChangesAsync(ct);
    }
}

using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Integrations;

namespace WhatsAppAI.Infrastructure.Persistence.Repositories;

public sealed class BotConfigurationRepository(AppDbContext context) : IBotConfigurationRepository
{
    public async Task<BotConfiguration?> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await context.Set<BotConfiguration>().FirstOrDefaultAsync(b => b.TenantId == tenantId, ct);

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

using WhatsAppAI.Domain.Integrations;

namespace WhatsAppAI.Application.Abstractions;

public interface IBotConfigurationRepository
{
    Task<BotConfiguration?> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task AddAsync(BotConfiguration config, CancellationToken ct = default);
    Task UpdateAsync(BotConfiguration config, CancellationToken ct = default);
}

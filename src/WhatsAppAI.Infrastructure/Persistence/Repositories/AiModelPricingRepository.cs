using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Usage;

namespace WhatsAppAI.Infrastructure.Persistence.Repositories;

public sealed class AiModelPricingRepository(AppDbContext context) : IAiModelPricingRepository
{
    public async Task<AiModelPricing?> GetActiveAsync(
        string provider,
        string modelId,
        DateTime at,
        CancellationToken cancellationToken = default)
    {
        return await context.Set<AiModelPricing>()
            .Where(p => p.Provider == provider && p.ModelId == modelId &&
                p.EffectiveFrom <= at && (p.EffectiveTo == null || p.EffectiveTo > at))
            .OrderByDescending(p => p.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AiModelPricing>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await context.Set<AiModelPricing>()
            .OrderBy(p => p.Provider)
            .ThenBy(p => p.ModelId)
            .ThenByDescending(p => p.Version)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(AiModelPricing pricing, CancellationToken cancellationToken = default)
    {
        context.Set<AiModelPricing>().Add(pricing);
        await context.SaveChangesAsync(cancellationToken);
    }
}

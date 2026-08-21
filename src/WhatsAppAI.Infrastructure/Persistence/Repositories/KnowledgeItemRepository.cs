using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Knowledge;

namespace WhatsAppAI.Infrastructure.Persistence.Repositories;

public sealed class KnowledgeItemRepository(AppDbContext context) : IKnowledgeItemRepository
{
    public async Task<KnowledgeItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Set<KnowledgeItem>()
            .FirstOrDefaultAsync(k => k.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<KnowledgeItem>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await context.Set<KnowledgeItem>()
            .Where(k => k.TenantId == tenantId)
            .OrderByDescending(k => k.Priority)
            .ThenByDescending(k => k.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<KnowledgeItem>> GetActiveByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await context.Set<KnowledgeItem>()
            .IgnoreQueryFilters()
            .Where(k => k.TenantId == tenantId && k.IsActive)
            .OrderByDescending(k => k.Priority)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(KnowledgeItem item, CancellationToken cancellationToken = default)
    {
        context.Set<KnowledgeItem>().Add(item);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(KnowledgeItem item, CancellationToken cancellationToken = default)
    {
        context.Set<KnowledgeItem>().Update(item);
        await context.SaveChangesAsync(cancellationToken);
    }
}

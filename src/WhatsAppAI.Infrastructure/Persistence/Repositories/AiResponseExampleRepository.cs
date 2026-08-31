using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Automation;

namespace WhatsAppAI.Infrastructure.Persistence.Repositories;

public sealed class AiResponseExampleRepository(AppDbContext context) : IAiResponseExampleRepository
{
    public Task<AiResponseExample?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.AiResponseExamples.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    public async Task<IReadOnlyList<AiResponseExample>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        await context.AiResponseExamples
            .Where(item => item.TenantId == tenantId)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AiResponseExample>> GetActiveByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        await context.AiResponseExamples
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && item.IsActive)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(AiResponseExample example, CancellationToken cancellationToken = default)
    {
        context.AiResponseExamples.Add(example);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(AiResponseExample example, CancellationToken cancellationToken = default)
    {
        context.AiResponseExamples.Update(example);
        await context.SaveChangesAsync(cancellationToken);
    }
}

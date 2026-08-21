using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Messaging;

namespace WhatsAppAI.Infrastructure.Persistence.Repositories;

public sealed class ServiceLineRepository(AppDbContext context) : IServiceLineRepository
{
    public async Task<ServiceLine?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.Set<ServiceLine>().FirstOrDefaultAsync(q => q.Id == id, ct);

    public async Task<IReadOnlyList<ServiceLine>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await context.Set<ServiceLine>().IgnoreQueryFilters().Where(q => q.TenantId == tenantId).OrderBy(q => q.SortOrder).ThenBy(q => q.Name).ToListAsync(ct);

    public async Task<IReadOnlyList<ServiceLine>> GetActiveByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await context.Set<ServiceLine>().IgnoreQueryFilters().Where(q => q.TenantId == tenantId && q.IsActive).OrderBy(q => q.SortOrder).ThenBy(q => q.Name).ToListAsync(ct);

    public async Task AddAsync(ServiceLine queue, CancellationToken ct = default)
    {
        context.Set<ServiceLine>().Add(queue);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ServiceLine queue, CancellationToken ct = default)
    {
        context.Set<ServiceLine>().Update(queue);
        await context.SaveChangesAsync(ct);
    }
}

using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Knowledge;

namespace WhatsAppAI.Infrastructure.Persistence.Repositories;

public sealed class ClientTagRepository(AppDbContext context) : IClientTagRepository
{
    public async Task<ClientTag?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.Set<ClientTag>().FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<ClientTag>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await context.Set<ClientTag>().IgnoreQueryFilters().Where(t => t.TenantId == tenantId).OrderBy(t => t.Name).ToListAsync(ct);

    public async Task<IReadOnlyList<ClientTag>> GetActiveByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await context.Set<ClientTag>().IgnoreQueryFilters().Where(t => t.TenantId == tenantId && t.IsActive).OrderBy(t => t.Name).ToListAsync(ct);

    public async Task AddAsync(ClientTag tag, CancellationToken ct = default)
    {
        context.Set<ClientTag>().Add(tag);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ClientTag tag, CancellationToken ct = default)
    {
        context.Set<ClientTag>().Update(tag);
        await context.SaveChangesAsync(ct);
    }
}

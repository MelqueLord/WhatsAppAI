using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Identity;

namespace WhatsAppAI.Infrastructure.Persistence.Repositories;

public sealed class TenantMembershipRepository(AppDbContext context) : ITenantMembershipRepository
{
    public async Task<TenantMembership?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.TenantMemberships
            .Include(m => m.Tenant)
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public async Task<TenantMembership?> GetByUserAndTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await context.TenantMemberships
            .IgnoreQueryFilters()
            .Include(m => m.Tenant)
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.UserId == userId && m.TenantId == tenantId, cancellationToken);
    }

    public async Task<IReadOnlyList<TenantMembership>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await context.TenantMemberships
            .Include(m => m.User)
            .Where(m => m.TenantId == tenantId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TenantMembership>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.TenantMemberships
            .IgnoreQueryFilters()
            .Include(m => m.Tenant)
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(TenantMembership membership, CancellationToken cancellationToken = default)
    {
        await context.TenantMemberships.AddAsync(membership, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(TenantMembership membership, CancellationToken cancellationToken = default)
    {
        context.TenantMemberships.Update(membership);
        await context.SaveChangesAsync(cancellationToken);
    }
}

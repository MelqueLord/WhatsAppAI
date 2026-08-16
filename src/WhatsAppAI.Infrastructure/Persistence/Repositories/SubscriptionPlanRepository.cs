using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Identity;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.Infrastructure.Persistence.Repositories;

public class SubscriptionPlanRepository : ISubscriptionPlanRepository
{
    private readonly AppDbContext _db;

    public SubscriptionPlanRepository(AppDbContext db) => _db = db;

    public async Task<SubscriptionPlan?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.SubscriptionPlans.FindAsync([id], ct);

    public async Task<SubscriptionPlan?> GetByCodeAsync(string code, CancellationToken ct = default)
        => await _db.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Code == code.ToUpperInvariant(), ct);

    public async Task<IReadOnlyList<SubscriptionPlan>> GetAllAsync(CancellationToken ct = default)
        => await _db.SubscriptionPlans
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SubscriptionPlan>> GetActiveAsync(CancellationToken ct = default)
        => await _db.SubscriptionPlans
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

    public async Task AddAsync(SubscriptionPlan plan, CancellationToken ct = default)
    {
        await _db.SubscriptionPlans.AddAsync(plan, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(SubscriptionPlan plan, CancellationToken ct = default)
    {
        _db.SubscriptionPlans.Update(plan);
        await _db.SaveChangesAsync(ct);
    }
}

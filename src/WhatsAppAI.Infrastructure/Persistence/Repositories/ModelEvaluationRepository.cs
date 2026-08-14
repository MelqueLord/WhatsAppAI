using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Automation;

namespace WhatsAppAI.Infrastructure.Persistence.Repositories;

public sealed class ModelEvaluationRepository(AppDbContext context) : IModelEvaluationRepository
{
    public async Task AddAsync(ModelEvaluation evaluation, CancellationToken cancellationToken = default)
    {
        context.Set<ModelEvaluation>().Add(evaluation);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ModelEvaluation?> GetLatestApprovedAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await context.Set<ModelEvaluation>()
            .Where(e => e.TenantId == tenantId && e.IsApproved)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ModelEvaluation>> GetByTenantAsync(Guid tenantId, int limit = 20, CancellationToken cancellationToken = default)
    {
        return await context.Set<ModelEvaluation>()
            .Where(e => e.TenantId == tenantId)
            .OrderByDescending(e => e.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}

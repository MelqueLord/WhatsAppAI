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

    public async Task UpdateAsync(ModelEvaluation evaluation, CancellationToken cancellationToken = default)
    {
        context.Set<ModelEvaluation>().Update(evaluation);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ModelEvaluation?> GetLatestApprovedAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await context.Set<ModelEvaluation>()
            .IgnoreQueryFilters()
            .Where(e => e.TenantId == tenantId && e.IsApproved)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ModelEvaluation?> GetApprovedForModelAsync(
        Guid tenantId, string provider, string modelId, CancellationToken cancellationToken = default)
    {
        var normalizedProvider = provider.Trim().ToLowerInvariant();
        var normalizedModelId = modelId.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
            ? modelId["models/".Length..]
            : modelId;
        return await context.Set<ModelEvaluation>()
            .IgnoreQueryFilters()
            .Where(e => e.TenantId == tenantId && e.IsApproved &&
                e.Provider == normalizedProvider &&
                (e.ModelId == modelId || e.ModelId == normalizedModelId))
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ModelEvaluation>> GetByTenantAsync(Guid tenantId, int limit = 20, CancellationToken cancellationToken = default)
    {
        return await context.Set<ModelEvaluation>()
            .IgnoreQueryFilters()
            .Where(e => e.TenantId == tenantId)
            .OrderByDescending(e => e.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}

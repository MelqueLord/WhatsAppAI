using WhatsAppAI.Domain.Automation;

namespace WhatsAppAI.Application.Abstractions;

public interface IModelEvaluationRepository
{
    Task AddAsync(ModelEvaluation evaluation, CancellationToken cancellationToken = default);
    Task UpdateAsync(ModelEvaluation evaluation, CancellationToken cancellationToken = default);
    Task<ModelEvaluation?> GetLatestApprovedAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<ModelEvaluation?> GetApprovedForModelAsync(Guid tenantId, string modelId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ModelEvaluation>> GetByTenantAsync(Guid tenantId, int limit = 20, CancellationToken cancellationToken = default);
}

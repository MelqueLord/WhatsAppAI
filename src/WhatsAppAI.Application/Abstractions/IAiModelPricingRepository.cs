using WhatsAppAI.Domain.Usage;

namespace WhatsAppAI.Application.Abstractions;

public interface IAiModelPricingRepository
{
    Task<AiModelPricing?> GetActiveAsync(
        string provider,
        string modelId,
        DateTime at,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiModelPricing>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(AiModelPricing pricing, CancellationToken cancellationToken = default);
}

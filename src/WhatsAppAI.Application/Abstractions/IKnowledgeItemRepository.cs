using WhatsAppAI.Domain.Knowledge;

namespace WhatsAppAI.Application.Abstractions;

public interface IKnowledgeItemRepository
{
    Task<KnowledgeItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KnowledgeItem>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KnowledgeItem>> GetActiveByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task AddAsync(KnowledgeItem item, CancellationToken cancellationToken = default);
    Task UpdateAsync(KnowledgeItem item, CancellationToken cancellationToken = default);
}

public sealed record KnowledgeItemDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public int Priority { get; init; }
    public bool IsActive { get; init; }
    public uint Version { get; init; }
}

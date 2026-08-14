using WhatsAppAI.Domain.Automation;

namespace WhatsAppAI.Application.Abstractions;

public interface IAiInteractionRepository
{
    Task AddAsync(AiInteraction interaction, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiInteraction>> GetByConversationAsync(Guid tenantId, Guid conversationId, int limit = 50, CancellationToken cancellationToken = default);
}

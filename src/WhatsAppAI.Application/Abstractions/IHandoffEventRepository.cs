using WhatsAppAI.Domain.Messaging;

namespace WhatsAppAI.Application.Abstractions;

public interface IHandoffEventRepository
{
    Task AddAsync(HandoffEvent handoffEvent);
    Task<IReadOnlyList<HandoffEvent>> GetByConversationAsync(Guid tenantId, Guid conversationId);
}

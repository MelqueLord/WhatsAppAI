using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Messaging;

namespace WhatsAppAI.Infrastructure.Persistence.Repositories;

public sealed class HandoffEventRepository(AppDbContext context) : IHandoffEventRepository
{
    public async Task AddAsync(HandoffEvent handoffEvent)
    {
        context.Set<HandoffEvent>().Add(handoffEvent);
        await context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<HandoffEvent>> GetByConversationAsync(Guid tenantId, Guid conversationId)
    {
        return await context.Set<HandoffEvent>()
            .Where(h => h.TenantId == tenantId && h.ConversationId == conversationId)
            .OrderByDescending(h => h.OccurredAt)
            .ToListAsync();
    }
}

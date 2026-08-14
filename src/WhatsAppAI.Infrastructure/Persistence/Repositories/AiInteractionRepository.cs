using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Automation;

namespace WhatsAppAI.Infrastructure.Persistence.Repositories;

public sealed class AiInteractionRepository(AppDbContext context) : IAiInteractionRepository
{
    public async Task AddAsync(AiInteraction interaction, CancellationToken cancellationToken = default)
    {
        context.Set<AiInteraction>().Add(interaction);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AiInteraction>> GetByConversationAsync(
        Guid tenantId, Guid conversationId, int limit = 50, CancellationToken cancellationToken = default)
    {
        return await context.Set<AiInteraction>()
            .Where(i => i.TenantId == tenantId && i.ConversationId == conversationId)
            .OrderByDescending(i => i.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}

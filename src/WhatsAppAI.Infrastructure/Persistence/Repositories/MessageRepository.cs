using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Messaging;

namespace WhatsAppAI.Infrastructure.Persistence.Repositories;

public sealed class MessageRepository(AppDbContext context) : IMessageRepository
{
    public async Task<Message?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Set<Message>()
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public async Task<Message?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default)
    {
        return await context.Set<Message>()
            .FirstOrDefaultAsync(m => m.ExternalId == externalId, cancellationToken);
    }

    public async Task<IReadOnlyList<Message>> GetByConversationAsync(
        Guid conversationId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        return await context.Set<Message>()
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Message>> GetByTenantAsync(
        Guid tenantId,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        return await context.Set<Message>()
            .Where(m => m.TenantId == tenantId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Message message, CancellationToken cancellationToken = default)
    {
        await context.Set<Message>().AddAsync(message, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Message message, CancellationToken cancellationToken = default)
    {
        context.Set<Message>().Update(message);
        await context.SaveChangesAsync(cancellationToken);
    }
}

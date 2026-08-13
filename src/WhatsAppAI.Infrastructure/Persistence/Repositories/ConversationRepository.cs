using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Messaging;

namespace WhatsAppAI.Infrastructure.Persistence.Repositories;

public sealed class ConversationRepository(AppDbContext context) : IConversationRepository
{
    public async Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Set<Conversation>()
            .Include(c => c.Contact)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<Conversation?> GetByContactAndPhoneAsync(
        Guid tenantId,
        Guid contactId,
        string phoneNumberId,
        CancellationToken cancellationToken = default)
    {
        return await context.Set<Conversation>()
            .Include(c => c.Contact)
            .FirstOrDefaultAsync(c =>
                c.TenantId == tenantId &&
                c.ContactId == contactId &&
                c.PhoneNumberId == phoneNumberId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Conversation>> GetByTenantAsync(
        Guid tenantId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        return await context.Set<Conversation>()
            .Include(c => c.Contact)
            .Where(c => c.TenantId == tenantId)
            .OrderByDescending(c => c.LastMessageAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Conversation>> GetOpenByTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return await context.Set<Conversation>()
            .Include(c => c.Contact)
            .Where(c => c.TenantId == tenantId && c.Status == ConversationStatus.Open)
            .OrderByDescending(c => c.LastMessageAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        await context.Set<Conversation>().AddAsync(conversation, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        context.Set<Conversation>().Update(conversation);
        await context.SaveChangesAsync(cancellationToken);
    }
}

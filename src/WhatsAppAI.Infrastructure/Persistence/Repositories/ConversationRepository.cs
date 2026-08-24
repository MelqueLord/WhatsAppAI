using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Messaging;

namespace WhatsAppAI.Infrastructure.Persistence.Repositories;

public sealed class ConversationRepository(AppDbContext context) : IConversationRepository
{
    public async Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var conversations = await context.Set<Conversation>()
            .IgnoreQueryFilters()
            .Include(c => c.Contact)
            .ToListAsync(cancellationToken);
        return conversations.Find(conversation => conversation.Id == id);
    }

    public async Task<Conversation?> GetByContactAndPhoneAsync(
        Guid tenantId,
        Guid contactId,
        string phoneNumberId,
        CancellationToken cancellationToken = default)
    {
        var conversations = await context.Set<Conversation>()
            .IgnoreQueryFilters()
            .Include(c => c.Contact)
            .ToListAsync(cancellationToken);
        return conversations.Find(conversation =>
            conversation.TenantId == tenantId &&
            conversation.ContactId == contactId &&
            string.Equals(conversation.PhoneNumberId, phoneNumberId, StringComparison.Ordinal));
    }

    public async Task<IReadOnlyList<Conversation>> GetByTenantAsync(
        Guid tenantId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        return await context.Set<Conversation>()
            .Include(c => c.Contact)
            .Where(c => c.TenantId == tenantId)
            .OrderByDescending(c => c.LastMessageAt != null)
            .ThenByDescending(c => c.LastMessageAt)
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
            .OrderByDescending(c => c.LastMessageAt != null)
            .ThenByDescending(c => c.LastMessageAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        try
        {
            await context.Set<Conversation>().AddAsync(conversation, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("FOREIGN KEY") == true)
        {
            // Conversa já existe ou referência de contato inválida; detach para permitir retry
            context.Entry(conversation).State = EntityState.Detached;
        }
    }

    public async Task UpdateAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        context.Set<Conversation>().Update(conversation);
        await context.SaveChangesAsync(cancellationToken);
    }
}

using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Messaging;

namespace WhatsAppAI.Infrastructure.Persistence.Repositories;

public sealed class ContactTagRepository(AppDbContext context) : IContactTagRepository
{
    public async Task<IReadOnlyList<ContactTag>> GetByContactAsync(Guid contactId, CancellationToken ct = default)
        => await context.Set<ContactTag>().Where(ct => ct.ContactId == contactId).ToListAsync(ct);

    public async Task<IReadOnlyList<ContactTag>> GetByTagAsync(Guid tagId, CancellationToken ct = default)
        => await context.Set<ContactTag>().Where(ct => ct.TagId == tagId).ToListAsync(ct);

    public async Task AddAsync(ContactTag contactTag, CancellationToken ct = default)
    {
        context.Set<ContactTag>().Add(contactTag);
        await context.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(Guid contactId, Guid tagId, CancellationToken ct = default)
    {
        var entity = await context.Set<ContactTag>().FirstOrDefaultAsync(ct => ct.ContactId == contactId && ct.TagId == tagId, ct);
        if (entity is not null)
        {
            context.Set<ContactTag>().Remove(entity);
            await context.SaveChangesAsync(ct);
        }
    }

    public async Task<bool> ExistsAsync(Guid contactId, Guid tagId, CancellationToken ct = default)
        => await context.Set<ContactTag>().AnyAsync(ct => ct.ContactId == contactId && ct.TagId == tagId, ct);
}

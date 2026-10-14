using Microsoft.EntityFrameworkCore;
using Npgsql;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Messaging;

namespace WhatsAppAI.Infrastructure.Persistence.Repositories;

public sealed class ContactRepository(AppDbContext context) : IContactRepository
{
    public async Task<Contact?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Set<Contact>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<Contact?> GetByPhoneAsync(Guid tenantId, string phoneNumber, CancellationToken cancellationToken = default)
    {
        var normalizedPhone = Contact.NormalizePhoneNumber(phoneNumber);
        var tenantContacts = await context.Set<Contact>()
            .IgnoreQueryFilters()
            .Where(contact => contact.TenantId == tenantId && !contact.PhoneNumber.StartsWith("anon-"))
            .OrderBy(contact => contact.CreatedAt)
            .ToListAsync(cancellationToken);

        return tenantContacts.Find(contact =>
            Contact.NormalizePhoneNumber(contact.PhoneNumber) == normalizedPhone);
    }

    public async Task<IReadOnlyList<Contact>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await context.Set<Contact>()
            .Where(c => c.TenantId == tenantId)
            .OrderByDescending(c => c.LastMessageAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Contact contact, CancellationToken cancellationToken = default)
    {
        try
        {
            await context.Set<Contact>().AddAsync(contact, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException?.Message.Contains("UNIQUE constraint failed", StringComparison.Ordinal) == true ||
            ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Contato já existe; remover da sessão e deixar usar o existente
            context.Entry(contact).State = EntityState.Detached;
        }
    }

    public async Task AddRangeAsync(IEnumerable<Contact> contacts, CancellationToken cancellationToken = default)
    {
        await context.Set<Contact>().AddRangeAsync(contacts, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Contact contact, CancellationToken cancellationToken = default)
    {
        context.Set<Contact>().Update(contact);
        await context.SaveChangesAsync(cancellationToken);
    }
}

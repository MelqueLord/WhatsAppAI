using Microsoft.EntityFrameworkCore;
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
        var contacts = await context.Set<Contact>()
            .IgnoreQueryFilters()
            .ToListAsync(cancellationToken);
        var normalizedPhone = NormalizePhone(phoneNumber);
        return contacts.Find(contact =>
            contact.TenantId == tenantId && NormalizePhone(contact.PhoneNumber) == normalizedPhone);
    }

    private static string NormalizePhone(string phoneNumber) =>
        new string(phoneNumber.Where(char.IsDigit).ToArray());

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
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UNIQUE constraint failed") == true)
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

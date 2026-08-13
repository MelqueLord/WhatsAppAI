using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Messaging;

namespace WhatsAppAI.Infrastructure.Persistence.Repositories;

public sealed class ContactRepository(AppDbContext context) : IContactRepository
{
    public async Task<Contact?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Set<Contact>()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<Contact?> GetByPhoneAsync(Guid tenantId, string phoneNumber, CancellationToken cancellationToken = default)
    {
        return await context.Set<Contact>()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.PhoneNumber == phoneNumber, cancellationToken);
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
        await context.Set<Contact>().AddAsync(contact, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Contact contact, CancellationToken cancellationToken = default)
    {
        context.Set<Contact>().Update(contact);
        await context.SaveChangesAsync(cancellationToken);
    }
}

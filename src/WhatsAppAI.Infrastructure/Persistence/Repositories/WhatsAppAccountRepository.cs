using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Integrations;

namespace WhatsAppAI.Infrastructure.Persistence.Repositories;

public sealed class WhatsAppAccountRepository(AppDbContext context) : IWhatsAppAccountRepository
{
    public async Task<WhatsAppAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Set<WhatsAppAccount>()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<WhatsAppAccount?> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await context.Set<WhatsAppAccount>()
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.IsActive, cancellationToken);
    }

    public async Task<WhatsAppAccount?> GetByTenantAndSlotAsync(
        Guid tenantId,
        WhatsAppConnectionType connectionType,
        int lineNumber,
        CancellationToken cancellationToken = default)
    {
        return await context.Set<WhatsAppAccount>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(account =>
                account.TenantId == tenantId &&
                account.ConnectionType == connectionType &&
                account.LineNumber == lineNumber &&
                account.IsActive,
                cancellationToken);
    }

    public async Task<IReadOnlyList<WhatsAppAccount>> GetAllByTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return await context.Set<WhatsAppAccount>()
            .Where(a => a.TenantId == tenantId)
            .OrderBy(a => a.ConnectionType)
            .ThenBy(a => a.LineNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<WhatsAppAccount?> GetByPhoneNumberIdAsync(string phoneNumberId, CancellationToken cancellationToken = default)
    {
        return await context.Set<WhatsAppAccount>()
            .FirstOrDefaultAsync(a => a.PhoneNumberId == phoneNumberId, cancellationToken);
    }

    public async Task AddAsync(WhatsAppAccount account, CancellationToken cancellationToken = default)
    {
        await context.Set<WhatsAppAccount>().AddAsync(account, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(WhatsAppAccount account, CancellationToken cancellationToken = default)
    {
        context.Set<WhatsAppAccount>().Update(account);
        await context.SaveChangesAsync(cancellationToken);
    }
}

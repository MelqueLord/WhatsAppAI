using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Privacy;

namespace WhatsAppAI.Infrastructure.Persistence.Repositories;

public sealed class CustomerMemoryRepository(AppDbContext context) : ICustomerMemoryRepository
{
    public async Task<IReadOnlyList<CustomerMemory>> GetActiveByContactAsync(
        Guid tenantId,
        Guid contactId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await context.CustomerMemories.IgnoreQueryFilters()
            .Where(memory => memory.TenantId == tenantId
                && memory.ContactId == contactId
                && memory.IsActive
                && memory.ExpiresAt > now)
            .OrderByDescending(memory => memory.UpdatedAt)
            .ThenByDescending(memory => memory.CreatedAt)
            .Take(4)
            .ToListAsync(cancellationToken);
    }
}

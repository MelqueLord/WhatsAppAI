using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Automation.Policy;
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
        return await (
            from memory in context.CustomerMemories.IgnoreQueryFilters()
            join consent in context.ConsentEvidence.IgnoreQueryFilters()
                on memory.ConsentEvidenceId equals consent.Id
            join purpose in context.ProcessingPurposes.IgnoreQueryFilters()
                on consent.ProcessingPurposeId equals purpose.Id
            where memory.TenantId == tenantId
                && memory.ContactId == contactId
                && memory.IsActive
                && memory.ExpiresAt > now
                && consent.TenantId == tenantId
                && consent.ContactId == contactId
                && consent.RevokedAt == null
                && purpose.TenantId == tenantId
                && purpose.IsActive
                && purpose.Name == AiConsentOptInPolicy.DefaultPurposeName
            orderby memory.UpdatedAt descending, memory.CreatedAt descending
            select memory)
            .Take(4)
            .ToListAsync(cancellationToken);
    }
}

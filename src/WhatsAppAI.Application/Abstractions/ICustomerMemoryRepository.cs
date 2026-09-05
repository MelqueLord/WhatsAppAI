using WhatsAppAI.Domain.Privacy;

namespace WhatsAppAI.Application.Abstractions;

public interface ICustomerMemoryRepository
{
    Task<IReadOnlyList<CustomerMemory>> GetActiveByContactAsync(
        Guid tenantId,
        Guid contactId,
        CancellationToken cancellationToken = default);
}

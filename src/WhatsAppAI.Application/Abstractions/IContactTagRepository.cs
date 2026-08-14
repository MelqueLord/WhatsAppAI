using WhatsAppAI.Domain.Messaging;

namespace WhatsAppAI.Application.Abstractions;

public interface IContactTagRepository
{
    Task<IReadOnlyList<ContactTag>> GetByContactAsync(Guid contactId, CancellationToken ct = default);
    Task<IReadOnlyList<ContactTag>> GetByTagAsync(Guid tagId, CancellationToken ct = default);
    Task AddAsync(ContactTag contactTag, CancellationToken ct = default);
    Task RemoveAsync(Guid contactId, Guid tagId, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid contactId, Guid tagId, CancellationToken ct = default);
}

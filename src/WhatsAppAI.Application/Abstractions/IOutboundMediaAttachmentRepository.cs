using WhatsAppAI.Domain.Messaging;

namespace WhatsAppAI.Application.Abstractions;

public interface IOutboundMediaAttachmentRepository
{
    Task<OutboundMediaAttachment?> GetByMessageIdAsync(Guid tenantId, Guid messageId, CancellationToken cancellationToken = default);
    Task AddAsync(OutboundMediaAttachment attachment, CancellationToken cancellationToken = default);
    Task UpdateAsync(OutboundMediaAttachment attachment, CancellationToken cancellationToken = default);
}

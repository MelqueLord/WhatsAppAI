using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Messaging;

namespace WhatsAppAI.Infrastructure.Persistence.Repositories;

public sealed class OutboundMediaAttachmentRepository(AppDbContext context) : IOutboundMediaAttachmentRepository
{
    public Task<OutboundMediaAttachment?> GetByMessageIdAsync(Guid tenantId, Guid messageId, CancellationToken cancellationToken = default) =>
        context.OutboundMediaAttachments
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.TenantId == tenantId && item.MessageId == messageId, cancellationToken);

    public async Task AddAsync(OutboundMediaAttachment attachment, CancellationToken cancellationToken = default)
    {
        context.OutboundMediaAttachments.Add(attachment);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(OutboundMediaAttachment attachment, CancellationToken cancellationToken = default)
    {
        context.OutboundMediaAttachments.Update(attachment);
        await context.SaveChangesAsync(cancellationToken);
    }
}

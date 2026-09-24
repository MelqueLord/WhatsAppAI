using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Domain.Messaging;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.WebApi.Contacts;

internal static class QueueContactQuery
{
    internal static IQueryable<Contact> ForQueue(AppDbContext dbContext, Guid tenantId, Guid queueId)
    {
        return dbContext.Contacts
            .IgnoreQueryFilters()
            .Where(contact =>
                contact.TenantId == tenantId &&
                !contact.PhoneNumber.StartsWith("anon-") &&
                (contact.QueueId == queueId || dbContext.Conversations
                    .IgnoreQueryFilters()
                    .Any(conversation =>
                        conversation.TenantId == tenantId &&
                        conversation.ContactId == contact.Id &&
                        conversation.QueueId == queueId &&
                        conversation.Status == ConversationStatus.Open)));
    }
}

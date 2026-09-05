using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Conversations.Queries;
using WhatsAppAI.Domain.Integrations;
using WhatsAppAI.Domain.Knowledge;
using WhatsAppAI.Domain.Messaging;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.Infrastructure.Conversations;

internal sealed class ConversationQueries(AppDbContext context) : IConversationQueries
{
    public async Task<CursorPaginationResponse<ConversationDto>> GetConversationsAsync(
        Guid tenantId, CursorPaginationRequest request, string? operatorUserId = null, List<string>? phoneNumberIds = null,
        Guid? queueId = null, ConversationStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var limit = Math.Clamp(request.Limit, 1, 100);

        var query = context.Conversations
            .Include(c => c.Contact)
            .Where(c => c.TenantId == tenantId && c.Status == (status ?? ConversationStatus.Open))
            .AsQueryable();

        if (operatorUserId == "unassigned")
            query = query.Where(c => string.IsNullOrEmpty(c.AssignedToUserId));
        else if (!string.IsNullOrWhiteSpace(operatorUserId))
            query = query.Where(c => c.AssignedToUserId == operatorUserId);

        if (phoneNumberIds is { Count: > 0 })
        {
            // "manual" is a legacy phoneNumberId for QR line 1 — only include it when
            // the filter contains the first QR slot (qr:...:1) to avoid leaking those
            // conversations into other line tabs.
            var includeManual = phoneNumberIds.Exists(p =>
                p.StartsWith("qr:", StringComparison.OrdinalIgnoreCase) && p.EndsWith(":1"));
            query = includeManual
                ? query.Where(c => phoneNumberIds.Contains(c.PhoneNumberId) || c.PhoneNumberId == "manual")
                : query.Where(c => phoneNumberIds.Contains(c.PhoneNumberId));
        }

        if (queueId.HasValue)
            query = query.Where(c => c.QueueId == queueId.Value);

        if (!string.IsNullOrEmpty(request.Cursor))
        {
            var cursorData = DecodeCursor(request.Cursor);
            if (cursorData is not null)
            {
                var cursorTimestamp = cursorData.Value.timestamp;
                var cursorId = cursorData.Value.id;

                if (cursorTimestamp == DateTime.MinValue)
                {
                    // Paginating through conversations with null LastMessageAt
                    query = query.Where(c => c.LastMessageAt == null && c.Id.CompareTo(cursorId) < 0);
                }
                else
                {
                    // Paginating through conversations with non-null LastMessageAt
                    query = query.Where(c =>
                        (c.LastMessageAt != null && c.LastMessageAt < cursorTimestamp) ||
                        (c.LastMessageAt == cursorTimestamp && c.Id.CompareTo(cursorId) < 0));
                }
            }
        }

        var rawConversations = await query
            .OrderByDescending(c => c.LastMessageAt != null)
            .ThenByDescending(c => c.LastMessageAt)
            .ThenByDescending(c => c.Id)
            .Take(limit + 1)
            .Select(c => new
            {
                c.Id,
                c.ContactId,
                ContactName = c.Contact.Name ?? c.Contact.PhoneNumber,
                ContactPhone = c.Contact.PhoneNumber,
                Mode = c.Mode.ToString(),
                Status = c.Status.ToString(),
                c.Version,
                LastMessage = c.Messages.OrderByDescending(m => m.CreatedAt).FirstOrDefault()!.Content,
                c.LastMessageAt,
                c.QueueId,
                c.PhoneNumberId,
                c.WindowExpiresAt,
            })
            .ToListAsync(cancellationToken);

        var contactIds = rawConversations.Select(c => c.ContactId).Distinct().ToList();
        var tagsByContact = await (
            from ct in context.ContactTags
            join t in context.ClientTags on ct.TagId equals t.Id
            where contactIds.Contains(ct.ContactId) && t.IsActive
            select new { ct.ContactId, t.Name, t.Color }
        ).ToListAsync(cancellationToken);
        var tagsLookup = tagsByContact
            .GroupBy(x => x.ContactId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ConversationTagDto>)g.Select(x => new ConversationTagDto(x.Name, x.Color)).ToList());

        var activeQrExists = await context.WhatsAppAccounts
            .AnyAsync(a => a.ConnectionType == WhatsAppConnectionType.QrCode && a.IsActive, cancellationToken);
        var qrPhoneNumberIds = await context.WhatsAppAccounts
            .Where(a => a.ConnectionType == WhatsAppConnectionType.QrCode)
            .Select(a => a.PhoneNumberId)
            .ToListAsync(cancellationToken);

        var conversations = rawConversations.Select(c => new ConversationDto
        {
            Id = c.Id,
            ContactId = c.ContactId,
            ContactName = c.ContactName,
            ContactPhone = c.ContactPhone,
            Mode = c.Mode,
            Status = c.Status,
            Version = c.Version,
            LastMessage = c.LastMessage,
            LastMessageAt = c.LastMessageAt,
            QueueId = c.QueueId,
            Tags = tagsLookup.GetValueOrDefault(c.ContactId, []),
            IsQrCode = c.PhoneNumberId.StartsWith("qr:") || c.PhoneNumberId == "whatsapp-web" ||
                (c.PhoneNumberId == "manual" && activeQrExists) ||
                qrPhoneNumberIds.Contains(c.PhoneNumberId),
            IsWindowOpen = c.PhoneNumberId.StartsWith("qr:") || c.PhoneNumberId == "whatsapp-web" ||
                (activeQrExists && (qrPhoneNumberIds.Contains(c.PhoneNumberId) || c.PhoneNumberId == "manual")) ||
                (c.WindowExpiresAt.HasValue && c.WindowExpiresAt.Value > DateTime.UtcNow),
        }).ToList();

        var hasMore = conversations.Count > limit;
        if (hasMore) conversations.RemoveAt(conversations.Count - 1);

        var nextCursor = hasMore && conversations.Count > 0
            ? EncodeCursor(conversations[^1].LastMessageAt ?? DateTime.MinValue, conversations[^1].Id)
            : null;

        return new CursorPaginationResponse<ConversationDto>
        {
            Items = conversations,
            NextCursor = nextCursor,
            HasMore = hasMore
        };
    }

    public async Task<CursorPaginationResponse<MessageDto>> GetMessagesAsync(
        Guid tenantId, Guid conversationId, CursorPaginationRequest request, CancellationToken cancellationToken = default)
    {
        var limit = Math.Clamp(request.Limit, 1, 100);

        var query = context.Messages
            .IgnoreQueryFilters()
            .Include(m => m.Contact)
            .Where(m => m.TenantId == tenantId && m.ConversationId == conversationId)
            .AsQueryable();

        if (!string.IsNullOrEmpty(request.Cursor))
        {
            var cursorData = DecodeCursor(request.Cursor);
            if (cursorData is not null)
            {
                query = query.Where(m =>
                    m.CreatedAt < cursorData.Value.timestamp ||
                    (m.CreatedAt == cursorData.Value.timestamp && m.Id.CompareTo(cursorData.Value.id) < 0));
            }
        }

        var messages = await query
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .Take(limit + 1)
            .Select(m => new MessageDto
            {
                Id = m.Id,
                Direction = m.Direction.ToString(),
                Status = m.Status.ToString(),
                Type = m.Type.ToString(),
                Content = m.Content,
                MediaId = m.MediaId,
                Caption = m.Caption,
                CreatedAt = m.CreatedAt,
                SenderName = m.Direction == MessageDirection.Inbound
                    ? m.Contact.Name ?? m.Contact.PhoneNumber
                    : "You"
            })
            .ToListAsync(cancellationToken);

        var responseMessageIds = messages
            .Where(message => message.Direction == nameof(MessageDirection.Outbound))
            .Select(message => message.Id)
            .ToList();
        if (responseMessageIds.Count > 0)
        {
            var interactions = await context.AiInteractions
                .Where(interaction => interaction.TenantId == tenantId &&
                    interaction.ResponseMessageId.HasValue &&
                    responseMessageIds.Contains(interaction.ResponseMessageId.Value))
                .Select(interaction => new
                {
                    interaction.ResponseMessageId,
                    interaction.Id,
                    interaction.FeedbackRating,
                    interaction.FeedbackNote,
                    interaction.CorrectedResponse
                })
                .ToListAsync(cancellationToken);

            var interactionByMessageId = interactions
                .Where(interaction => interaction.ResponseMessageId.HasValue)
                .ToDictionary(interaction => interaction.ResponseMessageId!.Value);
            messages = messages
                .Select(message => interactionByMessageId.TryGetValue(message.Id, out var interaction)
                    ? message with
                    {
                        AiInteractionId = interaction.Id,
                        AiFeedback = interaction.FeedbackRating.HasValue
                            ? new AiFeedbackDto(
                                interaction.FeedbackRating.Value.ToString(),
                                interaction.FeedbackNote,
                                interaction.CorrectedResponse)
                            : null
                    }
                    : message)
                .ToList();
        }

        var hasMore = messages.Count > limit;
        if (hasMore) messages.RemoveAt(messages.Count - 1);

        var nextCursor = hasMore && messages.Count > 0
            ? EncodeCursor(messages[^1].CreatedAt, messages[^1].Id)
            : null;

        return new CursorPaginationResponse<MessageDto>
        {
            Items = messages,
            NextCursor = nextCursor,
            HasMore = hasMore
        };
    }

    public async Task<ConversationDto?> GetConversationByIdAsync(
        Guid tenantId, Guid conversationId, CancellationToken cancellationToken = default)
    {
        var raw = await context.Conversations
            .Include(c => c.Contact)
            .Where(c => c.TenantId == tenantId && c.Id == conversationId)
            .Select(c => new
            {
                c.Id,
                c.ContactId,
                ContactName = c.Contact.Name ?? c.Contact.PhoneNumber,
                ContactPhone = c.Contact.PhoneNumber,
                Mode = c.Mode.ToString(),
                Status = c.Status.ToString(),
                c.Version,
                LastMessage = c.Messages.OrderByDescending(m => m.CreatedAt).FirstOrDefault()!.Content,
                c.LastMessageAt,
                c.QueueId,
                c.PhoneNumberId,
                c.WindowExpiresAt,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (raw is null) return null;

        var tags = await (
            from ct in context.ContactTags
            join t in context.ClientTags on ct.TagId equals t.Id
            where ct.ContactId == raw.ContactId && t.IsActive
            select new ConversationTagDto(t.Name, t.Color)
        ).ToListAsync(cancellationToken);

        var activeQrExists = await context.WhatsAppAccounts
            .AnyAsync(a => a.ConnectionType == WhatsAppConnectionType.QrCode && a.IsActive, cancellationToken);
        var qrPhoneNumberIds = await context.WhatsAppAccounts
            .Where(a => a.ConnectionType == WhatsAppConnectionType.QrCode)
            .Select(a => a.PhoneNumberId)
            .ToListAsync(cancellationToken);

        return new ConversationDto
        {
            Id = raw.Id,
            ContactId = raw.ContactId,
            ContactName = raw.ContactName,
            ContactPhone = raw.ContactPhone,
            Mode = raw.Mode,
            Status = raw.Status,
            Version = raw.Version,
            LastMessage = raw.LastMessage,
            LastMessageAt = raw.LastMessageAt,
            QueueId = raw.QueueId,
            Tags = tags,
            IsQrCode = raw.PhoneNumberId.StartsWith("qr:") || raw.PhoneNumberId == "whatsapp-web" ||
                (raw.PhoneNumberId == "manual" && activeQrExists) ||
                qrPhoneNumberIds.Contains(raw.PhoneNumberId),
            IsWindowOpen = raw.PhoneNumberId.StartsWith("qr:") || raw.PhoneNumberId == "whatsapp-web" ||
                (activeQrExists && (qrPhoneNumberIds.Contains(raw.PhoneNumberId) || raw.PhoneNumberId == "manual")) ||
                (raw.WindowExpiresAt.HasValue && raw.WindowExpiresAt.Value > DateTime.UtcNow),
        };
    }

    private static string EncodeCursor(DateTime timestamp, Guid id)
    {
        var data = $"{timestamp.Ticks}:{id}";
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(data));
    }

    private static (DateTime timestamp, Guid id)? DecodeCursor(string cursor)
    {
        try
        {
            var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var parts = decoded.Split(':');
            if (parts.Length == 2 && long.TryParse(parts[0], out var ticks) && Guid.TryParse(parts[1], out var id))
            {
                return (new DateTime(ticks, DateTimeKind.Utc), id);
            }
        }
        catch { }
        return null;
    }
}

using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Conversations.Queries;
using WhatsAppAI.Domain.Messaging;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.Infrastructure.Conversations;

internal sealed class ConversationQueries(AppDbContext context) : IConversationQueries
{
    public async Task<CursorPaginationResponse<ConversationDto>> GetConversationsAsync(
        Guid tenantId, CursorPaginationRequest request, string? operatorUserId = null, string? phoneNumberId = null,
        CancellationToken cancellationToken = default)
    {
        var limit = Math.Clamp(request.Limit, 1, 100);

        var query = context.Conversations
            .Include(c => c.Contact)
            .Where(c => c.TenantId == tenantId)
            .AsQueryable();

        if (operatorUserId == "unassigned")
            query = query.Where(c => string.IsNullOrEmpty(c.AssignedToUserId));
        else if (!string.IsNullOrWhiteSpace(operatorUserId))
            query = query.Where(c => c.AssignedToUserId == operatorUserId);

        if (!string.IsNullOrWhiteSpace(phoneNumberId))
            query = query.Where(c => c.PhoneNumberId == phoneNumberId);

        if (!string.IsNullOrEmpty(request.Cursor))
        {
            var cursorData = DecodeCursor(request.Cursor);
            if (cursorData is not null)
            {
                query = query.Where(c =>
                    c.LastMessageAt < cursorData.Value.timestamp ||
                    (c.LastMessageAt == cursorData.Value.timestamp && c.Id.CompareTo(cursorData.Value.id) < 0));
            }
        }

        var conversations = await query
            .OrderByDescending(c => c.LastMessageAt)
            .ThenByDescending(c => c.Id)
            .Take(limit + 1)
            .Select(c => new ConversationDto
            {
                Id = c.Id,
                ContactId = c.ContactId,
                ContactName = c.Contact.Name ?? c.Contact.PhoneNumber,
                ContactPhone = c.Contact.PhoneNumber,
                Mode = c.Mode.ToString(),
                Status = c.Status.ToString(),
                Version = c.Version,
                LastMessage = c.Messages.OrderByDescending(m => m.CreatedAt).FirstOrDefault()!.Content,
                LastMessageAt = c.LastMessageAt,
                IsWindowOpen = c.PhoneNumberId.StartsWith("qr:") ||
                    (c.WindowExpiresAt.HasValue && c.WindowExpiresAt.Value > DateTime.UtcNow)
            })
            .ToListAsync(cancellationToken);

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
        return await context.Conversations
            .Include(c => c.Contact)
            .Where(c => c.TenantId == tenantId && c.Id == conversationId)
            .Select(c => new ConversationDto
            {
                Id = c.Id,
                ContactId = c.ContactId,
                ContactName = c.Contact.Name ?? c.Contact.PhoneNumber,
                ContactPhone = c.Contact.PhoneNumber,
                Mode = c.Mode.ToString(),
                Status = c.Status.ToString(),
                Version = c.Version,
                LastMessage = c.Messages.OrderByDescending(m => m.CreatedAt).FirstOrDefault()!.Content,
                LastMessageAt = c.LastMessageAt,
                IsWindowOpen = c.PhoneNumberId.StartsWith("qr:") ||
                    (c.WindowExpiresAt.HasValue && c.WindowExpiresAt.Value > DateTime.UtcNow)
            })
            .FirstOrDefaultAsync(cancellationToken);
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

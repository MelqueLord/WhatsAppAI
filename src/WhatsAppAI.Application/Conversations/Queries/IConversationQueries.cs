namespace WhatsAppAI.Application.Conversations.Queries;

public interface IConversationQueries
{
    Task<CursorPaginationResponse<ConversationDto>> GetConversationsAsync(
        Guid tenantId, CursorPaginationRequest request, string? operatorUserId = null, string? phoneNumberId = null,
        CancellationToken cancellationToken = default);

    Task<CursorPaginationResponse<MessageDto>> GetMessagesAsync(
        Guid tenantId, Guid conversationId, CursorPaginationRequest request, CancellationToken cancellationToken = default);

    Task<ConversationDto?> GetConversationByIdAsync(
        Guid tenantId, Guid conversationId, CancellationToken cancellationToken = default);
}

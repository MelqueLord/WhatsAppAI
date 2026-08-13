using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Conversations.Queries;
using WhatsAppAI.Domain.Messaging;
using WhatsAppAI.Infrastructure.Identity;
using WhatsAppAI.WebApi.Hubs;

namespace WhatsAppAI.WebApi.Conversations;

public static class ConversationEndpoints
{
    public static IEndpointRouteBuilder MapConversationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/conversations")
            .WithTags("Conversations")
            .RequireAuthorization();

        group.MapGet("/", ListConversationsAsync)
            .WithName("ListConversations");

        group.MapGet("/{conversationId:guid}", GetConversationAsync)
            .WithName("GetConversation");

        group.MapGet("/{conversationId:guid}/messages", ListMessagesAsync)
            .WithName("ListMessages");

        group.MapPost("/{conversationId:guid}/messages", SendMessageAsync)
            .WithName("SendMessage");

        return app;
    }

    private static async Task<IResult> ListConversationsAsync(
        ICurrentTenant currentTenant,
        IConversationQueries conversationQueries,
        string? cursor = null,
        int limit = 50)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();

        var result = await conversationQueries.GetConversationsAsync(
            currentTenant.TenantId.Value,
            new CursorPaginationRequest { Cursor = cursor, Limit = limit });

        return Results.Ok(result);
    }

    private static async Task<IResult> GetConversationAsync(
        Guid conversationId,
        ICurrentTenant currentTenant,
        IConversationQueries conversationQueries)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();

        var conversation = await conversationQueries.GetConversationByIdAsync(
            currentTenant.TenantId.Value, conversationId);

        return conversation is not null ? Results.Ok(conversation) : Results.NotFound();
    }

    private static async Task<IResult> ListMessagesAsync(
        Guid conversationId,
        ICurrentTenant currentTenant,
        IConversationQueries conversationQueries,
        string? cursor = null,
        int limit = 50)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();

        var result = await conversationQueries.GetMessagesAsync(
            currentTenant.TenantId.Value,
            conversationId,
            new CursorPaginationRequest { Cursor = cursor, Limit = limit });

        return Results.Ok(result);
    }

    private static async Task<IResult> SendMessageAsync(
        Guid conversationId,
        [FromBody] SendMessageRequest request,
        ICurrentTenant currentTenant,
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        IHubContext<InboxHub> hubContext)
    {
        if (currentTenant.TenantId is null || currentTenant.UserId is null)
            return Results.Unauthorized();

        var conversation = await conversationRepository.GetByIdAsync(conversationId);
        if (conversation is null || conversation.TenantId != currentTenant.TenantId)
            return Results.NotFound();

        if (!conversation.IsWindowOpen)
            return Results.BadRequest(new { error = "Window closed. Only templates allowed." });

        var idempotencyKey = request.IdempotencyKey ?? Guid.NewGuid().ToString();
        var message = Message.CreateOutbound(
            currentTenant.TenantId.Value,
            conversationId,
            conversation.ContactId,
            MessageType.Text,
            request.Content,
            idempotencyKey);

        await messageRepository.AddAsync(message);

        conversation.RecordMessage();
        await conversationRepository.UpdateAsync(conversation);

        // Notify clients via SignalR
        await hubContext.Clients.Group($"tenant:{currentTenant.TenantId}")
            .SendAsync(InboxHubMethods.NewMessage, new
            {
                id = message.Id,
                conversationId,
                direction = message.Direction.ToString(),
                content = message.Content,
                status = message.Status.ToString(),
                createdAt = message.CreatedAt
            });

        return Results.Ok(new { id = message.Id, status = message.Status.ToString() });
    }
}

public sealed record SendMessageRequest
{
    public string Content { get; init; } = string.Empty;
    public string? IdempotencyKey { get; init; }
}

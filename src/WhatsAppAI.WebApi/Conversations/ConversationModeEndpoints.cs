using Microsoft.AspNetCore.Mvc;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Messaging;
using WhatsAppAI.Infrastructure.Identity;

namespace WhatsAppAI.WebApi.Conversations;

public static class ConversationModeEndpoints
{
    public static IEndpointRouteBuilder MapConversationModeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/conversations/{conversationId:guid}/mode")
            .WithTags("Conversation Mode")
            .RequireAuthorization();

        group.MapPut("/", SwitchModeAsync)
            .WithName("SwitchConversationMode");

        return app;
    }

    private static async Task<IResult> SwitchModeAsync(
        Guid conversationId,
        [FromBody] SwitchModeRequest request,
        ICurrentTenant currentTenant,
        IConversationRepository conversationRepository,
        HttpContext httpContext)
    {
        if (currentTenant.TenantId is null || currentTenant.UserId is null)
            return Results.Unauthorized();

        var conversation = await conversationRepository.GetByIdAsync(conversationId);
        if (conversation is null || conversation.TenantId != currentTenant.TenantId)
            return Results.NotFound();

        // Optimistic concurrency via If-Match header
        var ifMatch = httpContext.Request.Headers["If-Match"].FirstOrDefault();
        if (ifMatch is not null && uint.TryParse(ifMatch, out var expectedVersion))
        {
            if (conversation.Version != expectedVersion)
                return Results.Conflict(new { error = "Version conflict. Conversation was modified." });
        }

        if (!Enum.TryParse<ConversationMode>(request.Mode, true, out var mode))
            return Results.BadRequest(new { error = "Invalid mode. Use: Automatic, Human, Paused" });

        conversation.SwitchMode(mode, currentTenant.UserId.ToString());
        await conversationRepository.UpdateAsync(conversation);

        return Results.Ok(new
        {
            id = conversation.Id,
            mode = conversation.Mode.ToString(),
            version = conversation.Version
        });
    }
}

public sealed record SwitchModeRequest
{
    public string Mode { get; init; } = string.Empty;
}

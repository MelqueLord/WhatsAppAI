using Microsoft.AspNetCore.Mvc;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain;
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
        IHandoffEventRepository handoffEventRepository,
        HttpContext httpContext)
    {
        if (currentTenant.TenantId is null || currentTenant.UserId is null)
            return Results.Unauthorized();

        var conversation = await conversationRepository.GetByIdAsync(conversationId);
        if (conversation is null || conversation.TenantId != currentTenant.TenantId)
            return Results.NotFound();

        var ifMatch = httpContext.Request.Headers["If-Match"].FirstOrDefault();
        if (ifMatch is null || !uint.TryParse(ifMatch, out var expectedVersion))
            return Results.BadRequest(new { error = "If-Match header with version is required." });

        if (!Enum.TryParse<ConversationMode>(request.Mode, true, out var mode))
            return Results.BadRequest(new { error = "Invalid mode. Use: Automatic, Human, Paused" });

        ConversationMode previousMode;
        try
        {
            previousMode = conversation.SwitchMode(mode, expectedVersion, currentTenant.UserId.ToString());
        }
        catch (ConcurrencyException)
        {
            return Results.Conflict(new { error = "Version conflict. Conversation was modified." });
        }

        await conversationRepository.UpdateAsync(conversation);

        var handoffEvent = HandoffEvent.Create(
            currentTenant.TenantId.Value,
            conversationId,
            previousMode,
            mode,
            currentTenant.UserId,
            request.Reason ?? "Mode changed by operator");

        await handoffEventRepository.AddAsync(handoffEvent);

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
    public string? Reason { get; init; }
}

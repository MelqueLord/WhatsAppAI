using Microsoft.AspNetCore.Mvc;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain;
using WhatsAppAI.Domain.Audit;
using WhatsAppAI.Domain.Messaging;
using WhatsAppAI.Infrastructure.Identity;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.WebApi.Conversations;

public static class ConversationModeEndpoints
{
    public static IEndpointRouteBuilder MapConversationModeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/conversations/{conversationId:guid}/mode")
            .WithTags("Conversation Mode")
            .RequireAuthorization("RequireTenantContext");

        group.MapPut("/", SwitchModeAsync)
            .WithName("SwitchConversationMode");

        return app;
    }

    private static async Task<IResult> SwitchModeAsync(
        Guid conversationId,
        [FromBody] SwitchModeRequest request,
        ICurrentTenant currentTenant,
        IConversationRepository conversationRepository,
        ITenantMembershipRepository membershipRepository,
        IHandoffEventRepository handoffEventRepository,
        IAuditLogRepository auditLogRepository,
        AppDbContext dbContext,
        HttpContext httpContext)
    {
        if (currentTenant.TenantId is null || currentTenant.UserId is null)
            return Results.Unauthorized();

        var conversation = await conversationRepository.GetByIdAsync(conversationId);
        if (conversation is null || conversation.TenantId != currentTenant.TenantId)
            return Results.NotFound();

        if (currentTenant.UserRole == "Operator")
        {
            var membership = await membershipRepository.GetByUserAndTenantAsync(
                currentTenant.UserId.Value, currentTenant.TenantId.Value);
            if (membership is null || !membership.CanAccessQueue(conversation.QueueId))
                return Results.Forbid();
        }

        var ifMatch = httpContext.Request.Headers["If-Match"].FirstOrDefault();
        if (ifMatch is null || !uint.TryParse(ifMatch, out var expectedVersion))
            return Results.BadRequest(new { error = "If-Match header with version is required." });

        if (!Enum.TryParse<ConversationMode>(request.Mode, true, out var mode))
            return Results.BadRequest(new { error = "Invalid mode. Use: Automatic, Human, Paused" });

        await using var transaction = await dbContext.Database.BeginTransactionAsync(httpContext.RequestAborted);
        ConversationMode previousMode;
        try
        {
            previousMode = conversation.SwitchMode(mode, expectedVersion, currentTenant.UserId.ToString());
            await conversationRepository.UpdateAsync(conversation, httpContext.RequestAborted);

            var reason = request.Reason ?? "Mode changed by operator";
            await handoffEventRepository.AddAsync(HandoffEvent.Create(
                currentTenant.TenantId.Value,
                conversationId,
                previousMode,
                mode,
                currentTenant.UserId,
                reason));
            await auditLogRepository.AddAsync(AuditLog.Create(
                currentTenant.TenantId.Value,
                currentTenant.UserId,
                "Conversation.ModeChanged",
                "Conversation",
                conversationId.ToString(),
                $"from={previousMode};to={mode}"),
                httpContext.RequestAborted);
            await transaction.CommitAsync(httpContext.RequestAborted);
        }
        catch (ConcurrencyException)
        {
            return Results.Conflict(new { error = "Version conflict. Conversation was modified." });
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            return Results.Conflict(new { error = "Version conflict. Conversation was modified." });
        }

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

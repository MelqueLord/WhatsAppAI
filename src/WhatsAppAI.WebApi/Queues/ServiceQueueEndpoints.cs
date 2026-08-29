using Microsoft.AspNetCore.Mvc;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Messaging;
using WhatsAppAI.Infrastructure.Identity;
using WhatsAppAI.WebApi;

namespace WhatsAppAI.WebApi.Queues;

public static class ServiceLineEndpoints
{
    public static IEndpointRouteBuilder MapServiceLineEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/service-queues")
            .WithTags("Service Queues")
            .RequireAuthorization("RequireTenantContext")
            .RequirePlanFeature(PlanFeature.AutomaticDistribution);

        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/{id:guid}", UpdateAsync);
        group.MapPost("/{id:guid}/deactivate", DeactivateAsync);
        group.MapPost("/{id:guid}/reactivate", ReactivateAsync);
        group.MapPost("/conversations/{conversationId:guid}/assign", AssignQueueAsync);
        group.MapPost("/conversations/{conversationId:guid}/unassign", UnassignQueueAsync);

        return app;
    }

    private static async Task<IResult> ListAsync(
        ICurrentTenant currentTenant, IServiceLineRepository repo)
    {
        if (currentTenant.TenantId is null) return Results.Unauthorized();
        var queues = await repo.GetByTenantAsync(currentTenant.TenantId.Value);
        return Results.Ok(queues.Select(q => new
        {
            id = q.Id, name = q.Name, description = q.Description,
            color = q.Color, sortOrder = q.SortOrder, isActive = q.IsActive,
            keywords = q.Keywords
        }));
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] CreateQueueRequest request, ICurrentTenant currentTenant,
        IServiceLineRepository repo)
    {
        if (currentTenant.TenantId is null) return Results.Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { error = "Name is required." });

        var queue = ServiceLine.Create(
            currentTenant.TenantId.Value, request.Name,
            request.Description, request.Color, request.SortOrder);
        queue.SetKeywords(request.Keywords);
        await repo.AddAsync(queue);
        return Results.Ok(new { id = queue.Id });
    }

    private static async Task<IResult> UpdateAsync(
        Guid id, [FromBody] UpdateQueueRequest request,
        ICurrentTenant currentTenant, IServiceLineRepository repo)
    {
        if (currentTenant.TenantId is null) return Results.Unauthorized();
        var queue = await repo.GetByIdAsync(id);
        if (queue is null || queue.TenantId != currentTenant.TenantId) return Results.NotFound();

        queue.Update(request.Name, request.Description, request.Color, request.SortOrder);
        queue.SetKeywords(request.Keywords);
        await repo.UpdateAsync(queue);
        return Results.Ok(new { id = queue.Id });
    }

    private static async Task<IResult> DeactivateAsync(
        Guid id, ICurrentTenant currentTenant, IServiceLineRepository repo)
    {
        if (currentTenant.TenantId is null) return Results.Unauthorized();
        var queue = await repo.GetByIdAsync(id);
        if (queue is null || queue.TenantId != currentTenant.TenantId) return Results.NotFound();

        queue.Deactivate();
        await repo.UpdateAsync(queue);
        return Results.Ok(new { id = queue.Id, isActive = false });
    }

    private static async Task<IResult> ReactivateAsync(
        Guid id, ICurrentTenant currentTenant, IServiceLineRepository repo)
    {
        if (currentTenant.TenantId is null) return Results.Unauthorized();
        var queue = await repo.GetByIdAsync(id);
        if (queue is null || queue.TenantId != currentTenant.TenantId) return Results.NotFound();

        queue.Activate();
        await repo.UpdateAsync(queue);
        return Results.Ok(new { id = queue.Id, isActive = true });
    }

    private static async Task<IResult> AssignQueueAsync(
        Guid conversationId, [FromBody] AssignQueueRequest request,
        ICurrentTenant currentTenant, IConversationRepository convRepo,
        IServiceLineRepository queueRepo,
        ITenantMembershipRepository membershipRepo)
    {
        if (currentTenant.TenantId is null) return Results.Unauthorized();
        var conversation = await convRepo.GetByIdAsync(conversationId);
        if (conversation is null || conversation.TenantId != currentTenant.TenantId) return Results.NotFound();
        if (!await OperatorCanAccessQueueAsync(currentTenant, membershipRepo, conversation.QueueId))
            return Results.Forbid();

        if (request.QueueId.HasValue)
        {
            var queue = await queueRepo.GetByIdAsync(request.QueueId.Value);
            if (queue is null || queue.TenantId != currentTenant.TenantId || !queue.IsActive)
                return Results.BadRequest(new { error = "Queue not found." });
        }

        conversation.AssignQueue(request.QueueId);
        if (request.QueueId.HasValue && conversation.Mode != ConversationMode.Human)
            conversation.SwitchMode(ConversationMode.Human, conversation.Version);
        await convRepo.UpdateAsync(conversation);
        return Results.Ok(new { conversationId, queueId = request.QueueId });
    }

    private static async Task<IResult> UnassignQueueAsync(
        Guid conversationId,
        ICurrentTenant currentTenant, IConversationRepository convRepo,
        ITenantMembershipRepository membershipRepo)
    {
        if (currentTenant.TenantId is null) return Results.Unauthorized();
        var conversation = await convRepo.GetByIdAsync(conversationId);
        if (conversation is null || conversation.TenantId != currentTenant.TenantId) return Results.NotFound();
        if (!await OperatorCanAccessQueueAsync(currentTenant, membershipRepo, conversation.QueueId))
            return Results.Forbid();

        conversation.AssignQueue(null);
        await convRepo.UpdateAsync(conversation);
        return Results.Ok(new { conversationId, queueId = (Guid?)null });
    }

    private static async Task<bool> OperatorCanAccessQueueAsync(
        ICurrentTenant currentTenant,
        ITenantMembershipRepository membershipRepository,
        Guid? queueId)
    {
        if (currentTenant.UserRole != "Operator") return true;
        if (currentTenant.TenantId is null || currentTenant.UserId is null) return false;

        var membership = await membershipRepository.GetByUserAndTenantAsync(
            currentTenant.UserId.Value, currentTenant.TenantId.Value);
        return membership?.CanAccessQueue(queueId) == true;
    }
}

public sealed record CreateQueueRequest(string Name, string? Description, string? Color, int SortOrder, string? Keywords);
public sealed record UpdateQueueRequest(string Name, string? Description, string? Color, int SortOrder, string? Keywords);
public sealed record AssignQueueRequest(Guid? QueueId);

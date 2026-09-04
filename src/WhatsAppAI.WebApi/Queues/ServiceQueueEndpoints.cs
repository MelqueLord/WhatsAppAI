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
            .RequireAuthorization("RequireTenantContext");

        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/{id:guid}", UpdateAsync);
        group.MapPost("/{id:guid}/deactivate", DeactivateAsync);
        group.MapPost("/{id:guid}/reactivate", ReactivateAsync);
        // Queue assignment is an operational feature and remains plan-gated.
        var operationalGroup = group.MapGroup("").RequirePlanFeature(PlanFeature.AutomaticDistribution);
        operationalGroup.MapPost("/conversations/{conversationId:guid}/assign", AssignQueueAsync);
        operationalGroup.MapPost("/conversations/{conversationId:guid}/unassign", UnassignQueueAsync);

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
            keywords = q.Keywords, transferNotice = q.TransferNotice
        }));
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] CreateQueueRequest request, ICurrentTenant currentTenant,
        IServiceLineRepository repo)
    {
        if (currentTenant.TenantId is null) return Results.Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { error = "Name is required." });
        if (request.TransferNotice?.Trim().Length > ServiceLine.TransferNoticeMaxLength)
            return Results.BadRequest(new { error = $"Transfer notice must contain at most {ServiceLine.TransferNoticeMaxLength} characters." });

        var queue = ServiceLine.Create(
            currentTenant.TenantId.Value, request.Name,
            request.Description, request.Color, request.SortOrder);
        queue.SetKeywords(request.Keywords);
        queue.SetTransferNotice(request.TransferNotice);
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
        if (request.TransferNotice?.Trim().Length > ServiceLine.TransferNoticeMaxLength)
            return Results.BadRequest(new { error = $"Transfer notice must contain at most {ServiceLine.TransferNoticeMaxLength} characters." });

        queue.Update(request.Name, request.Description, request.Color, request.SortOrder);
        queue.SetKeywords(request.Keywords);
        queue.SetTransferNotice(request.TransferNotice);
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

public sealed record CreateQueueRequest(string Name, string? Description, string? Color, int SortOrder, string? Keywords, string? TransferNotice);
public sealed record UpdateQueueRequest(string Name, string? Description, string? Color, int SortOrder, string? Keywords, string? TransferNotice);
public sealed record AssignQueueRequest(Guid? QueueId);

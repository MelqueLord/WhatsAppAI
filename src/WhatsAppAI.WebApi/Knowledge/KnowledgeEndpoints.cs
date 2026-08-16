using Microsoft.AspNetCore.Mvc;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain;
using WhatsAppAI.Domain.Knowledge;
using WhatsAppAI.Infrastructure.Identity;

namespace WhatsAppAI.WebApi.Knowledge;

public static class KnowledgeEndpoints
{
    public static IEndpointRouteBuilder MapKnowledgeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/knowledge")
            .WithTags("Knowledge")
            .RequireAuthorization("RequireTenantContext");

        group.MapGet("/", ListAsync)
            .WithName("ListKnowledge");

        group.MapPost("/", CreateAsync)
            .WithName("CreateKnowledge");

        group.MapPut("/{id:guid}", UpdateAsync)
            .WithName("UpdateKnowledge");

        group.MapPost("/{id:guid}/deactivate", DeactivateAsync)
            .WithName("DeactivateKnowledge");

        group.MapPost("/{id:guid}/reactivate", ReactivateAsync)
            .WithName("ReactivateKnowledge");

        return app;
    }

    private static async Task<IResult> ListAsync(
        ICurrentTenant currentTenant,
        IKnowledgeItemRepository repository)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();

        var items = await repository.GetByTenantAsync(currentTenant.TenantId.Value);
        return Results.Ok(items.Select(k => new
        {
            id = k.Id,
            title = k.Title,
            content = k.Content,
            priority = k.Priority,
            isActive = k.IsActive,
            version = k.Version,
            createdAt = k.CreatedAt,
            updatedAt = k.UpdatedAt
        }));
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] CreateKnowledgeRequest request,
        ICurrentTenant currentTenant,
        IKnowledgeItemRepository repository)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Title))
            return Results.BadRequest(new { error = "Title is required." });

        if (string.IsNullOrWhiteSpace(request.Content))
            return Results.BadRequest(new { error = "Content is required." });

        var item = KnowledgeItem.Create(
            currentTenant.TenantId.Value,
            request.Title,
            request.Content,
            request.Priority);

        await repository.AddAsync(item);

        return Results.Ok(new { id = item.Id, version = item.Version });
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        [FromBody] UpdateKnowledgeRequest request,
        ICurrentTenant currentTenant,
        IKnowledgeItemRepository repository,
        HttpContext httpContext)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();

        var item = await repository.GetByIdAsync(id);
        if (item is null || item.TenantId != currentTenant.TenantId)
            return Results.NotFound();

        var ifMatch = httpContext.Request.Headers["If-Match"].FirstOrDefault();
        if (ifMatch is null || !uint.TryParse(ifMatch, out var expectedVersion))
            return Results.BadRequest(new { error = "If-Match header with version is required." });

        try
        {
            item.Update(request.Title, request.Content, request.Priority, expectedVersion);
        }
        catch (ConcurrencyException)
        {
            return Results.Conflict(new { error = "Version conflict." });
        }

        await repository.UpdateAsync(item);
        return Results.Ok(new { version = item.Version });
    }

    private static async Task<IResult> DeactivateAsync(
        Guid id,
        ICurrentTenant currentTenant,
        IKnowledgeItemRepository repository,
        HttpContext httpContext)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();

        var item = await repository.GetByIdAsync(id);
        if (item is null || item.TenantId != currentTenant.TenantId)
            return Results.NotFound();

        var ifMatch = httpContext.Request.Headers["If-Match"].FirstOrDefault();
        if (ifMatch is null || !uint.TryParse(ifMatch, out var expectedVersion))
            return Results.BadRequest(new { error = "If-Match header with version is required." });

        try
        {
            item.Deactivate(expectedVersion);
        }
        catch (ConcurrencyException)
        {
            return Results.Conflict(new { error = "Version conflict." });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }

        await repository.UpdateAsync(item);
        return Results.Ok(new { version = item.Version, isActive = item.IsActive });
    }

    private static async Task<IResult> ReactivateAsync(
        Guid id,
        ICurrentTenant currentTenant,
        IKnowledgeItemRepository repository,
        HttpContext httpContext)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();

        var item = await repository.GetByIdAsync(id);
        if (item is null || item.TenantId != currentTenant.TenantId)
            return Results.NotFound();

        var ifMatch = httpContext.Request.Headers["If-Match"].FirstOrDefault();
        if (ifMatch is null || !uint.TryParse(ifMatch, out var expectedVersion))
            return Results.BadRequest(new { error = "If-Match header with version is required." });

        try
        {
            item.Reactivate(expectedVersion);
        }
        catch (ConcurrencyException)
        {
            return Results.Conflict(new { error = "Version conflict." });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }

        await repository.UpdateAsync(item);
        return Results.Ok(new { version = item.Version, isActive = item.IsActive });
    }
}

public sealed record CreateKnowledgeRequest
{
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public int Priority { get; init; }
}

public sealed record UpdateKnowledgeRequest
{
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public int Priority { get; init; }
}

using Microsoft.AspNetCore.Mvc;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Knowledge;
using WhatsAppAI.Domain.Messaging;
using WhatsAppAI.Infrastructure.Identity;

namespace WhatsAppAI.WebApi.Tags;

public static class ClientTagEndpoints
{
    public static IEndpointRouteBuilder MapClientTagEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/client-tags")
            .WithTags("Client Tags")
            .RequireAuthorization();

        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/{id:guid}", UpdateAsync);
        group.MapPost("/{id:guid}/deactivate", DeactivateAsync);
        group.MapPost("/{id:guid}/reactivate", ReactivateAsync);

        // Contact-Tag association
        group.MapPost("/contacts/{contactId:guid}/tags/{tagId:guid}", AssignTagAsync);
        group.MapDelete("/contacts/{contactId:guid}/tags/{tagId:guid}", RemoveTagAsync);
        group.MapGet("/contacts/{contactId:guid}/tags", GetContactTagsAsync);

        return app;
    }

    private static async Task<IResult> ListAsync(
        ICurrentTenant currentTenant, IClientTagRepository repo)
    {
        if (currentTenant.TenantId is null) return Results.Unauthorized();
        var tags = await repo.GetByTenantAsync(currentTenant.TenantId.Value);
        return Results.Ok(tags.Select(t => new
        {
            id = t.Id, name = t.Name, color = t.Color,
            description = t.Description, isActive = t.IsActive
        }));
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] CreateTagRequest request, ICurrentTenant currentTenant,
        IClientTagRepository repo)
    {
        if (currentTenant.TenantId is null) return Results.Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { error = "Name is required." });

        var tag = ClientTag.Create(currentTenant.TenantId.Value, request.Name, request.Color, request.Description);
        await repo.AddAsync(tag);
        return Results.Ok(new { id = tag.Id });
    }

    private static async Task<IResult> UpdateAsync(
        Guid id, [FromBody] UpdateTagRequest request,
        ICurrentTenant currentTenant, IClientTagRepository repo)
    {
        if (currentTenant.TenantId is null) return Results.Unauthorized();
        var tag = await repo.GetByIdAsync(id);
        if (tag is null || tag.TenantId != currentTenant.TenantId) return Results.NotFound();

        tag.Update(request.Name, request.Color, request.Description);
        await repo.UpdateAsync(tag);
        return Results.Ok(new { id = tag.Id });
    }

    private static async Task<IResult> DeactivateAsync(
        Guid id, ICurrentTenant currentTenant, IClientTagRepository repo)
    {
        if (currentTenant.TenantId is null) return Results.Unauthorized();
        var tag = await repo.GetByIdAsync(id);
        if (tag is null || tag.TenantId != currentTenant.TenantId) return Results.NotFound();

        tag.Deactivate();
        await repo.UpdateAsync(tag);
        return Results.Ok(new { id = tag.Id, isActive = false });
    }

    private static async Task<IResult> ReactivateAsync(
        Guid id, ICurrentTenant currentTenant, IClientTagRepository repo)
    {
        if (currentTenant.TenantId is null) return Results.Unauthorized();
        var tag = await repo.GetByIdAsync(id);
        if (tag is null || tag.TenantId != currentTenant.TenantId) return Results.NotFound();

        tag.Activate();
        await repo.UpdateAsync(tag);
        return Results.Ok(new { id = tag.Id, isActive = true });
    }

    private static async Task<IResult> AssignTagAsync(
        Guid contactId, Guid tagId,
        ICurrentTenant currentTenant, IContactTagRepository contactTagRepo,
        IClientTagRepository tagRepo, IContactRepository contactRepo)
    {
        if (currentTenant.TenantId is null) return Results.Unauthorized();

        var tag = await tagRepo.GetByIdAsync(tagId);
        if (tag is null || tag.TenantId != currentTenant.TenantId) return Results.NotFound();

        var contact = await contactRepo.GetByIdAsync(contactId);
        if (contact is null || contact.TenantId != currentTenant.TenantId) return Results.NotFound();

        if (await contactTagRepo.ExistsAsync(contactId, tagId))
            return Results.Ok(new { alreadyAssigned = true });

        var ct = ContactTag.Create(contactId, tagId, currentTenant.TenantId.Value);
        await contactTagRepo.AddAsync(ct);
        return Results.Ok(new { assigned = true });
    }

    private static async Task<IResult> RemoveTagAsync(
        Guid contactId, Guid tagId,
        ICurrentTenant currentTenant, IContactTagRepository repo)
    {
        if (currentTenant.TenantId is null) return Results.Unauthorized();
        await repo.RemoveAsync(contactId, tagId);
        return Results.Ok(new { removed = true });
    }

    private static async Task<IResult> GetContactTagsAsync(
        Guid contactId, ICurrentTenant currentTenant,
        IContactTagRepository contactTagRepo, IClientTagRepository tagRepo)
    {
        if (currentTenant.TenantId is null) return Results.Unauthorized();

        var contactTags = await contactTagRepo.GetByContactAsync(contactId);
        var tagIds = contactTags.Select(ct => ct.TagId).ToHashSet();

        var allTags = await tagRepo.GetActiveByTenantAsync(currentTenant.TenantId.Value);
        var result = allTags.Where(t => tagIds.Contains(t.Id)).Select(t => new
        {
            id = t.Id, name = t.Name, color = t.Color
        });

        return Results.Ok(result);
    }
}

public sealed record CreateTagRequest(string Name, string? Color, string? Description);
public sealed record UpdateTagRequest(string Name, string? Color, string? Description);

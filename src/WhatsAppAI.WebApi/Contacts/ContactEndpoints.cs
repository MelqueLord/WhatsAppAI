using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Messaging;
using WhatsAppAI.Infrastructure.Identity;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.WebApi.Contacts;

public static class ContactEndpoints
{
    public static IEndpointRouteBuilder MapContactEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/contacts")
            .WithTags("Contacts")
            .RequireAuthorization("RequireTenantContext");

        group.MapGet("/", ListContactsAsync)
            .WithName("ListContacts");

        group.MapPost("/", CreateContactAsync)
            .WithName("CreateContact");

        group.MapGet("/{contactId:guid}", GetContactAsync)
            .WithName("GetContact");

        group.MapPut("/{contactId:guid}", UpdateContactAsync)
            .WithName("UpdateContact");

        group.MapPost("/{contactId:guid}/start-conversation", StartConversationAsync)
            .WithName("StartConversation");

        return app;
    }

    private static async Task<IResult> ListContactsAsync(
        ICurrentTenant currentTenant,
        AppDbContext dbContext,
        string? search = null,
        Guid? queueId = null,
        int limit = 50)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();

        if (queueId.HasValue)
        {
            var queueExists = await dbContext.ServiceLines
                .AnyAsync(q =>
                    q.Id == queueId.Value &&
                    q.TenantId == currentTenant.TenantId.Value &&
                    q.IsActive);

            if (!queueExists)
                return Results.BadRequest(new { error = "Queue not found." });
        }

        var query = dbContext.Contacts
            .Where(c => c.TenantId == currentTenant.TenantId.Value);

        if (queueId.HasValue)
        {
            query = query.Where(c => dbContext.Conversations.Any(conversation =>
                conversation.TenantId == currentTenant.TenantId.Value &&
                conversation.ContactId == c.Id &&
                conversation.QueueId == queueId.Value &&
                conversation.Status == ConversationStatus.Open));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var likePattern = $"%{search}%";
            query = query.Where(c =>
                (c.Name != null && EF.Functions.Like(c.Name, likePattern)) ||
                EF.Functions.Like(c.PhoneNumber, likePattern));
        }

        var contacts = await query
            .OrderByDescending(c => c.LastMessageAt)
            .ThenByDescending(c => c.CreatedAt)
            .Take(limit)
            .Select(c => new ContactResponse
            {
                Id = c.Id,
                PhoneNumber = c.PhoneNumber,
                Name = c.Name,
                ProfilePictureUrl = c.ProfilePictureUrl,
                LastMessageAt = c.LastMessageAt,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync();

        return Results.Ok(contacts);
    }

    private static async Task<IResult> CreateContactAsync(
        [FromBody] CreateContactRequest request,
        ICurrentTenant currentTenant,
        AppDbContext dbContext)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
            return Results.BadRequest(new { error = "Phone number is required." });

        var phone = request.PhoneNumber.Trim();

        // Check if contact already exists
        var existing = await dbContext.Contacts
            .FirstOrDefaultAsync(c =>
                c.TenantId == currentTenant.TenantId.Value &&
                c.PhoneNumber == phone);

        if (existing is not null)
        {
            // Update name if provided
            if (!string.IsNullOrWhiteSpace(request.Name) && existing.Name != request.Name)
            {
                existing.UpdateName(request.Name);
                await dbContext.SaveChangesAsync();
            }

            return Results.Ok(new ContactResponse
            {
                Id = existing.Id,
                PhoneNumber = existing.PhoneNumber,
                Name = existing.Name,
                CreatedAt = existing.CreatedAt,
                Message = "Contact already exists"
            });
        }

        var contact = Contact.Create(
            currentTenant.TenantId.Value,
            phone,
            request.Name?.Trim());

        dbContext.Contacts.Add(contact);
        await dbContext.SaveChangesAsync();

        // Start conversation if requested
        if (request.StartConversation)
        {
            var conversation = Conversation.Create(
                currentTenant.TenantId.Value,
                contact.Id,
                "manual",
                ConversationMode.Human);
            conversation.RecordMessage();

            dbContext.Conversations.Add(conversation);
            await dbContext.SaveChangesAsync();

            return Results.Created($"/api/contacts/{contact.Id}", new ContactResponse
            {
                Id = contact.Id,
                PhoneNumber = contact.PhoneNumber,
                Name = contact.Name,
                CreatedAt = contact.CreatedAt,
                ConversationId = conversation.Id,
                Message = "Contact created and conversation started"
            });
        }

        return Results.Created($"/api/contacts/{contact.Id}", new ContactResponse
        {
            Id = contact.Id,
            PhoneNumber = contact.PhoneNumber,
            Name = contact.Name,
            CreatedAt = contact.CreatedAt
        });
    }

    private static async Task<IResult> GetContactAsync(
        Guid contactId,
        ICurrentTenant currentTenant,
        AppDbContext dbContext)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();

        var contact = await dbContext.Contacts
            .FirstOrDefaultAsync(c =>
                c.Id == contactId &&
                c.TenantId == currentTenant.TenantId.Value);

        if (contact is null)
            return Results.NotFound();

        return Results.Ok(new ContactResponse
        {
            Id = contact.Id,
            PhoneNumber = contact.PhoneNumber,
            Name = contact.Name,
            ProfilePictureUrl = contact.ProfilePictureUrl,
            LastMessageAt = contact.LastMessageAt,
            CreatedAt = contact.CreatedAt
        });
    }

    private static async Task<IResult> UpdateContactAsync(
        Guid contactId,
        [FromBody] UpdateContactRequest request,
        ICurrentTenant currentTenant,
        AppDbContext dbContext)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();

        var contact = await dbContext.Contacts
            .FirstOrDefaultAsync(c =>
                c.Id == contactId &&
                c.TenantId == currentTenant.TenantId.Value);

        if (contact is null)
            return Results.NotFound();

        if (!string.IsNullOrWhiteSpace(request.Name))
            contact.UpdateName(request.Name);

        if (request.ProfilePictureUrl != null)
            contact.UpdateProfilePicture(request.ProfilePictureUrl);

        await dbContext.SaveChangesAsync();

        return Results.Ok(new ContactResponse
        {
            Id = contact.Id,
            PhoneNumber = contact.PhoneNumber,
            Name = contact.Name,
            ProfilePictureUrl = contact.ProfilePictureUrl,
            CreatedAt = contact.CreatedAt
        });
    }

    private static async Task<IResult> StartConversationAsync(
        Guid contactId,
        ICurrentTenant currentTenant,
        AppDbContext dbContext)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();

        var contact = await dbContext.Contacts
            .FirstOrDefaultAsync(c =>
                c.Id == contactId &&
                c.TenantId == currentTenant.TenantId.Value);

        if (contact is null)
            return Results.NotFound();

        // Check if conversation already exists
        var existingConversation = await dbContext.Conversations
            .FirstOrDefaultAsync(c =>
                c.ContactId == contactId &&
                c.TenantId == currentTenant.TenantId.Value &&
                c.Status == ConversationStatus.Open);

        if (existingConversation is not null)
            return Results.Ok(new { conversationId = existingConversation.Id, message = "Conversation already exists" });

        var conversation = Conversation.Create(
            currentTenant.TenantId.Value,
            contact.Id,
            "manual",
            ConversationMode.Human);
        conversation.RecordMessage();

        dbContext.Conversations.Add(conversation);
        await dbContext.SaveChangesAsync();

        return Results.Ok(new { conversationId = conversation.Id, message = "Conversation started" });
    }
}

public sealed class CreateContactRequest
{
    public string PhoneNumber { get; init; } = string.Empty;
    public string? Name { get; init; }
    public bool StartConversation { get; init; }
}

public sealed class UpdateContactRequest
{
    public string? Name { get; init; }
    public string? ProfilePictureUrl { get; init; }
}

public sealed class ContactResponse
{
    public Guid Id { get; init; }
    public string PhoneNumber { get; init; } = string.Empty;
    public string? Name { get; init; }
    public string? ProfilePictureUrl { get; init; }
    public DateTime? LastMessageAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public Guid? ConversationId { get; init; }
    public string? Message { get; init; }
}

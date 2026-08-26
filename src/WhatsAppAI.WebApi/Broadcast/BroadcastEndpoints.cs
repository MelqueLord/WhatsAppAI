using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Broadcast;
using WhatsAppAI.Domain.Broadcast;
using WhatsAppAI.Domain.Integrations;
using WhatsAppAI.Infrastructure.Identity;
using WhatsAppAI.Infrastructure.Persistence;
using WhatsAppAI.WebApi.Hubs;

namespace WhatsAppAI.WebApi.Broadcast;

public static class BroadcastEndpoints
{
    public static IEndpointRouteBuilder MapBroadcastEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/broadcasts")
            .WithTags("Broadcast")
            .RequireAuthorization("RequireTenantContext");

        group.MapGet("/", ListAsync).WithName("ListBroadcasts");
        group.MapGet("/{id:guid}", GetAsync).WithName("GetBroadcast");
        group.MapPost("/", CreateAsync).WithName("CreateBroadcast");
        group.MapPost("/{id:guid}/dispatch", DispatchAsync).WithName("DispatchBroadcast");
        group.MapPost("/{id:guid}/cancel", CancelAsync).WithName("CancelBroadcast");
        group.MapDelete("/{id:guid}", DeleteAsync).WithName("DeleteBroadcast");

        return app;
    }

    // GET /api/broadcasts
    private static async Task<IResult> ListAsync(
        ICurrentTenant currentTenant,
        IBroadcastRepository broadcastRepo)
    {
        if (currentTenant.TenantId is null) return Results.Unauthorized();

        var broadcasts = await broadcastRepo.GetByTenantAsync(currentTenant.TenantId.Value);
        return Results.Ok(broadcasts.Select(ToDto));
    }

    // GET /api/broadcasts/{id}
    private static async Task<IResult> GetAsync(
        Guid id,
        ICurrentTenant currentTenant,
        IBroadcastRepository broadcastRepo,
        AppDbContext db)
    {
        if (currentTenant.TenantId is null) return Results.Unauthorized();

        var broadcast = await broadcastRepo.GetByIdAsync(id);
        if (broadcast is null || broadcast.TenantId != currentTenant.TenantId)
            return Results.NotFound();

        var recipients = await db.BroadcastRecipients
            .IgnoreQueryFilters()
            .Where(r => r.BroadcastListId == id)
            .Select(r => new { r.Id, r.ContactId, r.Status, r.ErrorMessage, r.SentAt })
            .ToListAsync();

        return Results.Ok(new
        {
            broadcast = ToDto(broadcast),
            recipients
        });
    }

    // POST /api/broadcasts
    private static async Task<IResult> CreateAsync(
        [FromBody] CreateBroadcastRequest request,
        ICurrentTenant currentTenant,
        IBroadcastRepository broadcastRepo,
        IContactRepository contactRepo,
        AppDbContext db)
    {
        if (currentTenant.TenantId is null || currentTenant.UserId is null)
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { error = "Name is required." });

        if (string.IsNullOrWhiteSpace(request.Message))
            return Results.BadRequest(new { error = "Message is required." });

        if (request.Message.Length > 4096)
            return Results.BadRequest(new { error = "Message must be at most 4096 characters." });

        if (request.ContactIds is null || request.ContactIds.Count == 0)
            return Results.BadRequest(new { error = "At least one recipient required." });

        if (request.ContactIds.Count > 500)
            return Results.BadRequest(new { error = "Maximum 500 recipients per broadcast." });

        // Validate all contacts belong to this tenant
        var contactIds = request.ContactIds.Distinct().ToList();
        var tenantId = currentTenant.TenantId.Value;

        var validContacts = await db.Contacts
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId && contactIds.Contains(c.Id))
            .Select(c => c.Id)
            .ToListAsync();

        if (validContacts.Count != contactIds.Count)
            return Results.BadRequest(new { error = "One or more contacts not found." });

        var broadcast = BroadcastList.Create(
            tenantId,
            request.Name,
            request.Message,
            currentTenant.UserId.Value);

        await broadcastRepo.AddAsync(broadcast);

        var recipients = contactIds
            .Select(cid => BroadcastRecipient.Create(tenantId, broadcast.Id, cid))
            .ToList();

        await broadcastRepo.AddRecipientsAsync(recipients);

        return Results.Created($"/api/broadcasts/{broadcast.Id}", ToDto(broadcast));
    }

    // POST /api/broadcasts/{id}/dispatch
    private static async Task<IResult> DispatchAsync(
        Guid id,
        [FromBody] DispatchBroadcastRequest request,
        ICurrentTenant currentTenant,
        IBroadcastRepository broadcastRepo,
        IWhatsAppAccountRepository accountRepo,
        ITenantMembershipRepository membershipRepo,
        AppDbContext dbContext,
        IHubContext<InboxHub> hub)
    {
        if (currentTenant.TenantId is null) return Results.Unauthorized();

        var tenantId = currentTenant.TenantId.Value;

        var broadcast = await broadcastRepo.GetByIdAsync(id);
        if (broadcast is null || broadcast.TenantId != tenantId)
            return Results.NotFound();

        if (broadcast.Status != BroadcastStatus.Draft)
            return Results.BadRequest(new { error = "Only draft broadcasts can be dispatched." });

        // BR-BC-001: only QR Code lines
        var accounts = await accountRepo.GetAllByTenantAsync(tenantId);
        var line = accounts.FirstOrDefault(a =>
            a.PhoneNumberId == request.LinePhoneNumberId
            && a.ConnectionType == WhatsAppConnectionType.QrCode
            && a.IsActive);

        if (line is null)
            return Results.BadRequest(new { error = "QR Code line not found or not active." });

        if (currentTenant.UserRole == "Operator")
        {
            if (currentTenant.UserId is null)
                return Results.Forbid();

            var membership = await membershipRepo.GetByUserAndTenantAsync(
                currentTenant.UserId.Value,
                tenantId);
            membership?.LoadAssignedLinesFromJson();

            var hasAssignedLine = membership is not null &&
                (membership.AssignedLines.Any(assigned =>
                    assigned.ConnectionType == WhatsAppConnectionType.QrCode &&
                    assigned.LineNumber == line.LineNumber) ||
                 (membership.AssignedLines.Count == 0 &&
                  membership.AssignedConnectionType == WhatsAppConnectionType.QrCode &&
                  membership.AssignedLineNumber == line.LineNumber));

            if (!hasAssignedLine)
                return Results.Forbid();
        }

        // BR-BC-005: only one active broadcast per tenant
        var active = await broadcastRepo.GetActiveSendingAsync(tenantId);
        if (active is not null)
            return Results.BadRequest(new { error = "There is already a broadcast in progress." });

        // Count pending recipients for this broadcast
        var pendingRecipients = await broadcastRepo.GetPendingRecipientsAsync(broadcast.Id, 500);
        var totalCount = pendingRecipients.Count;

        if (totalCount == 0)
            return Results.BadRequest(new { error = "No recipients found for this broadcast." });

        Guid? queueId = null;
        if (request.QueueId.HasValue)
        {
            var queueExists = await dbContext.ServiceLines
                .AnyAsync(q => q.Id == request.QueueId.Value && q.TenantId == tenantId && q.IsActive);
            if (!queueExists)
                return Results.BadRequest(new { error = "Queue not found or does not belong to this tenant." });
            queueId = request.QueueId.Value;
        }

        broadcast.StartDispatch(request.LinePhoneNumberId, totalCount, queueId);
        await broadcastRepo.UpdateAsync(broadcast);

        // Notify via SignalR
        await hub.Clients.Group($"tenant:{tenantId}")
            .SendAsync(BroadcastHubEvents.BroadcastUpdated, ToDto(broadcast));

        return Results.Ok(ToDto(broadcast));
    }

    // POST /api/broadcasts/{id}/cancel
    private static async Task<IResult> CancelAsync(
        Guid id,
        ICurrentTenant currentTenant,
        IBroadcastRepository broadcastRepo,
        IHubContext<InboxHub> hub)
    {
        if (currentTenant.TenantId is null) return Results.Unauthorized();

        var broadcast = await broadcastRepo.GetByIdAsync(id);
        if (broadcast is null || broadcast.TenantId != currentTenant.TenantId)
            return Results.NotFound();

        broadcast.Cancel();
        await broadcastRepo.UpdateAsync(broadcast);

        await hub.Clients.Group($"tenant:{currentTenant.TenantId}")
            .SendAsync(BroadcastHubEvents.BroadcastUpdated, ToDto(broadcast));

        return Results.Ok(ToDto(broadcast));
    }

    // DELETE /api/broadcasts/{id}
    private static async Task<IResult> DeleteAsync(
        Guid id,
        ICurrentTenant currentTenant,
        IBroadcastRepository broadcastRepo)
    {
        if (currentTenant.TenantId is null) return Results.Unauthorized();

        var broadcast = await broadcastRepo.GetByIdAsync(id);
        if (broadcast is null || broadcast.TenantId != currentTenant.TenantId)
            return Results.NotFound();

        if (broadcast.Status == BroadcastStatus.Sending)
            return Results.BadRequest(new { error = "Cancel the broadcast before deleting." });

        // Soft-cancel before delete for audit
        broadcast.Cancel();
        await broadcastRepo.UpdateAsync(broadcast);

        return Results.NoContent();
    }

    private static object ToDto(BroadcastList b) => new
    {
        id = b.Id,
        name = b.Name,
        message = b.Message,
        status = b.Status.ToString(),
        linePhoneNumberId = b.LinePhoneNumberId,
        queueId = b.QueueId,
        totalCount = b.TotalCount,
        sentCount = b.SentCount,
        failedCount = b.FailedCount,
        createdAt = b.CreatedAt,
        startedAt = b.StartedAt,
        finishedAt = b.FinishedAt,
    };
}

public sealed record CreateBroadcastRequest
{
    public string Name { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public List<Guid> ContactIds { get; init; } = [];
}

public sealed record DispatchBroadcastRequest
{
    public string LinePhoneNumberId { get; init; } = string.Empty;
    public Guid? QueueId { get; init; }
}

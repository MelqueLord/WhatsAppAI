using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Broadcast;
using WhatsAppAI.Application.Integrations;
using WhatsAppAI.Domain.Broadcast;
using WhatsAppAI.Domain.Integrations;
using WhatsAppAI.Infrastructure.Identity;
using WhatsAppAI.Infrastructure.Persistence;
using WhatsAppAI.WebApi.Contacts;
using WhatsAppAI.WebApi.Hubs;

namespace WhatsAppAI.WebApi.Broadcast;

public static class BroadcastEndpoints
{
    private const int ManualRecipientLimit = 500;
    private const int RecipientInsertBatchSize = 500;

    public static IEndpointRouteBuilder MapBroadcastEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/broadcasts")
            .WithTags("Broadcast")
            .RequireAuthorization("RequireTenantContext");

        group.MapGet("/", ListAsync).WithName("ListBroadcasts");
        group.MapGet("/{id:guid}", GetAsync).WithName("GetBroadcast");
        group.MapPost("/", CreateAsync).WithName("CreateBroadcast");
        group.MapGet("/official-templates", ListOfficialTemplatesAsync).WithName("ListOfficialBroadcastTemplates");
        group.MapPut("/{id:guid}", UpdateAsync).WithName("UpdateBroadcast");
        group.MapPost("/{id:guid}/dispatch", DispatchAsync).WithName("DispatchBroadcast");
        group.MapPost("/{id:guid}/retry-failed", RetryFailedAsync).WithName("RetryFailedBroadcast");
        group.MapPost("/{id:guid}/cancel", CancelAsync).WithName("CancelBroadcast");
        group.MapDelete("/{id:guid}", DeleteAsync).WithName("DeleteBroadcast");

        return app;
    }

    // GET /api/broadcasts
    private static async Task<IResult> ListAsync(
        ICurrentTenant currentTenant,
        IBroadcastRepository broadcastRepo,
        IWhatsAppAccountRepository accountRepository,
        ITenantMembershipRepository membershipRepository)
    {
        if (currentTenant.TenantId is null) return Results.Unauthorized();

        var broadcasts = await broadcastRepo.GetByTenantAsync(currentTenant.TenantId.Value);
        var visible = new List<BroadcastList>();
        foreach (var broadcast in broadcasts)
        {
            if (await CanAccessBroadcastAsync(currentTenant, broadcast, accountRepository, membershipRepository))
                visible.Add(broadcast);
        }
        return Results.Ok(visible.Select(ToDto));
    }

    // GET /api/broadcasts/{id}
    private static async Task<IResult> GetAsync(
        Guid id,
        ICurrentTenant currentTenant,
        IBroadcastRepository broadcastRepo,
        IWhatsAppAccountRepository accountRepository,
        ITenantMembershipRepository membershipRepository,
        AppDbContext db)
    {
        if (currentTenant.TenantId is null) return Results.Unauthorized();

        var broadcast = await broadcastRepo.GetByIdAsync(id);
        if (broadcast is null || broadcast.TenantId != currentTenant.TenantId)
            return Results.NotFound();
        if (!await CanAccessBroadcastAsync(currentTenant, broadcast, accountRepository, membershipRepository))
            return Results.NotFound();

        var recipients = await db.BroadcastRecipients
            .IgnoreQueryFilters()
            .Where(r => r.BroadcastListId == id && r.TenantId == currentTenant.TenantId)
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
        IWhatsAppAccountRepository accountRepository,
        ITenantMembershipRepository membershipRepository,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null || currentTenant.UserId is null)
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { error = "Name is required." });

        if (request.DeliveryMode == BroadcastDeliveryMode.QrCodeText && string.IsNullOrWhiteSpace(request.Message))
            return Results.BadRequest(new { error = "Message is required." });

        if (request.Message.Length > 4096)
            return Results.BadRequest(new { error = "Message must be at most 4096 characters." });

        var templateBodyParameters = request.TemplateBodyParameters ?? [];
        if (request.DeliveryMode == BroadcastDeliveryMode.OfficialApiTemplate &&
            (string.IsNullOrWhiteSpace(request.LinePhoneNumberId) || string.IsNullOrWhiteSpace(request.TemplateName) ||
             string.IsNullOrWhiteSpace(request.TemplateLanguage) || templateBodyParameters.Count > 10 ||
             templateBodyParameters.Exists(value => value is null || value.Length > 1024)))
            return Results.BadRequest(new { error = "An official template broadcast requires a line, template, language and valid body parameters." });

        var tenantId = currentTenant.TenantId.Value;
        Guid? queueId = null;
        List<Guid> contactIds;

        if (request.QueueId.HasValue)
        {
            var queueExists = await db.ServiceLines
                .AnyAsync(
                    q => q.Id == request.QueueId.Value && q.TenantId == tenantId && q.IsActive,
                    cancellationToken);
            if (!queueExists)
                return Results.BadRequest(new { error = "Queue not found or does not belong to this tenant." });

            if (request.ContactIds.Count == 0)
            {
                contactIds = await QueueContactQuery.ForQueue(db, tenantId, request.QueueId.Value)
                    .OrderBy(c => c.Id)
                    .Select(c => c.Id)
                    .ToListAsync(cancellationToken);

                if (contactIds.Count == 0)
                    return Results.BadRequest(new { error = "No contacts found for this queue." });
            }
            else
            {
                contactIds = request.ContactIds.Distinct().ToList();
                if (contactIds.Count > ManualRecipientLimit)
                    return Results.BadRequest(new { error = $"Maximum {ManualRecipientLimit} manually selected recipients per broadcast." });

                var validContacts = await QueueContactQuery.ForQueue(db, tenantId, request.QueueId.Value)
                    .Where(c => contactIds.Contains(c.Id))
                    .CountAsync(cancellationToken);

                if (validContacts != contactIds.Count)
                    return Results.BadRequest(new { error = "One or more contacts do not belong to this queue." });
            }

            queueId = request.QueueId.Value;
        }
        else
        {
            contactIds = request.ContactIds.Distinct().ToList();
            if (contactIds.Count == 0)
                return Results.BadRequest(new { error = "At least one recipient required." });
            if (contactIds.Count > ManualRecipientLimit)
                return Results.BadRequest(new { error = $"Maximum {ManualRecipientLimit} manually selected recipients per broadcast." });

            var validContacts = await db.Contacts
                .IgnoreQueryFilters()
                .Where(c => c.TenantId == tenantId && contactIds.Contains(c.Id))
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);

            if (validContacts.Count != contactIds.Count)
                return Results.BadRequest(new { error = "One or more contacts not found." });
        }

        if (currentTenant.UserRole == "Operator")
        {
            var membership = await membershipRepository.GetByUserAndTenantAsync(currentTenant.UserId.Value, tenantId, cancellationToken);
            membership?.LoadAssignedLinesFromJson();
            if (membership is null || !membership.CanAccessQueue(queueId)) return Results.Forbid();
            if (request.DeliveryMode == BroadcastDeliveryMode.OfficialApiTemplate)
            {
                var account = await accountRepository.GetByTenantAndPhoneNumberIdAsync(tenantId, request.LinePhoneNumberId!, cancellationToken);
                var hasLine = account is not null &&
                    (membership.AssignedLines.Any(assignment => assignment.ConnectionType == WhatsAppConnectionType.OfficialApi && assignment.LineNumber == account.LineNumber) ||
                     (membership.AssignedLines.Count == 0 && membership.AssignedConnectionType == WhatsAppConnectionType.OfficialApi && membership.AssignedLineNumber == account.LineNumber));
                if (!hasLine) return Results.Forbid();
            }
        }

        var broadcast = BroadcastList.Create(
            tenantId,
            request.Name,
            request.Message,
            currentTenant.UserId.Value,
            queueId,
            request.DeliveryMode,
            request.TemplateName,
            request.TemplateLanguage,
            request.DeliveryMode == BroadcastDeliveryMode.OfficialApiTemplate ? JsonSerializer.Serialize(templateBodyParameters) : null,
            request.LinePhoneNumberId);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await broadcastRepo.AddAsync(broadcast);

        foreach (var recipientBatch in contactIds.Chunk(RecipientInsertBatchSize))
        {
            var recipients = recipientBatch
                .Select(contactId => BroadcastRecipient.Create(tenantId, broadcast.Id, contactId));
            await broadcastRepo.AddRecipientsAsync(recipients);
        }

        await transaction.CommitAsync(cancellationToken);

        return Results.Created($"/api/broadcasts/{broadcast.Id}", ToDto(broadcast));
    }

    // PUT /api/broadcasts/{id}
    private static async Task<IResult> UpdateAsync(
        Guid id,
        [FromBody] UpdateBroadcastRequest request,
        ICurrentTenant currentTenant,
        IBroadcastRepository broadcastRepo,
        IWhatsAppAccountRepository accountRepository,
        ITenantMembershipRepository membershipRepository)
    {
        if (currentTenant.TenantId is null) return Results.Unauthorized();

        var broadcast = await broadcastRepo.GetByIdAsync(id);
        if (broadcast is null || broadcast.TenantId != currentTenant.TenantId)
            return Results.NotFound();
        if (!await CanAccessBroadcastAsync(currentTenant, broadcast, accountRepository, membershipRepository))
            return Results.NotFound();

        if (string.IsNullOrWhiteSpace(request.Message))
            return Results.BadRequest(new { error = "Message is required." });
        if (request.Message.Length > 4096)
            return Results.BadRequest(new { error = "Message must be at most 4096 characters." });

        try
        {
            broadcast.UpdateMessage(request.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }

        await broadcastRepo.UpdateAsync(broadcast);
        return Results.Ok(ToDto(broadcast));
    }

    // POST /api/broadcasts/{id}/dispatch
    private static async Task<IResult> DispatchAsync(
        Guid id,
        [FromBody] DispatchBroadcastRequest request,
        ICurrentTenant currentTenant,
        IBroadcastRepository broadcastRepo,
        IWhatsAppAccountRepository accountRepo,
        ITenantMembershipRepository membershipRepo,
        ISecretStore secretStore,
        IWhatsAppClientResolver whatsAppClientResolver,
        IHubContext<InboxHub> hub)
    {
        if (currentTenant.TenantId is null) return Results.Unauthorized();

        var tenantId = currentTenant.TenantId.Value;

        var broadcast = await broadcastRepo.GetByIdAsync(id);
        if (broadcast is null || broadcast.TenantId != tenantId)
            return Results.NotFound();

        if (broadcast.Status != BroadcastStatus.Draft)
            return Results.BadRequest(new { error = "Only draft broadcasts can be dispatched." });

        var expectedConnectionType = broadcast.DeliveryMode == BroadcastDeliveryMode.OfficialApiTemplate
            ? WhatsAppConnectionType.OfficialApi : WhatsAppConnectionType.QrCode;
        var requestedLine = broadcast.DeliveryMode == BroadcastDeliveryMode.OfficialApiTemplate
            ? broadcast.LinePhoneNumberId : request.LinePhoneNumberId;
        var accounts = await accountRepo.GetAllByTenantAsync(tenantId);
        var line = accounts.FirstOrDefault(a =>
            a.PhoneNumberId == requestedLine
            && a.ConnectionType == expectedConnectionType
            && a.IsActive);

        if (line is null)
            return Results.BadRequest(new { error = "Selected WhatsApp line not found or not active." });

        if (broadcast.DeliveryMode == BroadcastDeliveryMode.OfficialApiTemplate)
        {
            var token = await secretStore.GetAsync(line.AccessTokenRef);
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(line.WabaId))
                return Results.BadRequest(new { error = "Official API credentials are not available." });
            var templates = await whatsAppClientResolver.GetClient(WhatsAppConnectionType.OfficialApi)
                .ListTemplatesAsync(line.WabaId, token);
            var parameterCount = string.IsNullOrWhiteSpace(broadcast.TemplateParametersJson) ? 0 :
                JsonSerializer.Deserialize<List<string>>(broadcast.TemplateParametersJson)?.Count ?? 0;
            if (!templates.IsSuccess || !templates.Templates.Any(template =>
                template.Name == broadcast.TemplateName && template.Language == broadcast.TemplateLanguage &&
                template.CanSendInBroadcast &&
                template.BodyParameterCount == parameterCount))
                return Results.BadRequest(new { error = "The selected template is no longer eligible for sending." });
        }

        if (currentTenant.UserRole == "Operator")
        {
            if (currentTenant.UserId is null)
                return Results.Forbid();

            var membership = await membershipRepo.GetByUserAndTenantAsync(
                currentTenant.UserId.Value,
                tenantId,
                CancellationToken.None);
            membership?.LoadAssignedLinesFromJson();

            if (membership is null || !membership.CanAccessQueue(broadcast.QueueId))
                return Results.Forbid();

            var hasAssignedLine = membership.AssignedLines.Any(assigned =>
                    assigned.ConnectionType == expectedConnectionType &&
                    assigned.LineNumber == line.LineNumber) ||
                 (membership.AssignedLines.Count == 0 &&
                  membership.AssignedConnectionType == expectedConnectionType &&
                  membership.AssignedLineNumber == line.LineNumber);

            if (!hasAssignedLine)
                return Results.Forbid();
        }

        // BR-BC-005: only one active broadcast per tenant
        var active = await broadcastRepo.GetActiveSendingAsync(tenantId);
        if (active is not null)
            return Results.BadRequest(new { error = "There is already a broadcast in progress." });

        var totalCount = await broadcastRepo.CountPendingRecipientsAsync(broadcast.Id);

        if (totalCount == 0)
            return Results.BadRequest(new { error = "No recipients found for this broadcast." });

        broadcast.StartDispatch(requestedLine, totalCount);
        await broadcastRepo.UpdateAsync(broadcast);

        // Notify via SignalR
        await hub.Clients.Group($"tenant:{tenantId}")
            .SendAsync(BroadcastHubEvents.BroadcastUpdated, ToDto(broadcast));

        return Results.Ok(ToDto(broadcast));
    }

    // POST /api/broadcasts/{id}/retry-failed
    private static async Task<IResult> RetryFailedAsync(
        Guid id,
        ICurrentTenant currentTenant,
        IBroadcastRepository broadcastRepo,
        IWhatsAppAccountRepository accountRepo,
        ITenantMembershipRepository membershipRepo,
        IHubContext<InboxHub> hub)
    {
        if (currentTenant.TenantId is null) return Results.Unauthorized();

        var tenantId = currentTenant.TenantId.Value;
        var broadcast = await broadcastRepo.GetByIdAsync(id);
        if (broadcast is null || broadcast.TenantId != tenantId)
            return Results.NotFound();

        if (broadcast.Status != BroadcastStatus.Completed)
            return Results.BadRequest(new { error = "Only completed broadcasts can retry failed recipients." });

        var active = await broadcastRepo.GetActiveSendingAsync(tenantId);
        if (active is not null)
            return Results.BadRequest(new { error = "There is already a broadcast in progress." });

        var expectedConnectionType = broadcast.DeliveryMode == BroadcastDeliveryMode.OfficialApiTemplate
            ? WhatsAppConnectionType.OfficialApi : WhatsAppConnectionType.QrCode;
        var accounts = await accountRepo.GetAllByTenantAsync(tenantId);
        var line = accounts.FirstOrDefault(a =>
            a.PhoneNumberId == broadcast.LinePhoneNumberId
            && a.ConnectionType == expectedConnectionType
            && a.IsActive);

        if (line is null)
            return Results.BadRequest(new { error = "The broadcast line is not active." });

        if (currentTenant.UserRole == "Operator")
        {
            if (currentTenant.UserId is null)
                return Results.Forbid();

            var membership = await membershipRepo.GetByUserAndTenantAsync(
                currentTenant.UserId.Value,
                tenantId,
                CancellationToken.None);
            membership?.LoadAssignedLinesFromJson();

            if (membership is null || !membership.CanAccessQueue(broadcast.QueueId))
                return Results.Forbid();

            var hasAssignedLine = membership.AssignedLines.Any(assigned =>
                    assigned.ConnectionType == expectedConnectionType &&
                    assigned.LineNumber == line.LineNumber) ||
                 (membership.AssignedLines.Count == 0 &&
                  membership.AssignedConnectionType == expectedConnectionType &&
                  membership.AssignedLineNumber == line.LineNumber);

            if (!hasAssignedLine)
                return Results.Forbid();
        }

        var failedRecipients = await broadcastRepo.GetFailedRecipientsAsync(id);
        if (failedRecipients.Count == 0)
            return Results.BadRequest(new { error = "No failed recipients found for this broadcast." });

        foreach (var recipient in failedRecipients)
            recipient.Retry();

        broadcast.PrepareRetry(failedRecipients.Count);
        await broadcastRepo.UpdateAsync(broadcast);

        await hub.Clients.Group($"tenant:{tenantId}")
            .SendAsync(BroadcastHubEvents.BroadcastUpdated, ToDto(broadcast));

        return Results.Ok(ToDto(broadcast));
    }

    // POST /api/broadcasts/{id}/cancel
    private static async Task<IResult> CancelAsync(
        Guid id,
        ICurrentTenant currentTenant,
        IBroadcastRepository broadcastRepo,
        IWhatsAppAccountRepository accountRepository,
        ITenantMembershipRepository membershipRepository,
        IHubContext<InboxHub> hub)
    {
        if (currentTenant.TenantId is null) return Results.Unauthorized();

        var broadcast = await broadcastRepo.GetByIdAsync(id);
        if (broadcast is null || broadcast.TenantId != currentTenant.TenantId)
            return Results.NotFound();
        if (!await CanAccessBroadcastAsync(currentTenant, broadcast, accountRepository, membershipRepository))
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
        IBroadcastRepository broadcastRepo,
        IWhatsAppAccountRepository accountRepository,
        ITenantMembershipRepository membershipRepository)
    {
        if (currentTenant.TenantId is null) return Results.Unauthorized();

        var broadcast = await broadcastRepo.GetByIdAsync(id);
        if (broadcast is null || broadcast.TenantId != currentTenant.TenantId)
            return Results.NotFound();
        if (!await CanAccessBroadcastAsync(currentTenant, broadcast, accountRepository, membershipRepository))
            return Results.NotFound();

        if (broadcast.Status == BroadcastStatus.Sending)
            return Results.BadRequest(new { error = "Cancel the broadcast before deleting." });

        // Soft-cancel before delete for audit
        broadcast.Cancel();
        await broadcastRepo.UpdateAsync(broadcast);

        return Results.NoContent();
    }

    private static async Task<bool> CanAccessBroadcastAsync(
        ICurrentTenant currentTenant,
        BroadcastList broadcast,
        IWhatsAppAccountRepository accountRepository,
        ITenantMembershipRepository membershipRepository)
    {
        if (currentTenant.UserRole != "Operator") return true;
        if (currentTenant.UserId is null || currentTenant.TenantId != broadcast.TenantId) return false;
        var membership = await membershipRepository.GetByUserAndTenantAsync(currentTenant.UserId.Value, broadcast.TenantId);
        if (membership is null || !membership.CanAccessQueue(broadcast.QueueId)) return false;
        membership.LoadAssignedLinesFromJson();
        if (string.IsNullOrWhiteSpace(broadcast.LinePhoneNumberId))
            return broadcast.CreatedByUserId == currentTenant.UserId.Value;
        var account = await accountRepository.GetByTenantAndPhoneNumberIdAsync(broadcast.TenantId, broadcast.LinePhoneNumberId);
        if (account is null) return false;
        return membership.AssignedLines.Any(assignment =>
            assignment.ConnectionType == account.ConnectionType && assignment.LineNumber == account.LineNumber) ||
            (membership.AssignedLines.Count == 0 && membership.AssignedConnectionType == account.ConnectionType && membership.AssignedLineNumber == account.LineNumber);
    }

    private static async Task<IResult> ListOfficialTemplatesAsync(
        [FromQuery] string linePhoneNumberId,
        ICurrentTenant currentTenant,
        IWhatsAppAccountRepository accountRepository,
        ITenantMembershipRepository membershipRepository,
        ISecretStore secretStore,
        IWhatsAppClientResolver whatsAppClientResolver,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) return Results.Unauthorized();
        var account = await accountRepository.GetByTenantAndPhoneNumberIdAsync(
            currentTenant.TenantId.Value, linePhoneNumberId, cancellationToken);
        if (account is null || !account.IsActive || account.ConnectionType != WhatsAppConnectionType.OfficialApi ||
            string.IsNullOrWhiteSpace(account.WabaId))
            return Results.BadRequest(new { error = "Official API line not found or inactive." });

        if (currentTenant.UserRole == "Operator")
        {
            if (currentTenant.UserId is null) return Results.Forbid();
            var membership = await membershipRepository.GetByUserAndTenantAsync(currentTenant.UserId.Value, currentTenant.TenantId.Value, cancellationToken);
            membership?.LoadAssignedLinesFromJson();
            var hasLine = membership is not null &&
                (membership.AssignedLines.Any(assignment =>
                    assignment.ConnectionType == WhatsAppConnectionType.OfficialApi && assignment.LineNumber == account.LineNumber) ||
                 (membership.AssignedLines.Count == 0 && membership.AssignedConnectionType == WhatsAppConnectionType.OfficialApi && membership.AssignedLineNumber == account.LineNumber));
            if (!hasLine) return Results.Forbid();
        }

        var token = await secretStore.GetAsync(account.AccessTokenRef, cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
            return Results.BadRequest(new { error = "Access token not available." });

        var result = await whatsAppClientResolver.GetClient(WhatsAppConnectionType.OfficialApi)
            .ListTemplatesAsync(account.WabaId, token, cancellationToken);
        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.ErrorMessage ?? "Unable to load templates." });

        return Results.Ok(new { templates = result.Templates.Where(template => template.CanSendInBroadcast).ToArray() });
    }

    private static object ToDto(BroadcastList b) => new
    {
        id = b.Id,
        name = b.Name,
        message = b.Message,
        deliveryMode = b.DeliveryMode.ToString(),
        templateName = b.TemplateName,
        templateLanguage = b.TemplateLanguage,
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
    public Guid? QueueId { get; init; }
    public BroadcastDeliveryMode DeliveryMode { get; init; } = BroadcastDeliveryMode.QrCodeText;
    public string? LinePhoneNumberId { get; init; }
    public string? TemplateName { get; init; }
    public string? TemplateLanguage { get; init; }
    public List<string>? TemplateBodyParameters { get; init; } = [];
}

public sealed record DispatchBroadcastRequest
{
    public string LinePhoneNumberId { get; init; } = string.Empty;
}

public sealed record UpdateBroadcastRequest
{
    public string Message { get; init; } = string.Empty;
}

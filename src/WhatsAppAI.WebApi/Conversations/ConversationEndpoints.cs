using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Conversations.Queries;
using WhatsAppAI.Application.Integrations;
using WhatsAppAI.Domain;
using WhatsAppAI.Domain.Audit;
using WhatsAppAI.Domain.Identity;
using WhatsAppAI.Application.Messaging;
using WhatsAppAI.Domain.Messaging;
using WhatsAppAI.Domain.Integrations;
using WhatsAppAI.Infrastructure.Identity;
using WhatsAppAI.Infrastructure.Persistence;
using WhatsAppAI.WebApi.Hubs;

namespace WhatsAppAI.WebApi.Conversations;

public static class ConversationEndpoints
{
    public static IEndpointRouteBuilder MapConversationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/conversations")
            .WithTags("Conversations")
            .RequireAuthorization("RequireTenantContext");

        group.MapGet("/", ListConversationsAsync)
            .WithName("ListConversations");

        group.MapGet("/{conversationId:guid}", GetConversationAsync)
            .WithName("GetConversation");

        group.MapGet("/{conversationId:guid}/messages", ListMessagesAsync)
            .WithName("ListMessages");

        group.MapGet("/{conversationId:guid}/templates", ListTemplatesAsync)
            .WithName("ListConversationTemplates");

        group.MapPost("/{conversationId:guid}/messages", SendMessageAsync)
            .WithName("SendMessage");

        group.MapPost("/{conversationId:guid}/media", SendMediaMessageAsync)
            .WithName("SendMediaMessage");

        group.MapPost("/{conversationId:guid}/close", CloseConversationAsync)
            .WithName("CloseConversation");

        return app;
    }

    private static async Task<IResult> ListConversationsAsync(
        ICurrentTenant currentTenant,
        IConversationQueries conversationQueries,
        ITenantMembershipRepository membershipRepository,
        IWhatsAppAccountRepository accountRepository,
        AppDbContext dbContext,
        string? cursor = null,
        int limit = 50,
        string? operatorUserId = null,
        string? lineConnectionType = null,
        int? lineNumber = null,
        string? status = null)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();

        if (!await IsTenantActiveAsync(currentTenant.TenantId.Value, dbContext))
            return Results.StatusCode(StatusCodes.Status423Locked);

        ConversationStatus? requestedStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<ConversationStatus>(status, true, out var parsedStatus) ||
                !Enum.IsDefined(parsedStatus))
                return Results.BadRequest(new { error = "Invalid conversation status. Use Open or Closed." });
            requestedStatus = parsedStatus;
        }

        List<string>? phoneNumberIds = null;
        Guid? queueId = null;
        if (currentTenant.UserRole == "TenantOwner" &&
            !string.IsNullOrWhiteSpace(operatorUserId) &&
            !operatorUserId.Equals("unassigned", StringComparison.OrdinalIgnoreCase))
        {
            if (!Guid.TryParse(operatorUserId, out var selectedOperatorUserId))
                return Results.Ok(new CursorPaginationResponse<ConversationDto>());

            var selectedMembership = await membershipRepository.GetByUserAndTenantAsync(
                selectedOperatorUserId,
                currentTenant.TenantId.Value);
            if (selectedMembership?.Role != MembershipRole.Operator)
                return Results.Ok(new CursorPaginationResponse<ConversationDto>());

            selectedMembership.LoadAssignedLinesFromJson();
            queueId = selectedMembership.AssignedQueueId;
            phoneNumberIds = await ResolvePhoneNumberIdsAsync(
                selectedMembership, currentTenant.TenantId.Value, accountRepository);
            if (phoneNumberIds.Count == 0)
                return Results.Ok(new CursorPaginationResponse<ConversationDto>());

            operatorUserId = null;
        }

        if (currentTenant.UserRole == "Operator" && currentTenant.UserId is not null)
        {
            var membership = await membershipRepository.GetByUserAndTenantAsync(
                currentTenant.UserId.Value, currentTenant.TenantId.Value);
            membership?.LoadAssignedLinesFromJson();
            queueId = membership?.AssignedQueueId;

            // If the operator requested a specific line tab, resolve only that line
            if (!string.IsNullOrWhiteSpace(lineConnectionType) && lineNumber is not null &&
                Enum.TryParse<WhatsAppConnectionType>(lineConnectionType, true, out var lineType))
            {
                if (!HasAssignedLine(membership, lineType, lineNumber.Value))
                    return Results.Ok(new CursorPaginationResponse<ConversationDto>());

                var account = await accountRepository.GetByTenantAndSlotAsync(
                    currentTenant.TenantId.Value, lineType, lineNumber.Value);
                phoneNumberIds = account?.PhoneNumberId is not null ? [account.PhoneNumberId] : [];
            }
            else
            {
                phoneNumberIds = await ResolvePhoneNumberIdsAsync(
                    membership, currentTenant.TenantId.Value, accountRepository);
            }

            if (phoneNumberIds.Count == 0)
            {
                if (queueId is null)
                    return Results.Ok(new CursorPaginationResponse<ConversationDto>());

                phoneNumberIds = null;
            }
        }

        var result = await conversationQueries.GetConversationsAsync(
            currentTenant.TenantId.Value,
            new CursorPaginationRequest { Cursor = cursor, Limit = limit },
            operatorUserId,
            phoneNumberIds,
            queueId,
            requestedStatus);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetConversationAsync(
        Guid conversationId,
        ICurrentTenant currentTenant,
        IConversationQueries conversationQueries,
        IConversationRepository conversationRepository,
        ITenantMembershipRepository membershipRepository,
        IWhatsAppAccountRepository accountRepository,
        AppDbContext dbContext)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();

        if (!await IsTenantActiveAsync(currentTenant.TenantId.Value, dbContext))
            return Results.StatusCode(StatusCodes.Status423Locked);

        if (!await OperatorCanAccessConversationAsync(conversationId, currentTenant, conversationRepository, membershipRepository, accountRepository))
            return currentTenant.UserRole == "Operator" ? Results.Forbid() : Results.NotFound();

        var conversation = await conversationQueries.GetConversationByIdAsync(
            currentTenant.TenantId.Value, conversationId);

        return conversation is not null ? Results.Ok(conversation) : Results.NotFound();
    }

    private static async Task<IResult> ListMessagesAsync(
        Guid conversationId,
        ICurrentTenant currentTenant,
        IConversationQueries conversationQueries,
        IConversationRepository conversationRepository,
        ITenantMembershipRepository membershipRepository,
        IWhatsAppAccountRepository accountRepository,
        AppDbContext dbContext,
        string? cursor = null,
        int limit = 50)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();

        if (!await IsTenantActiveAsync(currentTenant.TenantId.Value, dbContext))
            return Results.StatusCode(StatusCodes.Status423Locked);

        if (!await OperatorCanAccessConversationAsync(conversationId, currentTenant, conversationRepository, membershipRepository, accountRepository))
            return currentTenant.UserRole == "Operator" ? Results.Forbid() : Results.NotFound();

        var result = await conversationQueries.GetMessagesAsync(
            currentTenant.TenantId.Value,
            conversationId,
            new CursorPaginationRequest { Cursor = cursor, Limit = limit });

        return Results.Ok(result);
    }

    private static async Task<IResult> ListTemplatesAsync(
        Guid conversationId,
        ICurrentTenant currentTenant,
        IConversationRepository conversationRepository,
        ITenantMembershipRepository membershipRepository,
        IWhatsAppAccountRepository accountRepository,
        ISecretStore secretStore,
        IWhatsAppClientResolver whatsAppClientResolver,
        AppDbContext dbContext)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();

        if (!await IsTenantActiveAsync(currentTenant.TenantId.Value, dbContext))
            return Results.StatusCode(StatusCodes.Status423Locked);

        if (!await OperatorCanAccessConversationAsync(conversationId, currentTenant, conversationRepository, membershipRepository, accountRepository))
            return currentTenant.UserRole == "Operator" ? Results.Forbid() : Results.NotFound();

        var conversation = await conversationRepository.GetByIdAsync(conversationId);
        if (conversation is null || conversation.TenantId != currentTenant.TenantId.Value)
            return Results.NotFound();

        var account = await accountRepository.GetByPhoneNumberIdAsync(conversation.PhoneNumberId);
        if (account is null || account.TenantId != currentTenant.TenantId.Value ||
            !account.IsActive || account.ConnectionType != WhatsAppConnectionType.OfficialApi ||
            string.IsNullOrWhiteSpace(account.WabaId))
            return Results.BadRequest(new { error = "Templates are available only for the official WhatsApp API." });

        var accessToken = await secretStore.GetAsync(account.AccessTokenRef);
        if (string.IsNullOrWhiteSpace(accessToken))
            return Results.Ok(new { templates = Array.Empty<WhatsAppTemplateSummary>(), error = "Access token not available." });

        var result = await whatsAppClientResolver
            .GetClient(WhatsAppConnectionType.OfficialApi)
            .ListTemplatesAsync(account.WabaId, accessToken);

        return Results.Ok(new { templates = result.Templates, error = result.ErrorMessage });
    }

    private static async Task<IResult> SendMessageAsync(
        Guid conversationId,
        [FromBody] SendMessageRequest request,
        ICurrentTenant currentTenant,
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        ITenantMembershipRepository membershipRepository,
        IWhatsAppAccountRepository accountRepository,
        IOutboxMessageRepository outboxMessageRepository,
        ISecretStore secretStore,
        IWhatsAppClientResolver whatsAppClientResolver,
        IClock clock,
        IHubContext<InboxHub> hubContext,
        AppDbContext dbContext)
    {
        if (currentTenant.TenantId is null || currentTenant.UserId is null)
            return Results.Unauthorized();

        if (!await IsTenantActiveAsync(currentTenant.TenantId.Value, dbContext))
            return Results.StatusCode(StatusCodes.Status423Locked);

        var conversation = await conversationRepository.GetByIdAsync(conversationId);
        if (conversation is null || conversation.TenantId != currentTenant.TenantId)
            return Results.NotFound();

        if (conversation.Status == ConversationStatus.Closed)
            return Results.Conflict(new { error = "Conversation is closed. Wait for a new customer message to reopen it." });

        if (currentTenant.UserRole == "Operator" && currentTenant.UserId is not null)
        {
            var membership = await membershipRepository.GetByUserAndTenantAsync(currentTenant.UserId.Value, currentTenant.TenantId.Value);
            membership?.LoadAssignedLinesFromJson();
            var phoneNumberIds = await ResolvePhoneNumberIdsAsync(membership, currentTenant.TenantId.Value, accountRepository);
            if (phoneNumberIds.Count == 0 ||
                (!phoneNumberIds.Contains(conversation.PhoneNumberId) && conversation.PhoneNumberId != "manual") ||
                membership is null || !membership.CanAccessQueue(conversation.QueueId))
                return Results.Forbid();
        }

        var conversationAccount = await accountRepository.GetByPhoneNumberIdAsync(conversation.PhoneNumberId);
        if (conversationAccount is null && conversation.PhoneNumberId == "manual")
            conversationAccount = await accountRepository.GetByTenantAndSlotAsync(
                currentTenant.TenantId.Value,
                WhatsAppConnectionType.QrCode,
                1);
        var isQrConversation = IsQrPhoneNumberId(conversation.PhoneNumberId) ||
            conversationAccount?.ConnectionType == WhatsAppConnectionType.QrCode;
        var templateRequested = request.TemplateName is not null ||
            request.TemplateLanguage is not null ||
            request.TemplateParameters is not null;
        if (templateRequested && isQrConversation)
            return Results.BadRequest(new { error = "Templates are available only for the official WhatsApp API." });

        if (templateRequested && string.IsNullOrWhiteSpace(request.TemplateName))
            return Results.BadRequest(new { error = "Template name is required." });

        if (templateRequested && string.IsNullOrWhiteSpace(request.TemplateLanguage))
            return Results.BadRequest(new { error = "Template language is required." });

        if (templateRequested && request.TemplateName!.Trim().Length > 512)
            return Results.BadRequest(new { error = "Template name is too long." });

        if (templateRequested && request.TemplateLanguage!.Trim().Length > 20)
            return Results.BadRequest(new { error = "Template language is too long." });

        var templateParameters = request.TemplateParameters ?? [];
        if (templateRequested && (templateParameters.Count > 10 ||
            templateParameters.Any(parameter => parameter is null || parameter.Length > 1024)))
            return Results.BadRequest(new { error = "A template may contain up to 10 parameters of 1024 characters." });

        if (templateRequested)
        {
            if (conversationAccount is null || conversationAccount.TenantId != currentTenant.TenantId.Value ||
                !conversationAccount.IsActive ||
                conversationAccount.ConnectionType != WhatsAppConnectionType.OfficialApi ||
                string.IsNullOrWhiteSpace(conversationAccount.WabaId))
            {
                return Results.BadRequest(new { error = "Templates are available only for an active official WhatsApp API line." });
            }

            var accessToken = await secretStore.GetAsync(conversationAccount.AccessTokenRef);
            if (string.IsNullOrWhiteSpace(accessToken))
                return Results.Problem("The WhatsApp access token is not available.", statusCode: StatusCodes.Status503ServiceUnavailable);

            var templatesResult = await whatsAppClientResolver
                .GetClient(WhatsAppConnectionType.OfficialApi)
                .ListTemplatesAsync(conversationAccount.WabaId, accessToken);
            if (!templatesResult.IsSuccess)
            {
                return Results.Problem(
                    templatesResult.ErrorMessage ?? "Unable to verify approved templates.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var selectedTemplate = templatesResult.Templates.SingleOrDefault(template =>
                string.Equals(template.Name, request.TemplateName!.Trim(), StringComparison.Ordinal) &&
                string.Equals(template.Language, request.TemplateLanguage!.Trim(), StringComparison.Ordinal));
            if (selectedTemplate is null || !selectedTemplate.CanSendInInbox)
                return Results.BadRequest(new { error = "The selected template is not approved or is incompatible with individual sending." });

            if (templateParameters.Count != selectedTemplate.BodyParameterCount)
            {
                return Results.BadRequest(new
                {
                    error = $"This template requires exactly {selectedTemplate.BodyParameterCount} body parameters."
                });
            }
        }

        if (!templateRequested && !isQrConversation && !conversation.IsWindowOpen(clock.UtcNow))
            return Results.BadRequest(new { error = "Window closed. Only templates allowed." });

        var idempotencyKey = request.IdempotencyKey ?? Guid.NewGuid().ToString();

        var existing = await messageRepository.GetByIdempotencyKeyAsync(idempotencyKey);
        if (existing is not null)
            return Results.Ok(new { id = existing.Id, status = existing.Status.ToString() });

        var message = templateRequested
            ? Message.CreateOutboundTemplate(
                currentTenant.TenantId.Value,
                conversationId,
                conversation.ContactId,
                request.TemplateName!.Trim(),
                request.TemplateLanguage!.Trim(),
                JsonSerializer.Serialize(templateParameters),
                idempotencyKey)
            : Message.CreateOutbound(
                currentTenant.TenantId.Value,
                conversationId,
                conversation.ContactId,
                MessageType.Text,
                request.Content,
                idempotencyKey);

        await messageRepository.AddAsync(message);

        var outboxMessage = OutboxMessage.Create(currentTenant.TenantId.Value, message.Id);
        await outboxMessageRepository.AddAsync(outboxMessage);

        conversation.RecordMessage();
        await conversationRepository.UpdateAsync(conversation);

        await hubContext.Clients.Group($"tenant:{currentTenant.TenantId}")
            .SendAsync(InboxHubMethods.NewMessage, new
            {
                id = message.Id,
                conversationId,
                direction = message.Direction.ToString(),
                content = message.Content,
                status = message.Status.ToString(),
                createdAt = message.CreatedAt
            });

        return Results.Ok(new { id = message.Id, status = message.Status.ToString() });
    }

    private static async Task<IResult> SendMediaMessageAsync(
        Guid conversationId,
        IFormFile file,
        ICurrentTenant currentTenant,
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        ITenantMembershipRepository membershipRepository,
        IWhatsAppAccountRepository accountRepository,
        IOutboxMessageRepository outboxMessageRepository,
        IClock clock,
        IHubContext<InboxHub> hubContext,
        AppDbContext dbContext,
        string? caption = null)
    {
        const long maxBytes = 16 * 1024 * 1024;
        if (currentTenant.TenantId is null || currentTenant.UserId is null)
            return Results.Unauthorized();
        if (file is null || file.Length == 0 || file.Length > maxBytes)
            return Results.BadRequest(new { error = "Attachment must be between 1 byte and 16 MB." });

        var contentType = file.ContentType?.Split(';')[0].Trim().ToLowerInvariant();
        var messageType = contentType switch
        {
            var type when type.StartsWith("image/") => MessageType.Image,
            var type when type.StartsWith("audio/") => MessageType.Audio,
            var type when type.StartsWith("video/") => MessageType.Video,
            "application/pdf" or "text/plain" or "application/msword" or
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document" or
                "application/vnd.ms-excel" or
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => MessageType.Document,
            _ => (MessageType?)null
        };
        if (messageType is null)
            return Results.BadRequest(new { error = "Unsupported attachment type." });

        var conversation = await conversationRepository.GetByIdAsync(conversationId);
        if (conversation is null || conversation.TenantId != currentTenant.TenantId)
            return Results.NotFound();
        if (conversation.Status == ConversationStatus.Closed)
            return Results.Conflict(new { error = "Conversation is closed." });

        if (currentTenant.UserRole == "Operator")
        {
            var membership = await membershipRepository.GetByUserAndTenantAsync(
                currentTenant.UserId.Value, currentTenant.TenantId.Value);
            membership?.LoadAssignedLinesFromJson();
            var phoneNumberIds = await ResolvePhoneNumberIdsAsync(
                membership, currentTenant.TenantId.Value, accountRepository);
            if (membership is null || phoneNumberIds.Count == 0 ||
                (!phoneNumberIds.Contains(conversation.PhoneNumberId) && conversation.PhoneNumberId != "manual") ||
                !membership.CanAccessQueue(conversation.QueueId))
                return Results.Forbid();
        }

        var account = await accountRepository.GetByPhoneNumberIdAsync(conversation.PhoneNumberId);
        if (account is null && conversation.PhoneNumberId == "manual")
            account = await accountRepository.GetByTenantAndSlotAsync(
                currentTenant.TenantId.Value, WhatsAppConnectionType.QrCode, 1);
        var isQrConversation = IsQrPhoneNumberId(conversation.PhoneNumberId) ||
            account?.ConnectionType == WhatsAppConnectionType.QrCode;
        if (!isQrConversation && !conversation.IsWindowOpen(clock.UtcNow))
            return Results.BadRequest(new { error = "Window closed. Only templates are allowed." });

        await using var stream = file.OpenReadStream();
        await using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        var dataUrl = $"data:{contentType};base64,{Convert.ToBase64String(memory.ToArray())}";
        var message = Message.CreateOutbound(
            currentTenant.TenantId.Value, conversationId, conversation.ContactId,
            messageType.Value, null, Guid.NewGuid().ToString(),
            caption: caption?.Trim(), mediaUrl: dataUrl);
        await messageRepository.AddAsync(message);
        await outboxMessageRepository.AddAsync(OutboxMessage.Create(currentTenant.TenantId.Value, message.Id));
        conversation.RecordMessage();
        await conversationRepository.UpdateAsync(conversation);
        await hubContext.Clients.Group($"tenant:{currentTenant.TenantId}").SendAsync(
            InboxHubMethods.NewMessage, new
            {
                id = message.Id, conversationId, direction = message.Direction.ToString(),
                content = caption, type = message.Type.ToString(), status = message.Status.ToString(),
                createdAt = message.CreatedAt
            });
        return Results.Ok(new { id = message.Id, status = message.Status.ToString() });
    }

    private static async Task<IResult> CloseConversationAsync(
        Guid conversationId,
        ICurrentTenant currentTenant,
        IConversationRepository conversationRepository,
        ITenantMembershipRepository membershipRepository,
        IWhatsAppAccountRepository accountRepository,
        IAuditLogRepository auditLogRepository,
        AppDbContext dbContext,
        HttpContext httpContext,
        IHubContext<InboxHub> hubContext)
    {
        if (currentTenant.TenantId is null || currentTenant.UserId is null)
            return Results.Unauthorized();

        if (!await IsTenantActiveAsync(currentTenant.TenantId.Value, dbContext))
            return Results.StatusCode(StatusCodes.Status423Locked);

        var conversation = await conversationRepository.GetByIdAsync(conversationId);
        if (conversation is null || conversation.TenantId != currentTenant.TenantId)
            return Results.NotFound();

        if (conversation.Status == ConversationStatus.Closed)
            return Results.Conflict(new { error = "Conversation is closed. Wait for a new customer message to reopen it." });

        if (!await OperatorCanAccessConversationAsync(
                conversationId,
                currentTenant,
                conversationRepository,
                membershipRepository,
                accountRepository))
            return currentTenant.UserRole == "Operator" ? Results.Forbid() : Results.NotFound();

        var ifMatch = httpContext.Request.Headers["If-Match"].FirstOrDefault();
        if (ifMatch is null || !uint.TryParse(ifMatch.Trim('"'), out var expectedVersion))
            return Results.BadRequest(new { error = "If-Match header with version is required." });

        await using var transaction = await dbContext.Database.BeginTransactionAsync(httpContext.RequestAborted);
        try
        {
            conversation.Close(expectedVersion);
            await conversationRepository.UpdateAsync(conversation, httpContext.RequestAborted);
            await auditLogRepository.AddAsync(AuditLog.Create(
                currentTenant.TenantId.Value,
                currentTenant.UserId.Value,
                "Conversation.Closed",
                "Conversation",
                conversationId.ToString(),
                "Conversation closed by operator"),
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

        await hubContext.Clients.Group($"tenant:{currentTenant.TenantId}")
            .SendAsync(InboxHubMethods.ConversationUpdated, new { conversationId }, httpContext.RequestAborted);

        return Results.Ok(new
        {
            id = conversation.Id,
            status = conversation.Status.ToString(),
            version = conversation.Version
        });
    }

    private static async Task<bool> IsTenantActiveAsync(Guid tenantId, AppDbContext dbContext)
    {
        var tenant = await dbContext.Tenants.FindAsync(tenantId);
        return tenant?.Status == TenantStatus.Active;
    }

    private static bool IsQrPhoneNumberId(string phoneNumberId) =>
        phoneNumberId.StartsWith("qr:", StringComparison.OrdinalIgnoreCase) ||
        phoneNumberId.Equals("whatsapp-web", StringComparison.OrdinalIgnoreCase);

    private static async Task<bool> OperatorCanAccessConversationAsync(
        Guid conversationId,
        ICurrentTenant currentTenant,
        IConversationRepository conversationRepository,
        ITenantMembershipRepository membershipRepository,
        IWhatsAppAccountRepository accountRepository)
    {
        if (currentTenant.UserRole != "Operator" || currentTenant.UserId is null || currentTenant.TenantId is null)
            return true;

        var conversation = await conversationRepository.GetByIdAsync(conversationId);
        if (conversation is null)
            return false;

        var membership = await membershipRepository.GetByUserAndTenantAsync(
            currentTenant.UserId.Value, currentTenant.TenantId.Value);
        if (membership is null)
            return false;

        membership.LoadAssignedLinesFromJson();
        var phoneNumberIds = await ResolvePhoneNumberIdsAsync(
            membership, currentTenant.TenantId.Value, accountRepository);
        var hasLineAssignment = membership.AssignedLines.Count > 0 ||
            (membership.AssignedConnectionType is not null && membership.AssignedLineNumber is not null);

        var includeManual = phoneNumberIds.Exists(p =>
            p.StartsWith("qr:", StringComparison.OrdinalIgnoreCase) && p.EndsWith(":1"));
        var canAccessQueue = membership.CanAccessQueue(conversation.QueueId);
        return canAccessQueue &&
            (hasLineAssignment
                ? phoneNumberIds.Count > 0 &&
                  (phoneNumberIds.Contains(conversation.PhoneNumberId) ||
                   (conversation.PhoneNumberId == "manual" && includeManual))
                : membership.AssignedQueueId is not null);
    }

    private static bool HasAssignedLine(
        TenantMembership? membership,
        WhatsAppConnectionType connectionType,
        int lineNumber) =>
        membership?.AssignedLines.Any(line =>
            line.ConnectionType == connectionType && line.LineNumber == lineNumber) == true ||
        (membership?.AssignedLines.Count == 0 &&
         membership?.AssignedConnectionType == connectionType &&
         membership?.AssignedLineNumber == lineNumber);

    // Resolves all assigned lines of a membership to their WhatsApp account PhoneNumberIds.
    // Falls back to the legacy single-line fields when AssignedLines is empty.
    private static async Task<List<string>> ResolvePhoneNumberIdsAsync(
        TenantMembership? membership,
        Guid tenantId,
        IWhatsAppAccountRepository accountRepository)
    {
        if (membership is null)
            return [];

        IReadOnlyList<LineAssignment> lines;
        if (membership.AssignedLines.Count > 0)
            lines = membership.AssignedLines;
        else if (membership.AssignedConnectionType is not null && membership.AssignedLineNumber is not null)
            lines = [new LineAssignment(membership.AssignedConnectionType.Value, membership.AssignedLineNumber.Value)];
        else
            lines = [];

        var result = new List<string>(lines.Count);
        foreach (var line in lines)
        {
            var account = await accountRepository.GetByTenantAndSlotAsync(tenantId, line.ConnectionType, line.LineNumber);
            if (account?.PhoneNumberId is not null)
                result.Add(account.PhoneNumberId);
        }
        return result;
    }
}

public sealed record SendMessageRequest
{
    public string Content { get; init; } = string.Empty;
    public string? IdempotencyKey { get; init; }
    public string? TemplateName { get; init; }
    public string? TemplateLanguage { get; init; }
    public IReadOnlyList<string>? TemplateParameters { get; init; }
}

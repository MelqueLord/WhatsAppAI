using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Conversations.Queries;
using WhatsAppAI.Application.Integrations;
using WhatsAppAI.Application.Messaging;
using WhatsAppAI.Domain;
using WhatsAppAI.Domain.Audit;
using WhatsAppAI.Domain.Identity;
using WhatsAppAI.Domain.Integrations;
using WhatsAppAI.Domain.Messaging;
using WhatsAppAI.Infrastructure.Identity;
using WhatsAppAI.Infrastructure.Persistence;
using WhatsAppAI.Infrastructure.Secrets;
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

        group.MapPost("/{conversationId:guid}/read", MarkConversationReadAsync)
            .WithName("MarkConversationRead");

        group.MapGet("/{conversationId:guid}/templates", ListTemplatesAsync)
            .WithName("ListConversationTemplates");

        group.MapPost("/{conversationId:guid}/messages", SendMessageAsync)
            .WithName("SendMessage");

        group.MapPost("/{conversationId:guid}/messages/{messageId:guid}/reactions", SendReactionAsync)
            .WithName("SendMessageReaction");

        group.MapDelete("/{conversationId:guid}/messages/{messageId:guid}", DeleteMessageAsync)
            .WithName("DeleteInboxMessage");

        group.MapPatch("/{conversationId:guid}/messages/{messageId:guid}", EditQrMessageAsync)
            .WithName("EditQrMessage");

        group.MapPost("/{conversationId:guid}/media", SendMediaMessageAsync)
            .WithName("SendMediaMessage")
            // Production CSRF validation is performed by the authenticated API middleware.
            // This prevents the minimal-API form binding metadata from requiring a second
            // antiforgery middleware before the handler can run.
            .DisableAntiforgery();

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
        string? status = null,
        string? search = null)
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
            requestedStatus,
            search);

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
        int limit = 50,
        string? search = null,
        Guid? throughMessageId = null)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();

        if (!await IsTenantActiveAsync(currentTenant.TenantId.Value, dbContext))
            return Results.StatusCode(StatusCodes.Status423Locked);

        if (!await OperatorCanAccessConversationAsync(conversationId, currentTenant, conversationRepository, membershipRepository, accountRepository))
            return currentTenant.UserRole == "Operator" ? Results.Forbid() : Results.NotFound();

        if (search is { Length: > 100 })
            return Results.BadRequest(new { error = "Search is limited to 100 characters." });

        var result = await conversationQueries.GetMessagesAsync(
            currentTenant.TenantId.Value,
            conversationId,
            new CursorPaginationRequest { Cursor = cursor, Limit = limit },
            throughMessageId,
            search);

        return Results.Ok(result);
    }

    private static async Task<IResult> MarkConversationReadAsync(
        Guid conversationId,
        ICurrentTenant currentTenant,
        IConversationRepository conversationRepository,
        ITenantMembershipRepository membershipRepository,
        IWhatsAppAccountRepository accountRepository,
        IClock clock,
        AppDbContext dbContext,
        HttpContext httpContext)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();

        if (!await IsTenantActiveAsync(currentTenant.TenantId.Value, dbContext))
            return Results.StatusCode(StatusCodes.Status423Locked);

        if (!await OperatorCanAccessConversationAsync(
                conversationId,
                currentTenant,
                conversationRepository,
                membershipRepository,
                accountRepository))
            return currentTenant.UserRole == "Operator" ? Results.Forbid() : Results.NotFound();

        var conversation = await conversationRepository.GetByIdAsync(conversationId, httpContext.RequestAborted);
        if (conversation is null || conversation.TenantId != currentTenant.TenantId)
            return Results.NotFound();

        conversation.MarkRead(clock.UtcNow);
        await dbContext.SaveChangesAsync(httpContext.RequestAborted);

        return Results.Ok(new { conversationId, unreadCount = 0 });
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
        List<WhatsAppTemplateParameter>? templateParameterPayload = null;
        WhatsAppTemplateSummary? selectedTemplate = null;
        var templateRequested = request.TemplateName is not null ||
            request.TemplateLanguage is not null ||
            request.TemplateParameters is not null;
        if (templateRequested && request.ReplyToMessageId.HasValue)
            return Results.BadRequest(new { error = "Replies in this release must be text messages." });

        if (request.ForwardSourceMessageId.HasValue &&
            (templateRequested || request.ReplyToMessageId.HasValue || request.CorrectsMessageId.HasValue))
            return Results.BadRequest(new { error = "Forwarding cannot be combined with a template or reply." });

        if (request.CorrectsMessageId.HasValue &&
            (templateRequested || request.ReplyToMessageId.HasValue))
            return Results.BadRequest(new { error = "A correction must be a separate text message." });

        Message? forwardSource = null;
        if (request.ForwardSourceMessageId.HasValue)
        {
            forwardSource = await messageRepository.GetByIdAsync(request.ForwardSourceMessageId.Value);
            if (forwardSource is null ||
                forwardSource.TenantId != currentTenant.TenantId.Value ||
                forwardSource.ConversationId == conversationId ||
                forwardSource.HiddenFromInboxAt is not null ||
                forwardSource.Type is not (MessageType.Text or MessageType.Template) ||
                string.IsNullOrWhiteSpace(forwardSource.Content) ||
                !await OperatorCanAccessConversationAsync(
                    forwardSource.ConversationId,
                    currentTenant,
                    conversationRepository,
                    membershipRepository,
                    accountRepository))
                return Results.BadRequest(new { error = "The selected message cannot be forwarded." });
        }

        Message? quotedMessage = null;
        if (request.ReplyToMessageId.HasValue)
        {
            quotedMessage = await messageRepository.GetByIdAsync(request.ReplyToMessageId.Value);
            if (quotedMessage is null ||
                quotedMessage.TenantId != currentTenant.TenantId.Value ||
                quotedMessage.ConversationId != conversationId ||
                quotedMessage.ContactId != conversation.ContactId ||
                quotedMessage.HiddenFromInboxAt is not null ||
                string.IsNullOrWhiteSpace(quotedMessage.ExternalId) ||
                quotedMessage.ExternalId.Length > 100)
            {
                return Results.BadRequest(new { error = "The selected message cannot be quoted in this conversation." });
            }
        }

        Message? correctionSource = null;
        if (request.CorrectsMessageId.HasValue)
        {
            correctionSource = await messageRepository.GetByIdAsync(request.CorrectsMessageId.Value);
            if (correctionSource is null ||
                correctionSource.TenantId != currentTenant.TenantId.Value ||
                correctionSource.ConversationId != conversationId ||
                correctionSource.ContactId != conversation.ContactId ||
                correctionSource.HiddenFromInboxAt is not null ||
                correctionSource.Direction != MessageDirection.Outbound ||
                correctionSource.Type != MessageType.Text ||
                correctionSource.Status is not (MessageStatus.Sent or MessageStatus.Delivered or MessageStatus.Read) ||
                string.IsNullOrWhiteSpace(correctionSource.ExternalId) ||
                correctionSource.ExternalId.Length > 100)
                return Results.BadRequest(new { error = "The selected message cannot be corrected." });

            if (string.IsNullOrWhiteSpace(request.Content) || request.Content.Trim().Length > 3990)
                return Results.BadRequest(new { error = "Correction text must contain 1 to 3990 characters." });

            quotedMessage = correctionSource;
        }
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

            selectedTemplate = templatesResult.Templates.SingleOrDefault(template =>
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

            templateParameterPayload = templateParameters
                .Select((value, index) => new WhatsAppTemplateParameter(
                    value,
                    string.Equals(selectedTemplate.ParameterFormat, "NAMED", StringComparison.OrdinalIgnoreCase)
                        ? selectedTemplate.BodyParameterNames[index]
                        : null))
                .ToList();
        }

        if (!templateRequested && !isQrConversation && !conversation.IsWindowOpen(clock.UtcNow))
            return Results.BadRequest(new { error = "Window closed. Only templates allowed." });

        var idempotencyKey = request.IdempotencyKey ?? Guid.NewGuid().ToString();

        var existing = await messageRepository.GetByIdempotencyKeyAsync(idempotencyKey);
        if (existing is not null)
            return Results.Ok(new { id = existing.Id, status = existing.Status.ToString() });

        var outgoingContent = correctionSource is null
            ? forwardSource?.Content ?? request.Content
            : $"Correção: {request.Content!.Trim()}";

        var message = templateRequested
            ? Message.CreateOutboundTemplate(
                currentTenant.TenantId.Value,
                conversationId,
                conversation.ContactId,
                request.TemplateName!.Trim(),
                request.TemplateLanguage!.Trim(),
                JsonSerializer.Serialize(templateParameterPayload),
                idempotencyKey,
                WhatsAppTemplateBodyRenderer.Render(selectedTemplate!.BodyText, templateParameterPayload!))
            : Message.CreateOutbound(
                currentTenant.TenantId.Value,
                conversationId,
                conversation.ContactId,
                MessageType.Text,
                outgoingContent,
                idempotencyKey,
                quotedMessageId: quotedMessage?.ExternalId);

        if (forwardSource is not null)
            message.MarkForwardedFrom(forwardSource.Id);
        if (correctionSource is not null)
            message.MarkCorrectionOf(correctionSource.Id);

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

    private static async Task<IResult> EditQrMessageAsync(
        Guid conversationId,
        Guid messageId,
        [FromBody] EditMessageRequest request,
        ICurrentTenant currentTenant,
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        IContactRepository contactRepository,
        ITenantMembershipRepository membershipRepository,
        IWhatsAppAccountRepository accountRepository,
        IWhatsAppClientResolver whatsAppClientResolver,
        IClock clock,
        IHubContext<InboxHub> hubContext,
        AppDbContext dbContext)
    {
        if (currentTenant.TenantId is null || currentTenant.UserId is null)
            return Results.Unauthorized();
        if (!await IsTenantActiveAsync(currentTenant.TenantId.Value, dbContext))
            return Results.StatusCode(StatusCodes.Status423Locked);
        if (!await OperatorCanAccessConversationAsync(conversationId, currentTenant,
                conversationRepository, membershipRepository, accountRepository))
            return currentTenant.UserRole == "Operator" ? Results.Forbid() : Results.NotFound();

        var content = request.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content) || content.Length > 4000)
            return Results.BadRequest(new { error = "Edited text must contain 1 to 4000 characters." });

        var conversation = await conversationRepository.GetByIdAsync(conversationId);
        var message = await messageRepository.GetByIdAsync(messageId);
        if (conversation is null || message is null ||
            conversation.TenantId != currentTenant.TenantId.Value ||
            message.TenantId != currentTenant.TenantId.Value ||
            message.ConversationId != conversationId ||
            message.ContactId != conversation.ContactId)
            return Results.NotFound();
        if (conversation.Status == ConversationStatus.Closed)
            return Results.Conflict(new { error = "Conversation is closed." });

        var account = await accountRepository.GetByPhoneNumberIdAsync(conversation.PhoneNumberId);
        if (account is null && conversation.PhoneNumberId == "manual")
            account = await accountRepository.GetByTenantAndSlotAsync(
                currentTenant.TenantId.Value, WhatsAppConnectionType.QrCode, 1);
        var phoneNumberId = account?.PhoneNumberId ?? conversation.PhoneNumberId;
        if (!IsQrSessionForTenant(phoneNumberId, currentTenant.TenantId.Value) ||
            account is { IsActive: false } ||
            account is not null && account.TenantId != currentTenant.TenantId.Value)
            return Results.BadRequest(new { error = "Native editing is available only on an active QR line." });

        var now = clock.UtcNow;
        if (message.Direction != MessageDirection.Outbound || message.Type != MessageType.Text ||
            message.Status is not (MessageStatus.Sent or MessageStatus.Delivered or MessageStatus.Read) ||
            message.HiddenFromInboxAt is not null || string.IsNullOrWhiteSpace(message.ExternalId) ||
            message.ExternalId.Length > 100 || now < message.CreatedAt ||
            now - message.CreatedAt > TimeSpan.FromMinutes(15))
            return Results.BadRequest(new { error = "This message can no longer be edited natively." });

        var contact = await contactRepository.GetByIdAsync(message.ContactId);
        if (contact is null || contact.TenantId != currentTenant.TenantId.Value)
            return Results.NotFound();

        var result = await whatsAppClientResolver.GetClient(WhatsAppConnectionType.QrCode)
            .EditTextMessageAsync(phoneNumberId, "whatsapp-web", contact.PhoneNumber,
                message.ExternalId, content);
        if (!result.IsSuccess)
            return Results.Problem(result.ErrorMessage ?? "Message could not be edited.",
                statusCode: StatusCodes.Status502BadGateway);

        message.ApplyNativeEdit(content, now);
        await messageRepository.UpdateAsync(message);
        await hubContext.Clients.Group($"tenant:{currentTenant.TenantId}")
            .SendAsync(InboxHubMethods.NewMessage, new { conversationId, id = message.Id });
        return Results.Ok(new { messageId, editedAt = message.EditedAt });
    }

    private static async Task<IResult> DeleteMessageAsync(
        Guid conversationId,
        Guid messageId,
        ICurrentTenant currentTenant,
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        IContactRepository contactRepository,
        ITenantMembershipRepository membershipRepository,
        IWhatsAppAccountRepository accountRepository,
        IWhatsAppClientResolver whatsAppClientResolver,
        IHubContext<InboxHub> hubContext,
        AppDbContext dbContext,
        string scope = "local")
    {
        if (currentTenant.TenantId is null || currentTenant.UserId is null)
            return Results.Unauthorized();
        if (!await IsTenantActiveAsync(currentTenant.TenantId.Value, dbContext))
            return Results.StatusCode(StatusCodes.Status423Locked);
        if (!await OperatorCanAccessConversationAsync(conversationId, currentTenant,
                conversationRepository, membershipRepository, accountRepository))
            return currentTenant.UserRole == "Operator" ? Results.Forbid() : Results.NotFound();
        if (scope is not ("local" or "everyone"))
            return Results.BadRequest(new { error = "Invalid deletion scope." });

        var conversation = await conversationRepository.GetByIdAsync(conversationId);
        var message = await messageRepository.GetByIdAsync(messageId);
        if (conversation is null || message is null ||
            conversation.TenantId != currentTenant.TenantId.Value ||
            message.TenantId != currentTenant.TenantId.Value ||
            message.ConversationId != conversationId ||
            message.ContactId != conversation.ContactId)
            return Results.NotFound();

        if (message.Direction != MessageDirection.Outbound)
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        if (scope == "everyone")
        {
            if (message.DeletedForEveryoneAt is not null)
                return Results.Ok(new { messageId, scope });
            if (message.Direction != MessageDirection.Outbound ||
                string.IsNullOrWhiteSpace(message.ExternalId) ||
                message.ExternalId.Length > 100)
                return Results.BadRequest(new { error = "Only sent messages can be revoked." });

            var account = await accountRepository.GetByPhoneNumberIdAsync(conversation.PhoneNumberId);
            if (account is null && conversation.PhoneNumberId == "manual")
                account = await accountRepository.GetByTenantAndSlotAsync(
                    currentTenant.TenantId.Value, WhatsAppConnectionType.QrCode, 1);
            var phoneNumberId = account?.PhoneNumberId ?? conversation.PhoneNumberId;
            if (!IsQrSessionForTenant(phoneNumberId, currentTenant.TenantId.Value) ||
                account is { IsActive: false } ||
                account is not null && account.TenantId != currentTenant.TenantId.Value)
                return Results.BadRequest(new { error = "Revocation is available only on an active QR line." });

            var contact = await contactRepository.GetByIdAsync(message.ContactId);
            if (contact is null || contact.TenantId != currentTenant.TenantId.Value)
                return Results.NotFound();

            var result = await whatsAppClientResolver.GetClient(WhatsAppConnectionType.QrCode)
                .DeleteMessageForEveryoneAsync(phoneNumberId, "whatsapp-web", contact.PhoneNumber,
                    message.ExternalId);
            if (!result.IsSuccess)
                return Results.Problem(result.ErrorMessage ?? "Message could not be revoked.",
                    statusCode: StatusCodes.Status502BadGateway);

            message.MarkDeletedForEveryone();
        }
        else
        {
            message.HideFromInbox();
        }

        await messageRepository.UpdateAsync(message);
        await hubContext.Clients.Group($"tenant:{currentTenant.TenantId}")
            .SendAsync(InboxHubMethods.NewMessage, new { conversationId, id = message.Id });
        return Results.Ok(new { messageId, scope });
    }

    private static async Task<IResult> SendReactionAsync(
        Guid conversationId,
        Guid messageId,
        [FromBody] SendReactionRequest request,
        ICurrentTenant currentTenant,
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        IOutboxMessageRepository outboxMessageRepository,
        ITenantMembershipRepository membershipRepository,
        IWhatsAppAccountRepository accountRepository,
        IClock clock,
        IHubContext<InboxHub> hubContext,
        AppDbContext dbContext)
    {
        if (currentTenant.TenantId is null || currentTenant.UserId is null)
            return Results.Unauthorized();
        if (!await IsTenantActiveAsync(currentTenant.TenantId.Value, dbContext))
            return Results.StatusCode(StatusCodes.Status423Locked);
        if (!await OperatorCanAccessConversationAsync(conversationId, currentTenant,
                conversationRepository, membershipRepository, accountRepository))
            return currentTenant.UserRole == "Operator" ? Results.Forbid() : Results.NotFound();

        var conversation = await conversationRepository.GetByIdAsync(conversationId);
        if (conversation is null || conversation.TenantId != currentTenant.TenantId.Value)
            return Results.NotFound();
        if (conversation.Status == ConversationStatus.Closed)
            return Results.Conflict(new { error = "Conversation is closed." });
        if (request.Emoji is not ("👍" or "❤️" or "😂" or "😮" or "😢" or "🙏" or ""))
            return Results.BadRequest(new { error = "Unsupported reaction emoji." });

        var account = await accountRepository.GetByPhoneNumberIdAsync(conversation.PhoneNumberId);
        var isQr = IsQrPhoneNumberId(conversation.PhoneNumberId) ||
            account?.ConnectionType == WhatsAppConnectionType.QrCode;
        if (!isQr && !conversation.IsWindowOpen(clock.UtcNow))
            return Results.BadRequest(new { error = "Window closed. Reactions are unavailable." });

        var target = await messageRepository.GetByIdAsync(messageId);
        if (target is null || target.TenantId != currentTenant.TenantId.Value ||
            target.ConversationId != conversationId || target.ContactId != conversation.ContactId ||
            target.Type == MessageType.Reaction || target.HiddenFromInboxAt is not null ||
            string.IsNullOrWhiteSpace(target.ExternalId) ||
            target.ExternalId.Length > 100)
            return Results.BadRequest(new { error = "The selected message cannot receive a reaction." });

        var idempotencyKey = request.IdempotencyKey ?? Guid.NewGuid().ToString();
        var existing = await messageRepository.GetByIdempotencyKeyAsync(idempotencyKey);
        if (existing is not null)
        {
            if (existing.TenantId != currentTenant.TenantId.Value || existing.ConversationId != conversationId ||
                existing.Type != MessageType.Reaction || existing.QuotedMessageId != target.ExternalId ||
                existing.Content != request.Emoji)
                return Results.Conflict(new { error = "Idempotency key belongs to another operation." });
            return Results.Ok(new { id = existing.Id, status = existing.Status.ToString() });
        }

        var reaction = Message.CreateOutbound(
            currentTenant.TenantId.Value, conversationId, conversation.ContactId,
            MessageType.Reaction, request.Emoji, idempotencyKey,
            quotedMessageId: target.ExternalId);
        await messageRepository.AddAsync(reaction);
        await outboxMessageRepository.AddAsync(OutboxMessage.Create(currentTenant.TenantId.Value, reaction.Id));
        await hubContext.Clients.Group($"tenant:{currentTenant.TenantId}")
            .SendAsync(InboxHubMethods.NewMessage, new { conversationId, id = reaction.Id });

        return Results.Ok(new { id = reaction.Id, status = reaction.Status.ToString() });
    }

    private static async Task<IResult> SendMediaMessageAsync(
        Guid conversationId,
        IFormFile file,
        ICurrentTenant currentTenant,
        IConversationRepository conversationRepository,
        ITenantMembershipRepository membershipRepository,
        IWhatsAppAccountRepository accountRepository,
        IEncryptionService encryptionService,
        IClock clock,
        IHubContext<InboxHub> hubContext,
        AppDbContext dbContext,
        string? caption = null)
    {
        const long maxBytes = 5 * 1024 * 1024;
        if (currentTenant.TenantId is null || currentTenant.UserId is null)
            return Results.Unauthorized();
        if (file is null || file.Length == 0 || file.Length > maxBytes)
            return Results.BadRequest(new { error = "Image must be between 1 byte and 5 MB." });

        if (string.IsNullOrWhiteSpace(file.ContentType))
            return Results.BadRequest(new { error = "Attachment content type is required." });

        var declaredContentType = file.ContentType.Split(';')[0].Trim().ToLowerInvariant();
        if (declaredContentType is not ("image/jpeg" or "image/png"))
            return Results.BadRequest(new { error = "Only JPEG and PNG images are supported." });
        if (caption?.Length > 1024)
            return Results.BadRequest(new { error = "Caption must be at most 1024 characters." });

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
        if (!conversation.IsWindowOpen(clock.UtcNow))
            return Results.BadRequest(new { error = "Window closed. Only templates are allowed." });

        await using var stream = file.OpenReadStream();
        await using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        var content = memory.ToArray();
        var contentType = DetectImageContentType(content);
        if (contentType is null)
            return Results.BadRequest(new { error = "Image contents do not match its type." });
        var message = Message.CreateOutbound(
            currentTenant.TenantId.Value, conversationId, conversation.ContactId,
            MessageType.Image, null, Guid.NewGuid().ToString(), caption: caption?.Trim());
        var attachment = OutboundMediaAttachment.Create(
            currentTenant.TenantId.Value, message.Id, contentType, content.LongLength,
            encryptionService.Encrypt(Convert.ToBase64String(content)));
        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        dbContext.Set<Message>().Add(message);
        dbContext.Set<OutboundMediaAttachment>().Add(attachment);
        dbContext.Set<OutboxMessage>().Add(OutboxMessage.Create(currentTenant.TenantId.Value, message.Id));
        conversation.RecordMessage();
        dbContext.Set<Conversation>().Update(conversation);
        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
        await hubContext.Clients.Group($"tenant:{currentTenant.TenantId}").SendAsync(
            InboxHubMethods.NewMessage, new
            {
                id = message.Id,
                conversationId,
                direction = message.Direction.ToString(),
                type = message.Type.ToString(),
                hasCaption = !string.IsNullOrWhiteSpace(message.Caption),
                status = message.Status.ToString(),
                createdAt = message.CreatedAt
            });
        return Results.Ok(new { id = message.Id, status = message.Status.ToString(), type = message.Type.ToString(), hasCaption = !string.IsNullOrWhiteSpace(message.Caption), createdAt = message.CreatedAt });
    }

    private static string? DetectImageContentType(byte[] content)
    {
        if (content.Length >= 8 && content.AsSpan(0, 8).SequenceEqual("\x89PNG\r\n\x1a\n"u8))
            return "image/png";

        if (content.Length >= 3 && content[0] == 0xff && content[1] == 0xd8 && content[2] == 0xff)
            return "image/jpeg";

        return null;
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

    private static bool IsQrSessionForTenant(string phoneNumberId, Guid tenantId)
    {
        var parts = phoneNumberId.Split(':', 3);
        return parts.Length == 3 &&
            parts[0].Equals("qr", StringComparison.OrdinalIgnoreCase) &&
            Guid.TryParse(parts[1], out var sessionTenantId) &&
            sessionTenantId == tenantId &&
            int.TryParse(parts[2], out var lineNumber) && lineNumber > 0;
    }

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
        int lineNumber)
    {
        if (membership is null)
            return false;

        return membership.AssignedLines.Any(line =>
            line.ConnectionType == connectionType && line.LineNumber == lineNumber) ||
            (membership.AssignedLines.Count == 0 &&
             membership.AssignedConnectionType == connectionType &&
             membership.AssignedLineNumber == lineNumber);
    }

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
    public Guid? ReplyToMessageId { get; init; }
    public Guid? ForwardSourceMessageId { get; init; }
    public Guid? CorrectsMessageId { get; init; }
}

public sealed record SendReactionRequest(string Emoji, string? IdempotencyKey = null);

public sealed record EditMessageRequest(string Content);

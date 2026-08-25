using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Conversations.Queries;
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

        group.MapPost("/{conversationId:guid}/messages", SendMessageAsync)
            .WithName("SendMessage");

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
        string? operatorUserId = null)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();

        if (!await IsTenantActiveAsync(currentTenant.TenantId.Value, dbContext))
            return Results.StatusCode(StatusCodes.Status423Locked);

        List<string>? phoneNumberIds = null;
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
            phoneNumberIds = await ResolvePhoneNumberIdsAsync(
                membership, currentTenant.TenantId.Value, accountRepository);
            if (phoneNumberIds.Count == 0)
                return Results.Ok(new CursorPaginationResponse<ConversationDto>());
        }

        var result = await conversationQueries.GetConversationsAsync(
            currentTenant.TenantId.Value,
            new CursorPaginationRequest { Cursor = cursor, Limit = limit },
            operatorUserId,
            phoneNumberIds);

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

    private static async Task<IResult> SendMessageAsync(
        Guid conversationId,
        [FromBody] SendMessageRequest request,
        ICurrentTenant currentTenant,
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        ITenantMembershipRepository membershipRepository,
        IWhatsAppAccountRepository accountRepository,
        IOutboxMessageRepository outboxMessageRepository,
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

        if (currentTenant.UserRole == "Operator" && currentTenant.UserId is not null)
        {
            var membership = await membershipRepository.GetByUserAndTenantAsync(currentTenant.UserId.Value, currentTenant.TenantId.Value);
            membership?.LoadAssignedLinesFromJson();
            var phoneNumberIds = await ResolvePhoneNumberIdsAsync(membership, currentTenant.TenantId.Value, accountRepository);
            if (phoneNumberIds.Count == 0 ||
                (!phoneNumberIds.Contains(conversation.PhoneNumberId) && conversation.PhoneNumberId != "manual"))
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
        if (!isQrConversation && !conversation.IsWindowOpen(clock.UtcNow))
            return Results.BadRequest(new { error = "Window closed. Only templates allowed." });

        var idempotencyKey = request.IdempotencyKey ?? Guid.NewGuid().ToString();

        var existing = await messageRepository.GetByIdempotencyKeyAsync(idempotencyKey);
        if (existing is not null)
            return Results.Ok(new { id = existing.Id, status = existing.Status.ToString() });

        var message = Message.CreateOutbound(
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
        membership?.LoadAssignedLinesFromJson();
        var phoneNumberIds = await ResolvePhoneNumberIdsAsync(
            membership, currentTenant.TenantId.Value, accountRepository);

        return phoneNumberIds.Count > 0 &&
            (phoneNumberIds.Contains(conversation.PhoneNumberId) || conversation.PhoneNumberId == "manual");
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
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Automation.Policy;
using WhatsAppAI.Application.Contacts;
using WhatsAppAI.Application.Integrations;
using WhatsAppAI.Domain.Audit;
using WhatsAppAI.Domain.Identity;
using WhatsAppAI.Domain.Integrations;
using WhatsAppAI.Domain.Messaging;
using WhatsAppAI.Domain.Privacy;
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

        group.MapPost("/import", ImportContactsAsync)
            .WithName("ImportContacts")
            .DisableAntiforgery(); // Custom middleware validates cookie sessions; bearer requests do not use CSRF.

        group.MapGet("/{contactId:guid}", GetContactAsync)
            .WithName("GetContact");

        group.MapPut("/{contactId:guid}", UpdateContactAsync)
            .WithName("UpdateContact");

        group.MapDelete("/{contactId:guid}", DeleteContactAsync)
            .WithName("DeleteContact");

        group.MapGet("/{contactId:guid}/memory", ListCustomerMemoryAsync)
            .WithName("ListCustomerMemory");

        group.MapPost("/{contactId:guid}/memory", SaveCustomerMemoryAsync)
            .WithName("SaveCustomerMemory");

        group.MapDelete("/{contactId:guid}/memory/{memoryId:guid}", DeactivateCustomerMemoryAsync)
            .WithName("DeactivateCustomerMemory");

        group.MapPost("/{contactId:guid}/start-conversation", StartConversationAsync)
            .WithName("StartConversation");

        return app;
    }

    private static async Task<IResult> ImportContactsAsync(
        IFormFile file,
        ICurrentTenant currentTenant,
        ContactImportService importService,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();

        if (currentTenant.UserRole != "TenantOwner")
            return Results.Forbid();

        if (file.Length == 0)
            return Results.BadRequest(new { error = "Selecione um arquivo para importar." });

        if (file.Length > 2 * 1024 * 1024)
            return Results.Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "O arquivo deve ter no máximo 2 MB.");

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await importService.ImportAsync(
                currentTenant.TenantId.Value,
                stream,
                file.FileName,
                cancellationToken);
            return Results.Ok(result);
        }
        catch (ContactImportFileException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
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

        List<Guid>? queueContactIds = null;
        if (queueId.HasValue)
        {
            queueContactIds = await dbContext.Conversations
                .Where(conversation =>
                    conversation.TenantId == currentTenant.TenantId.Value &&
                    conversation.QueueId == queueId.Value &&
                    conversation.Status == ConversationStatus.Open)
                .Select(conversation => conversation.ContactId)
                .Distinct()
                .ToListAsync();
        }

        var query = dbContext.Contacts
            .Where(c =>
                c.TenantId == currentTenant.TenantId.Value &&
                !c.PhoneNumber.StartsWith("anon-"));

        if (queueContactIds is not null)
            query = query.Where(c => queueContactIds.Contains(c.Id));

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
        AppDbContext dbContext,
        HttpContext httpContext,
        IWhatsAppAccountRepository accountRepository,
        ITenantMembershipRepository membershipRepository,
        IWhatsAppClient whatsAppClient)
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

            if (request.StartConversation)
            {
                var phoneNumberId = await ResolveOutboundPhoneNumberIdAsync(
                    currentTenant, accountRepository, membershipRepository, whatsAppClient,
                    request.PhoneNumberId,
                    currentTenant.TenantId.Value, httpContext.RequestAborted);
                if (phoneNumberId is null)
                    return Results.BadRequest(new { error = "No active WhatsApp line is configured for this tenant." });

                var conversation = await EnsureDirectConversationAsync(
                    currentTenant.TenantId.Value,
                    existing,
                    phoneNumberId,
                    dbContext,
                    httpContext.RequestAborted);

                return Results.Ok(new ContactResponse
                {
                    Id = existing.Id,
                    PhoneNumber = existing.PhoneNumber,
                    Name = existing.Name,
                    CreatedAt = existing.CreatedAt,
                    ConversationId = conversation.Id,
                    Message = "Contact already exists and conversation started"
                });
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
            var phoneNumberId = await ResolveOutboundPhoneNumberIdAsync(
                currentTenant, accountRepository, membershipRepository, whatsAppClient,
                request.PhoneNumberId,
                currentTenant.TenantId.Value, httpContext.RequestAborted);
            if (phoneNumberId is null)
                return Results.BadRequest(new { error = "No active WhatsApp line is configured for this tenant." });

            var conversation = await EnsureDirectConversationAsync(
                currentTenant.TenantId.Value,
                contact,
                phoneNumberId,
                dbContext,
                httpContext.RequestAborted);

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

        if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            var phone = new string(request.PhoneNumber.Where(char.IsDigit).ToArray());
            if (phone.Length is < 8 or > 15)
                return Results.BadRequest(new { error = "Phone number must contain between 8 and 15 digits." });

            if (await dbContext.Contacts.AnyAsync(c =>
                    c.Id != contactId &&
                    c.TenantId == currentTenant.TenantId.Value &&
                    c.PhoneNumber == phone))
                return Results.Conflict(new { error = "This phone number is already in use." });

            contact.UpdatePhoneNumber(phone);
        }

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
        [FromBody] StartConversationRequest? request,
        ICurrentTenant currentTenant,
        AppDbContext dbContext,
        HttpContext httpContext,
        IWhatsAppAccountRepository accountRepository,
        ITenantMembershipRepository membershipRepository,
        IWhatsAppClient whatsAppClient)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();

        var contact = await dbContext.Contacts
            .FirstOrDefaultAsync(c =>
                c.Id == contactId &&
                c.TenantId == currentTenant.TenantId.Value);

        if (contact is null)
            return Results.NotFound();

        var phoneNumberId = await ResolveOutboundPhoneNumberIdAsync(
            currentTenant, accountRepository, membershipRepository, whatsAppClient,
            request?.PhoneNumberId,
            currentTenant.TenantId.Value, httpContext.RequestAborted);
        if (phoneNumberId is null)
            return Results.BadRequest(new { error = "No active WhatsApp line is configured for this tenant." });

        var conversation = await EnsureDirectConversationAsync(
            currentTenant.TenantId.Value,
            contact,
            phoneNumberId,
            dbContext,
            httpContext.RequestAborted);

        return Results.Ok(new { conversationId = conversation.Id, message = "Conversation started" });
    }

    private static async Task<Conversation> EnsureDirectConversationAsync(
        Guid tenantId,
        Contact contact,
        string phoneNumberId,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var conversation = await dbContext.Conversations
            .FirstOrDefaultAsync(c =>
                c.TenantId == tenantId &&
                c.ContactId == contact.Id &&
                (c.PhoneNumberId == phoneNumberId || c.PhoneNumberId == "manual"),
                cancellationToken);

        if (conversation is not null)
        {
            if (conversation.PhoneNumberId == "manual")
                conversation.SetPhoneNumberId(phoneNumberId);

            if (conversation.Status == ConversationStatus.Closed)
            {
                conversation.Reopen();
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return conversation;
        }

        conversation = Conversation.Create(
            tenantId,
            contact.Id,
            phoneNumberId,
            ConversationMode.Human);
        conversation.RecordMessage();

        dbContext.Conversations.Add(conversation);
        await dbContext.SaveChangesAsync(cancellationToken);
        return conversation;
    }

    private static async Task<string?> ResolveOutboundPhoneNumberIdAsync(
        ICurrentTenant currentTenant,
        IWhatsAppAccountRepository accountRepository,
        ITenantMembershipRepository membershipRepository,
        IWhatsAppClient whatsAppClient,
        string? requestedPhoneNumberId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var accounts = (await accountRepository.GetAllByTenantAsync(tenantId, cancellationToken))
            .Where(account => account.IsActive && !string.IsNullOrWhiteSpace(account.PhoneNumberId))
            .ToList();

        if (accounts.Count == 0)
            return null;

        if (!string.IsNullOrWhiteSpace(requestedPhoneNumberId))
        {
            var selected = accounts.FirstOrDefault(account =>
                account.PhoneNumberId == requestedPhoneNumberId);
            if (selected is null)
                return null;

            if (currentTenant.UserRole == "Operator" && currentTenant.UserId is not null)
            {
                var membership = await membershipRepository.GetByUserAndTenantAsync(
                    currentTenant.UserId.Value, tenantId);
                membership?.LoadAssignedLinesFromJson();
                var assignedLines = membership is not null && membership.AssignedLines.Count > 0
                    ? membership.AssignedLines
                    : membership?.AssignedConnectionType is not null && membership.AssignedLineNumber is not null
                        ? [new LineAssignment(membership.AssignedConnectionType.Value, membership.AssignedLineNumber.Value)]
                        : [];
                if (!assignedLines.Any(line =>
                        line.ConnectionType == selected.ConnectionType &&
                        line.LineNumber == selected.LineNumber))
                    return null;
            }

            return selected.PhoneNumberId;
        }

        if (currentTenant.UserRole == "Operator" && currentTenant.UserId is not null)
        {
            var membership = await membershipRepository.GetByUserAndTenantAsync(
                currentTenant.UserId.Value, tenantId);
            if (membership is not null)
            {
                membership.LoadAssignedLinesFromJson();
                var assignedLines = membership.AssignedLines.Count > 0
                    ? membership.AssignedLines
                    : membership.AssignedConnectionType is not null && membership.AssignedLineNumber is not null
                        ? [new LineAssignment(membership.AssignedConnectionType.Value, membership.AssignedLineNumber.Value)]
                        : [];

                foreach (var line in assignedLines)
                {
                    var assignedAccount = accounts.FirstOrDefault(account =>
                        account.ConnectionType == line.ConnectionType &&
                        account.LineNumber == line.LineNumber);
                    if (assignedAccount is not null)
                        return assignedAccount.PhoneNumberId;
                }
            }
        }

        // A direct conversation started by an owner has no operator assignment.
        // Prefer a QR line whose session is actually connected, rather than the
        // lowest numbered active account, which may still be waiting for a QR scan.
        foreach (var account in accounts
            .Where(account => account.ConnectionType == WhatsAppConnectionType.QrCode)
            .OrderBy(account => account.LineNumber))
        {
            var status = await whatsAppClient.GetSessionStatusAsync(
                tenantId, account.LineNumber, cancellationToken);
            if (status.IsConnected)
                return account.PhoneNumberId;
        }

        return accounts
            .Where(account => account.ConnectionType == WhatsAppConnectionType.OfficialApi)
            .OrderBy(account => account.LineNumber)
            .Select(account => account.PhoneNumberId)
            .FirstOrDefault();
    }

    private static async Task<IResult> DeleteContactAsync(
        Guid contactId,
        ICurrentTenant currentTenant,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null || currentTenant.UserId is null)
            return Results.Unauthorized();

        var tenantId = currentTenant.TenantId.Value;
        var contact = await dbContext.Contacts
            .FirstOrDefaultAsync(c => c.Id == contactId && c.TenantId == tenantId, cancellationToken);
        if (contact is null || contact.PhoneNumber.StartsWith("anon-"))
            return Results.NotFound();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var messages = await dbContext.Messages
            .Where(x => x.ContactId == contactId && x.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        var evidence = await dbContext.ConsentEvidence
            .Where(x => x.ContactId == contactId && x.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        var memories = await dbContext.CustomerMemories
            .Where(x => x.ContactId == contactId && x.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        contact.Anonymize();
        foreach (var message in messages)
            message.RedactPersonalData();
        foreach (var item in evidence)
            item.RedactReference();
        foreach (var memory in memories)
            memory.Redact();

        dbContext.AuditLogs.Add(AuditLog.Create(
            tenantId,
            currentTenant.UserId,
            "Contact.Anonymized",
            "Contact",
            contactId.ToString()));

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ListCustomerMemoryAsync(
        Guid contactId,
        ICurrentTenant currentTenant,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!CanManageMemory(currentTenant))
            return Results.Forbid();

        var tenantId = currentTenant.TenantId!.Value;
        var contactExists = await dbContext.Contacts
            .AnyAsync(contact => contact.Id == contactId && contact.TenantId == tenantId, cancellationToken);
        if (!contactExists)
            return Results.NotFound();

        var consent = await GetActiveAiConsentAsync(dbContext, tenantId, contactId, cancellationToken);
        var now = DateTime.UtcNow;
        IReadOnlyList<CustomerMemoryResponse> memories = consent is null
            ? []
            : (await dbContext.CustomerMemories
                .Where(memory =>
                    memory.TenantId == tenantId &&
                    memory.ContactId == contactId &&
                    memory.IsActive &&
                    memory.ExpiresAt > now)
                .OrderByDescending(memory => memory.UpdatedAt ?? memory.CreatedAt)
                .ToListAsync(cancellationToken))
                .Select(ToCustomerMemoryResponse)
                .ToList();

        return Results.Ok(new
        {
            consentGranted = consent is not null,
            consentGrantedAt = consent?.GrantedAt,
            consentPurpose = AiConsentOptInPolicy.DefaultPurposeName,
            items = memories
        });
    }

    private static async Task<IResult> SaveCustomerMemoryAsync(
        Guid contactId,
        [FromBody] SaveCustomerMemoryRequest request,
        ICurrentTenant currentTenant,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!CanManageMemory(currentTenant))
            return Results.Forbid();

        var tenantId = currentTenant.TenantId!.Value;
        var contactExists = await dbContext.Contacts
            .AnyAsync(contact => contact.Id == contactId && contact.TenantId == tenantId, cancellationToken);
        if (!contactExists)
            return Results.NotFound();

        var consent = await GetActiveAiConsentAsync(dbContext, tenantId, contactId, cancellationToken);
        if (consent is null)
        {
            return Results.Conflict(new
            {
                code = "consent_required",
                error = "O contato precisa autorizar o atendimento automatizado respondendo SIM antes de salvar uma memória."
            });
        }

        if (!CustomerMemoryPolicy.TryNormalize(
                request.Key,
                request.Value,
                out var key,
                out var value,
                out var validationError))
        {
            return Results.BadRequest(new { error = validationError });
        }

        var now = DateTime.UtcNow;
        var expiresAt = request.ExpiresAt.HasValue
            ? ToUtc(request.ExpiresAt.Value)
            : now.AddDays(consent.ProcessingPurpose.RetentionDays);
        var maximumExpiration = now.AddDays(consent.ProcessingPurpose.RetentionDays);
        if (expiresAt <= now || expiresAt > maximumExpiration)
        {
            return Results.BadRequest(new
            {
                error = $"A validade deve estar entre agora e {consent.ProcessingPurpose.RetentionDays} dias."
            });
        }

        var memory = await dbContext.CustomerMemories
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(item =>
                item.TenantId == tenantId &&
                item.ContactId == contactId &&
                item.Key == key,
                cancellationToken);

        if (memory is null)
        {
            memory = CustomerMemory.Create(
                tenantId,
                contactId,
                consent.Id,
                key,
                value,
                CustomerMemorySource.OperatorConfirmed,
                expiresAt,
                currentTenant.UserId!.Value);
            dbContext.CustomerMemories.Add(memory);
        }
        else
        {
            memory.Replace(
                consent.Id,
                value,
                CustomerMemorySource.OperatorConfirmed,
                expiresAt);
        }

        dbContext.AuditLogs.Add(AuditLog.Create(
            tenantId,
            currentTenant.UserId,
            "CustomerMemory.Saved",
            "CustomerMemory",
            memory.Id.ToString(),
            "source=operator-confirmed"));
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToCustomerMemoryResponse(memory));
    }

    private static async Task<IResult> DeactivateCustomerMemoryAsync(
        Guid contactId,
        Guid memoryId,
        ICurrentTenant currentTenant,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!CanManageMemory(currentTenant))
            return Results.Forbid();

        var tenantId = currentTenant.TenantId!.Value;
        var memory = await dbContext.CustomerMemories
            .FirstOrDefaultAsync(item =>
                item.Id == memoryId &&
                item.ContactId == contactId &&
                item.TenantId == tenantId,
                cancellationToken);
        if (memory is null)
            return Results.NotFound();

        memory.Deactivate();
        dbContext.AuditLogs.Add(AuditLog.Create(
            tenantId,
            currentTenant.UserId,
            "CustomerMemory.Deactivated",
            "CustomerMemory",
            memory.Id.ToString(),
            "source=operator"));
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static Task<ConsentEvidence?> GetActiveAiConsentAsync(
        AppDbContext dbContext,
        Guid tenantId,
        Guid contactId,
        CancellationToken cancellationToken) =>
        dbContext.ConsentEvidence
            .Include(evidence => evidence.ProcessingPurpose)
            .Where(evidence =>
                evidence.TenantId == tenantId &&
                evidence.ContactId == contactId &&
                evidence.RevokedAt == null &&
                evidence.ProcessingPurpose.TenantId == tenantId &&
                evidence.ProcessingPurpose.IsActive &&
                evidence.ProcessingPurpose.Name == AiConsentOptInPolicy.DefaultPurposeName)
            .OrderByDescending(evidence => evidence.GrantedAt)
            .FirstOrDefaultAsync(cancellationToken);

    private static CustomerMemoryResponse ToCustomerMemoryResponse(CustomerMemory memory) => new(
        memory.Id,
        memory.Key,
        memory.Value,
        memory.Source.ToString(),
        memory.ExpiresAt,
        memory.CreatedAt,
        memory.UpdatedAt);

    private static bool CanManageMemory(ICurrentTenant currentTenant) =>
        currentTenant.TenantId.HasValue &&
        currentTenant.UserId.HasValue &&
        (currentTenant.UserRole is "TenantOwner" or "Operator" ||
         currentTenant.IsPlatformAdmin && currentTenant.SupportSession is not null);

    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}

public sealed class CreateContactRequest
{
    public string PhoneNumber { get; init; } = string.Empty;
    public string? Name { get; init; }
    public bool StartConversation { get; init; }
    public string? PhoneNumberId { get; init; }
}

public sealed class StartConversationRequest
{
    public string? PhoneNumberId { get; init; }
}

public sealed class UpdateContactRequest
{
    public string? PhoneNumber { get; init; }
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

public sealed record SaveCustomerMemoryRequest(string Key, string Value, DateTime? ExpiresAt);

public sealed record CustomerMemoryResponse(
    Guid Id,
    string Key,
    string Value,
    string Source,
    DateTime ExpiresAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

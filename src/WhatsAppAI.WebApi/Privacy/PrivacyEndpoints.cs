using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Audit;
using WhatsAppAI.Domain.Privacy;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.WebApi.Privacy;

public static class PrivacyEndpoints
{
    public static IEndpointRouteBuilder MapPrivacyEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/privacy/notice", GetNotice).AllowAnonymous();

        var group = app.MapGroup("/api/privacy")
            .WithTags("Privacy")
            .RequireAuthorization("RequireTenantContext");

        group.MapGet("/purposes", ListPurposesAsync);
        group.MapPost("/purposes", CreatePurposeAsync);
        group.MapPost("/consents", RecordConsentAsync);
        group.MapPost("/consents/{id:guid}/revoke", RevokeConsentAsync);
        group.MapGet("/requests", ListRequestsAsync);
        group.MapPost("/requests", CreateRequestAsync);
        group.MapGet("/requests/{id:guid}/export", ExportRequestAsync);
        group.MapPost("/requests/{id:guid}/erase", EraseRequestAsync);
        group.MapPost("/requests/{id:guid}/deny", DenyRequestAsync);

        return app;
    }

    private static IResult GetNotice(IConfiguration configuration)
    {
        var controllerName = NullIfBlank(configuration["Privacy:ControllerName"]);
        var controllerRegistration = NullIfBlank(configuration["Privacy:ControllerRegistration"]);
        var privacyEmail = NullIfBlank(configuration["Privacy:PrivacyEmail"]);
        var dpoName = NullIfBlank(configuration["Privacy:DpoName"]);
        var dpoContact = NullIfBlank(configuration["Privacy:DpoContact"]);
        var dpoExemptionReason = NullIfBlank(configuration["Privacy:DpoExemptionReason"]);
        var policyVersion = NullIfBlank(configuration["Privacy:PolicyVersion"]);
        var isComplete = controllerName is not null
            && privacyEmail is not null
            && (dpoName is not null || dpoExemptionReason is not null);

        return Results.Ok(new
        {
            controllerName,
            controllerRegistration,
            privacyEmail,
            dpoName,
            dpoContact,
            dpoExemptionReason,
            policyVersion,
            configurationComplete = isComplete,
            rights = new[]
            {
                "confirmation", "access", "correction", "portability",
                "anonymization", "blocking", "erasure", "consent-revocation"
            }
        });
    }

    private static async Task<IResult> ListPurposesAsync(
        ICurrentTenant currentTenant,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!CanManage(currentTenant))
            return Results.Forbid();

        var purposes = await dbContext.ProcessingPurposes
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Description,
                legalBasis = x.LegalBasis.ToString(),
                x.RetentionDays,
                x.IsActive,
                x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(purposes);
    }

    private static async Task<IResult> CreatePurposeAsync(
        [FromBody] CreatePurposeRequest request,
        ICurrentTenant currentTenant,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!CanManage(currentTenant))
            return Results.Forbid();
        if (!Enum.TryParse<LegalBasis>(request.LegalBasis, true, out var legalBasis))
            return Results.BadRequest(new { error = "Invalid legal basis." });

        ProcessingPurpose purpose;
        try
        {
            purpose = ProcessingPurpose.Create(
                currentTenant.TenantId!.Value,
                request.Name,
                request.Description,
                legalBasis,
                request.RetentionDays,
                currentTenant.UserId!.Value);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }

        dbContext.ProcessingPurposes.Add(purpose);
        AddAudit(dbContext, currentTenant, "Privacy.PurposeCreated", "ProcessingPurpose", purpose.Id, legalBasis.ToString());
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/privacy/purposes/{purpose.Id}", new { purpose.Id });
    }

    private static async Task<IResult> RecordConsentAsync(
        [FromBody] RecordConsentRequest request,
        ICurrentTenant currentTenant,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!CanManage(currentTenant))
            return Results.Forbid();

        var tenantId = currentTenant.TenantId!.Value;
        var contactExists = await dbContext.Contacts
            .AnyAsync(x => x.Id == request.ContactId && x.TenantId == tenantId, cancellationToken);
        var purpose = await dbContext.ProcessingPurposes
            .FirstOrDefaultAsync(x => x.Id == request.ProcessingPurposeId && x.TenantId == tenantId, cancellationToken);
        if (!contactExists || purpose is null)
            return Results.NotFound();

        ConsentEvidence evidence;
        try
        {
            evidence = ConsentEvidence.Create(
                tenantId,
                request.ContactId,
                purpose,
                request.Source,
                request.EvidenceReference,
                request.GrantedAt ?? DateTime.UtcNow,
                currentTenant.UserId!.Value);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }

        dbContext.ConsentEvidence.Add(evidence);
        AddAudit(dbContext, currentTenant, "Privacy.ConsentRecorded", "ConsentEvidence", evidence.Id);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/privacy/consents/{evidence.Id}", new { evidence.Id });
    }

    private static async Task<IResult> RevokeConsentAsync(
        Guid id,
        ICurrentTenant currentTenant,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!CanManage(currentTenant))
            return Results.Forbid();

        var evidence = await dbContext.ConsentEvidence
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == currentTenant.TenantId, cancellationToken);
        if (evidence is null)
            return Results.NotFound();

        evidence.Revoke(DateTime.UtcNow);
        AddAudit(dbContext, currentTenant, "Privacy.ConsentRevoked", "ConsentEvidence", evidence.Id);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { evidence.Id, evidence.RevokedAt });
    }

    private static async Task<IResult> ListRequestsAsync(
        ICurrentTenant currentTenant,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!CanManage(currentTenant))
            return Results.Forbid();

        return Results.Ok(await dbContext.DataSubjectRequests
            .OrderByDescending(x => x.RequestedAt)
            .Select(x => new
            {
                x.Id,
                x.ContactId,
                type = x.Type.ToString(),
                status = x.Status.ToString(),
                x.RequestedAt,
                x.DueAt,
                x.ResolvedAt,
                x.DecisionReason,
                x.ReviewAt
            })
            .ToListAsync(cancellationToken));
    }

    private static async Task<IResult> CreateRequestAsync(
        [FromBody] CreateDataSubjectRequest request,
        ICurrentTenant currentTenant,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!CanManage(currentTenant))
            return Results.Forbid();
        if (!Enum.TryParse<DataSubjectRequestType>(request.Type, true, out var type))
            return Results.BadRequest(new { error = "Invalid request type." });

        var tenantId = currentTenant.TenantId!.Value;
        var contactExists = await dbContext.Contacts
            .AnyAsync(x => x.Id == request.ContactId && x.TenantId == tenantId, cancellationToken);
        if (!contactExists)
            return Results.NotFound();

        var dataRequest = DataSubjectRequest.Create(
            tenantId, request.ContactId, type, currentTenant.UserId!.Value);
        dbContext.DataSubjectRequests.Add(dataRequest);
        AddAudit(dbContext, currentTenant, "Privacy.RequestOpened", "DataSubjectRequest", dataRequest.Id, type.ToString());
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/privacy/requests/{dataRequest.Id}", new
        {
            dataRequest.Id,
            type = dataRequest.Type.ToString(),
            status = dataRequest.Status.ToString(),
            dataRequest.DueAt
        });
    }

    private static async Task<IResult> ExportRequestAsync(
        Guid id,
        ICurrentTenant currentTenant,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!CanManage(currentTenant))
            return Results.Forbid();

        var tenantId = currentTenant.TenantId!.Value;
        var request = await dbContext.DataSubjectRequests
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, cancellationToken);
        if (request is null || request.Type is not (DataSubjectRequestType.Access or DataSubjectRequestType.Portability))
            return Results.NotFound();

        var contact = await dbContext.Contacts
            .Where(x => x.Id == request.ContactId && x.TenantId == tenantId)
            .Select(x => new
            {
                x.Id,
                x.PhoneNumber,
                x.Name,
                x.ProfilePictureUrl,
                x.CreatedAt,
                x.UpdatedAt,
                x.LastMessageAt
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (contact is null)
            return Results.NotFound();

        var conversations = await dbContext.Conversations
            .Where(x => x.ContactId == request.ContactId && x.TenantId == tenantId)
            .Select(x => new { x.Id, mode = x.Mode.ToString(), status = x.Status.ToString(), x.CreatedAt, x.LastMessageAt })
            .ToListAsync(cancellationToken);
        var messages = await dbContext.Messages
            .Where(x => x.ContactId == request.ContactId && x.TenantId == tenantId)
            .OrderBy(x => x.CreatedAt)
            .Take(10_000)
            .Select(x => new
            {
                x.Id,
                x.ConversationId,
                direction = x.Direction.ToString(),
                type = x.Type.ToString(),
                x.Content,
                x.Caption,
                x.CreatedAt
            })
            .ToListAsync(cancellationToken);
        var customerMemories = await dbContext.CustomerMemories
            .IgnoreQueryFilters()
            .Where(x => x.ContactId == request.ContactId && x.TenantId == tenantId)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.Key,
                x.Value,
                source = x.Source.ToString(),
                x.ExpiresAt,
                x.IsActive,
                x.CreatedAt,
                x.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        AddAudit(dbContext, currentTenant, "Privacy.DataExported", "DataSubjectRequest", request.Id);
        if (request.Status == DataSubjectRequestStatus.Open)
            request.Complete(currentTenant.UserId!.Value);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { requestId = request.Id, exportedAt = DateTime.UtcNow, contact, conversations, messages, customerMemories });
    }

    private static async Task<IResult> EraseRequestAsync(
        Guid id,
        ICurrentTenant currentTenant,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!CanManage(currentTenant))
            return Results.Forbid();

        var tenantId = currentTenant.TenantId!.Value;
        var request = await dbContext.DataSubjectRequests
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, cancellationToken);
        if (request is null)
            return Results.NotFound();
        if (request.Type is not (DataSubjectRequestType.Anonymization or DataSubjectRequestType.Erasure))
            return Results.Conflict(new { error = "Request type does not permit erasure." });
        if (request.Status == DataSubjectRequestStatus.Completed)
            return Results.Ok(new { request.Id, status = request.Status.ToString() });
        if (request.Status != DataSubjectRequestStatus.Open)
            return Results.Conflict(new { error = "Request is already resolved." });

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var contact = await dbContext.Contacts
            .FirstOrDefaultAsync(x => x.Id == request.ContactId && x.TenantId == tenantId, cancellationToken);
        if (contact is null)
            return Results.NotFound();

        var messages = await dbContext.Messages
            .Where(x => x.ContactId == contact.Id && x.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        var evidence = await dbContext.ConsentEvidence
            .Where(x => x.ContactId == contact.Id && x.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        var customerMemories = await dbContext.CustomerMemories
            .Where(x => x.ContactId == contact.Id && x.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        contact.Anonymize();
        foreach (var message in messages)
            message.RedactPersonalData();
        foreach (var item in evidence)
            item.RedactReference();
        foreach (var memory in customerMemories)
            memory.Redact();
        request.Complete(currentTenant.UserId!.Value);
        AddAudit(dbContext, currentTenant, "Privacy.DataErased", "DataSubjectRequest", request.Id);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(new { request.Id, status = request.Status.ToString() });
    }

    private static async Task<IResult> DenyRequestAsync(
        Guid id,
        [FromBody] DenyDataSubjectRequest requestBody,
        ICurrentTenant currentTenant,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!CanManage(currentTenant))
            return Results.Forbid();

        var request = await dbContext.DataSubjectRequests
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == currentTenant.TenantId, cancellationToken);
        if (request is null)
            return Results.NotFound();

        try
        {
            request.Deny(currentTenant.UserId!.Value, requestBody.Reason, requestBody.ReviewAt);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new { error = ex.Message });
        }

        AddAudit(dbContext, currentTenant, "Privacy.RequestDenied", "DataSubjectRequest", request.Id);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { request.Id, status = request.Status.ToString(), request.ReviewAt });
    }

    private static bool CanManage(ICurrentTenant currentTenant) =>
        currentTenant.TenantId.HasValue
        && currentTenant.UserId.HasValue
        && (currentTenant.UserRole == "TenantOwner"
            || currentTenant.IsPlatformAdmin && currentTenant.SupportSession is not null);

    private static void AddAudit(
        AppDbContext dbContext,
        ICurrentTenant currentTenant,
        string action,
        string entityType,
        Guid entityId,
        string? details = null) =>
        dbContext.AuditLogs.Add(AuditLog.Create(
            currentTenant.TenantId!.Value,
            currentTenant.UserId,
            action,
            entityType,
            entityId.ToString(),
            details));

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record CreatePurposeRequest(
    string Name,
    string Description,
    string LegalBasis,
    int RetentionDays);

public sealed record RecordConsentRequest(
    Guid ContactId,
    Guid ProcessingPurposeId,
    string Source,
    string? EvidenceReference,
    DateTime? GrantedAt);

public sealed record CreateDataSubjectRequest(Guid ContactId, string Type);

public sealed record DenyDataSubjectRequest(string Reason, DateTime ReviewAt);

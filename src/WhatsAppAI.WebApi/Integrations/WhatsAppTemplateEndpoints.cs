using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Audit;
using WhatsAppAI.Application.Integrations;
using WhatsAppAI.Domain.Integrations;
using WhatsAppAI.Infrastructure.Identity;
using WhatsAppAI.Infrastructure.Workers;

namespace WhatsAppAI.WebApi.Integrations;

public static class WhatsAppTemplateEndpoints
{
    public static IEndpointRouteBuilder MapWhatsAppTemplateEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/integrations/whatsapp/official/{lineNumber:int}/templates")
            .WithTags("Integrations - WhatsApp Templates")
            .RequireAuthorization("RequireTenantContext");
        group.MapGet("/", ListAsync).WithName("ListWhatsAppTemplates");
        group.MapPost("/", CreateAsync).WithName("CreateWhatsAppTemplate");
        group.MapPost("/sync", SynchronizeAsync).WithName("SynchronizeWhatsAppTemplates");
        return app;
    }

    private static async Task<IResult> ListAsync(int lineNumber, [FromQuery] string? cursor, [FromQuery] int? limit,
        ICurrentTenant currentTenant, IWhatsAppAccountRepository accounts, IWhatsAppTemplateRepository templates,
        CancellationToken cancellationToken)
    {
        if (!TryGetOwner(currentTenant, out var tenantId, out var result)) return result!;
        var account = await accounts.GetByTenantAndSlotAsync(tenantId, WhatsAppConnectionType.OfficialApi, lineNumber, cancellationToken);
        if (account is null) return Results.NotFound(new { error = "Linha oficial ativa não encontrada." });
        var waba = await templates.EnsureBusinessAccountAsync(account, cancellationToken);
        if (!TryDecodeCursor(cursor, out var skip))
            return Results.BadRequest(new { error = "Cursor inválido." });

        var pageSize = limit is null or <= 0 ? 50 : Math.Clamp(limit.Value, 1, 100);
        var catalog = await templates.ListTemplatesAsync(tenantId, waba.Id, skip, pageSize + 1, cancellationToken);
        var hasMore = catalog.Count > pageSize;
        var page = hasMore ? catalog.Take(pageSize) : catalog;
        return Results.Ok(new { templates = page.Select(ToResponse), nextCursor = hasMore ? EncodeCursor(skip + pageSize) : null });
    }

    private static async Task<IResult> CreateAsync(int lineNumber, [FromBody] CreateWhatsAppTemplateRequest request,
        HttpContext httpContext, ICurrentTenant currentTenant, IWhatsAppAccountRepository accounts,
        IWhatsAppTemplateRepository templates, AuditService auditService, CancellationToken cancellationToken)
    {
        if (!TryGetOwner(currentTenant, out var tenantId, out var result)) return result!;
        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length is < 8 or > 200)
            return Results.BadRequest(new { error = "O cabeçalho Idempotency-Key é obrigatório e deve ter entre 8 e 200 caracteres." });

        var input = new WhatsAppTemplateCreateRequest(request.Name?.Trim() ?? string.Empty, request.Language?.Trim() ?? string.Empty,
            request.Category?.Trim().ToUpperInvariant() ?? string.Empty, request.BodyText ?? string.Empty,
            request.BodyExamples ?? [], request.FooterText?.Trim());
        var errors = WhatsAppTemplateValidator.Validate(input);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var fingerprint = ComputeFingerprint(input);
        var existing = await templates.GetSubmissionByIdempotencyKeyAsync(tenantId, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.RequestFingerprint, fingerprint, StringComparison.Ordinal))
                return Results.Conflict(new { error = "A chave idempotente foi reutilizada com outro conteúdo." });
            return Results.Accepted($"/api/integrations/whatsapp/official/{lineNumber}/templates", ToOperation(existing));
        }

        var account = await accounts.GetByTenantAndSlotAsync(tenantId, WhatsAppConnectionType.OfficialApi, lineNumber, cancellationToken);
        if (account is null) return Results.NotFound(new { error = "Linha oficial ativa não encontrada." });
        var waba = await templates.EnsureBusinessAccountAsync(account, cancellationToken);
        var variant = await templates.GetTemplateAsync(tenantId, waba.Id, input.Name, input.Language, cancellationToken);
        if (variant is not null)
            return Results.Conflict(new { error = "Já existe uma variante com esse nome e idioma nesta WABA." });

        var parameterCount = CountParameters(input.BodyText);
        var template = WhatsAppMessageTemplate.CreatePending(tenantId, waba.Id, input.Name, input.Language, input.Category,
            input.BodyText, input.FooterText, input.BodyExamples, parameterCount);
        var submission = WhatsAppTemplateSubmission.Queue(tenantId, waba.Id, template.Id, account.Id, idempotencyKey, fingerprint,
            httpContext.TraceIdentifier);
        try
        {
            await templates.AddTemplateAndSubmissionAsync(template, submission, cancellationToken);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            var raced = await templates.GetSubmissionByIdempotencyKeyAsync(tenantId, idempotencyKey, cancellationToken);
            if (raced is not null && raced.RequestFingerprint == fingerprint)
                return Results.Accepted($"/api/integrations/whatsapp/official/{lineNumber}/templates", ToOperation(raced));
            return Results.Conflict(new { error = "Não foi possível criar uma submissão distinta para esta variante." });
        }

        await auditService.LogAsync(tenantId, currentTenant.UserId, "whatsapp_template.submission_queued", "WhatsAppMessageTemplate",
            template.Id.ToString(), $"name={template.Name};language={template.Language};category={template.RequestedCategory};correlation={httpContext.TraceIdentifier}", cancellationToken: cancellationToken);
        return Results.Accepted($"/api/integrations/whatsapp/official/{lineNumber}/templates", ToOperation(submission));
    }

    private static async Task<IResult> SynchronizeAsync(int lineNumber, HttpContext httpContext, ICurrentTenant currentTenant,
        IWhatsAppAccountRepository accounts, WhatsAppTemplateCatalogSyncService sync, AuditService auditService,
        CancellationToken cancellationToken)
    {
        if (!TryGetOwner(currentTenant, out var tenantId, out var result)) return result!;
        var account = await accounts.GetByTenantAndSlotAsync(tenantId, WhatsAppConnectionType.OfficialApi, lineNumber, cancellationToken);
        if (account is null) return Results.NotFound(new { error = "Linha oficial ativa não encontrada." });
        var syncResult = await sync.SynchronizeAsync(account, cancellationToken);
        if (!syncResult.IsSuccess) return Results.BadRequest(new { error = syncResult.ErrorMessage ?? "Não foi possível sincronizar o catálogo." });
        await auditService.LogAsync(tenantId, currentTenant.UserId, "whatsapp_template.catalog_synchronized", "WhatsAppAccount",
            account.Id.ToString(), $"line={lineNumber};count={syncResult.Templates.Count};correlation={httpContext.TraceIdentifier}", cancellationToken: cancellationToken);
        return Results.Accepted($"/api/integrations/whatsapp/official/{lineNumber}/templates", new { status = "QUEUED", count = syncResult.Templates.Count });
    }

    private static bool TryGetOwner(ICurrentTenant currentTenant, out Guid tenantId, out IResult? failure)
    {
        tenantId = currentTenant.TenantId ?? Guid.Empty;
        if (currentTenant.TenantId is null) { failure = Results.Unauthorized(); return false; }
        if (currentTenant.UserRole != "TenantOwner") { failure = Results.Forbid(); return false; }
        failure = null;
        return true;
    }

    private static int CountParameters(string body) => System.Text.RegularExpressions.Regex.Matches(body, @"\{\{[1-9]\d*\}\}").Select(x => x.Value).Distinct().Count();
    private static bool TryDecodeCursor(string? cursor, out int skip)
    {
        skip = 0;
        if (string.IsNullOrWhiteSpace(cursor)) return true;
        try
        {
            var value = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            return int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out skip) && skip >= 0;
        }
        catch (FormatException) { return false; }
    }
    private static string EncodeCursor(int skip) => Convert.ToBase64String(Encoding.UTF8.GetBytes(skip.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    private static string ComputeFingerprint(WhatsAppTemplateCreateRequest request) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        $"{request.Name}\n{request.Language}\n{request.Category}\n{request.BodyText}\n{request.FooterText}\n{string.Join("\u001F", request.BodyExamples)}"))).ToLowerInvariant();
    private static object ToOperation(WhatsAppTemplateSubmission submission) => new { templateId = submission.WhatsAppMessageTemplateId, submissionId = submission.Id, submissionStatus = submission.Status.ToString().ToUpperInvariant(), reviewStatus = "PENDING" };
    private static object ToResponse(WhatsAppMessageTemplate template) => new
    {
        id = template.Id,
        name = template.Name,
        language = template.Language,
        requestedCategory = template.RequestedCategory,
        effectiveCategory = template.EffectiveCategory,
        reviewStatus = template.ReviewStatus.ToString().ToUpperInvariant(),
        providerRawStatus = template.ProviderRawStatus,
        bodyText = template.BodyText,
        footerText = template.FooterText,
        bodyParameterCount = template.BodyParameterCount,
        isInboxCompatible = template.IsInboxCompatible,
        isBroadcastCompatible = template.IsBroadcastCompatible,
        rejectionReason = template.RejectionReason,
        recommendation = template.Recommendation,
        lastSyncedAt = template.LastSyncedAt
    };
}

public sealed class CreateWhatsAppTemplateRequest
{
    public string? Name { get; init; }
    public string? Language { get; init; }
    public string? Category { get; init; }
    public string? BodyText { get; init; }
    public IReadOnlyList<string>? BodyExamples { get; init; }
    public string? FooterText { get; init; }
}

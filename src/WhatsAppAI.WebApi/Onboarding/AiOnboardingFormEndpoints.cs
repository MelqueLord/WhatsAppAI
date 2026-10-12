using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Audit;
using WhatsAppAI.Domain.Onboarding;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.WebApi.Onboarding;

public static class AiOnboardingFormEndpoints
{
    private const string TokenHeaderName = "X-Onboarding-Token";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly HashSet<string> BusinessActivities =
    [
        "Loja ou comércio", "Clínica ou consultório", "Salão de beleza ou estética",
        "Restaurante ou alimentação", "Oficina ou assistência técnica", "Prestação de serviços",
        "Imobiliária", "Escola ou cursos", "Advocacia ou serviços jurídicos", "Outro"
    ];

    private static readonly HashSet<string> AiGoals =
    [
        "Responder às dúvidas dos clientes", "Apresentar produtos ou serviços", "Informar preços e condições",
        "Identificar clientes interessados em comprar", "Agendar horários ou serviços",
        "Receber pedidos ou solicitações", "Acompanhar clientes após a compra",
        "Resolver dúvidas e problemas comuns", "Encaminhar conversas para a equipe"
    ];

    private static readonly HashSet<string> HandoffConditions =
    [
        "Quando o cliente pedir para falar com uma pessoa", "Quando houver reclamação ou insatisfação",
        "Quando o assunto envolver pagamentos ou cobranças", "Quando a IA não conseguir resolver a solicitação",
        "Quando for necessário negociar condições especiais",
        "Quando o cliente demonstrar interesse em fechar uma compra ou contratação",
        "Em todas as situações que precisarem de atendimento humano"
    ];

    private static readonly HashSet<string> ServiceDays =
    ["De segunda a sexta-feira", "De segunda a sábado", "De segunda a domingo", "Todos os dias", "Em dias e horários variados"];

    private static readonly HashSet<string> ServiceHours =
    ["Das 8h às 17h", "Das 8h às 18h", "Das 9h às 18h", "Das 9h às 19h", "Outro horário", "Atendimento em turnos ou 24 horas"];

    private static readonly HashSet<string> OutsideHoursBehaviors =
    [
        "Continuar atendendo normalmente e respondendo às dúvidas",
        "Informar o horário de funcionamento e continuar coletando informações",
        "Registrar a solicitação para a equipe retornar no próximo expediente",
        "Encaminhar para um plantão, quando disponível"
    ];

    private static readonly HashSet<string> Departments =
    [
        "Vendas e novos clientes", "Agendamentos e reservas", "Financeiro e cobranças",
        "Suporte e assistência", "Pós-venda", "Entrega e acompanhamento de pedidos",
        "Recepção", "Não temos departamentos separados", "Outros"
    ];

    private static readonly HashSet<string> WhatsAppSituations =
    [
        "WhatsApp Business funcionando normalmente", "WhatsApp pessoal funcionando normalmente",
        "Já utilizo a API oficial do WhatsApp", "O número está bloqueado ou com restrições",
        "O número foi cancelado ou não tenho mais acesso", "Ainda não defini qual número vou utilizar"
    ];

    private static readonly HashSet<string> RestrictionAnswers = ["Não, nunca aconteceu", "Sim, já aconteceu", "Não sei informar"];

    private static readonly HashSet<string> RestrictionTypes =
    [
        "Bloqueio temporário", "Banimento da conta ou do número", "Restrição no envio de mensagens",
        "Cancelamento da linha telefônica", "Outro problema", "Não sei exatamente o que aconteceu"
    ];

    private static readonly HashSet<string> InformationSources =
    [
        "No site da empresa", "No Instagram ou em outra rede social", "Em um catálogo de produtos ou serviços",
        "Em documentos, arquivos ou tabelas", "Já estão cadastradas no sistema que utilizamos",
        "Ainda não temos essas informações organizadas"
    ];

    public static IEndpointRouteBuilder MapAiOnboardingFormEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/ai-onboarding-forms")
            .WithTags("AI onboarding forms")
            .RequireAuthorization("PlatformAdmin");

        admin.MapGet("/", ListAsync);
        admin.MapGet("/{formId:guid}", GetAsync);
        admin.MapPost("/", CreateAsync);
        admin.MapPut("/{formId:guid}", UpdateAsync);
        admin.MapDelete("/{formId:guid}", DeleteAsync);

        var publicGroup = app.MapGroup("/api/public/ai-onboarding")
            .WithTags("Public AI onboarding")
            .RequireRateLimiting("auth");

        publicGroup.MapGet("/", GetPublicAsync);
        publicGroup.MapPost("/", SubmitAsync);

        return app;
    }

    private static async Task<IResult> ListAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var rows = await (
            from form in db.AiOnboardingForms.IgnoreQueryFilters()
            join tenant in db.Tenants.AsNoTracking() on form.TenantId equals tenant.Id
            orderby form.CreatedAt descending
            select new { form, tenant.Name })
            .ToListAsync(cancellationToken);

        return Results.Ok(rows.Select(row => new AiOnboardingFormSummary(
            row.form.Id,
            row.form.TenantId,
            row.Name,
            row.form.CompanyName,
            ResolveStatus(row.form),
            row.form.CreatedAt,
            row.form.ExpiresAt,
            row.form.SubmittedAt)));
    }

    private static async Task<IResult> GetAsync(Guid formId, AppDbContext db, CancellationToken cancellationToken)
    {
        var result = await (
            from form in db.AiOnboardingForms.IgnoreQueryFilters()
            join tenant in db.Tenants.AsNoTracking() on form.TenantId equals tenant.Id
            where form.Id == formId
            select new { form, tenant.Name })
            .SingleOrDefaultAsync(cancellationToken);

        if (result is null)
            return Results.NotFound();

        AiOnboardingAnswers? answers = null;
        if (!string.IsNullOrWhiteSpace(result.form.AnswersJson))
            answers = JsonSerializer.Deserialize<AiOnboardingAnswers>(result.form.AnswersJson, JsonOptions);

        return Results.Ok(new AiOnboardingFormDetail(
            result.form.Id,
            result.form.TenantId,
            result.Name,
            ResolveStatus(result.form),
            result.form.CreatedAt,
            result.form.ExpiresAt,
            result.form.SubmittedAt,
            answers));
    }

    private static async Task<IResult> CreateAsync(
        CreateAiOnboardingFormRequest request,
        ICurrentTenant currentTenant,
        AppDbContext db,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (currentTenant.UserId is null)
            return Results.Unauthorized();

        var tenant = await db.Tenants.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.TenantId, cancellationToken);
        if (tenant is null)
            return Results.NotFound(new { error = "Empresa não encontrada." });

        var pendingForms = await db.AiOnboardingForms.IgnoreQueryFilters()
            .Where(form => form.TenantId == tenant.Id && form.Status == AiOnboardingFormStatus.Pending)
            .ToListAsync(cancellationToken);
        foreach (var pendingForm in pendingForms)
            pendingForm.Revoke();

        var rawToken = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var form = AiOnboardingForm.Create(
            tenant.Id,
            HashToken(rawToken),
            currentTenant.UserId.Value,
            DateTime.UtcNow.AddDays(30));

        db.AiOnboardingForms.Add(form);
        db.AuditLogs.Add(AuditLog.Create(
            tenant.Id,
            currentTenant.UserId,
            "AiOnboardingForm.Created",
            "AiOnboardingForm",
            form.Id.ToString(),
            "Public onboarding link generated.",
            httpContext.Connection.RemoteIpAddress?.ToString()));
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/admin/ai-onboarding-forms/{form.Id}", new
        {
            formId = form.Id,
            tenantId = tenant.Id,
            tenantName = tenant.Name,
            token = rawToken,
            expiresAt = form.ExpiresAt
        });
    }

    private static async Task<IResult> UpdateAsync(
        Guid formId,
        AiOnboardingAnswers request,
        ICurrentTenant currentTenant,
        AppDbContext db,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (currentTenant.UserId is null)
            return Results.Unauthorized();

        var form = await db.AiOnboardingForms.IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.Id == formId, cancellationToken);
        if (form is null)
            return Results.NotFound();
        if (form.Status != AiOnboardingFormStatus.Submitted)
            return Results.Conflict(new { error = "Somente formulários respondidos podem ser editados." });

        var validationError = Validate(request);
        if (validationError is not null)
            return Results.BadRequest(new { error = validationError });

        var normalized = Normalize(request);
        form.Revise(normalized.CompanyName, JsonSerializer.Serialize(normalized, JsonOptions));
        db.AuditLogs.Add(AuditLog.Create(
            form.TenantId,
            currentTenant.UserId,
            "AiOnboardingForm.Updated",
            "AiOnboardingForm",
            form.Id.ToString(),
            "Onboarding answers updated by platform administrator.",
            httpContext.Connection.RemoteIpAddress?.ToString()));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new { error = "As respostas foram alteradas. Atualize e tente novamente." });
        }

        return Results.NoContent();
    }

    private static async Task<IResult> DeleteAsync(
        Guid formId,
        ICurrentTenant currentTenant,
        AppDbContext db,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (currentTenant.UserId is null)
            return Results.Unauthorized();

        var form = await db.AiOnboardingForms.IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.Id == formId, cancellationToken);
        if (form is null)
            return Results.NotFound();

        db.AiOnboardingForms.Remove(form);
        db.AuditLogs.Add(AuditLog.Create(
            form.TenantId,
            currentTenant.UserId,
            "AiOnboardingForm.Deleted",
            "AiOnboardingForm",
            form.Id.ToString(),
            "Onboarding form deleted by platform administrator.",
            httpContext.Connection.RemoteIpAddress?.ToString()));
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetPublicAsync(
        AppDbContext db,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var token = GetToken(httpContext);
        var form = await FindByTokenAsync(token, db, cancellationToken);
        if (form is null)
            return Results.NotFound(new { error = "Formulário não encontrado." });

        var tenantName = await db.Tenants.AsNoTracking()
            .Where(tenant => tenant.Id == form.TenantId)
            .Select(tenant => tenant.Name)
            .SingleAsync(cancellationToken);

        return Results.Ok(new
        {
            available = form.IsUsable,
            status = ResolveStatus(form),
            tenantName,
            expiresAt = form.ExpiresAt
        });
    }

    private static async Task<IResult> SubmitAsync(
        AiOnboardingAnswers request,
        AppDbContext db,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var token = GetToken(httpContext);
        var form = await FindByTokenAsync(token, db, cancellationToken);
        if (form is null)
            return Results.NotFound(new { error = "Formulário não encontrado." });
        if (!form.IsUsable)
            return Results.Conflict(new { error = "Este formulário já foi enviado ou não está mais disponível." });

        var validationError = Validate(request);
        if (validationError is not null)
            return Results.BadRequest(new { error = validationError });

        var normalized = Normalize(request);
        form.Submit(normalized.CompanyName, JsonSerializer.Serialize(normalized, JsonOptions));
        db.AuditLogs.Add(AuditLog.Create(
            form.TenantId,
            null,
            "AiOnboardingForm.Submitted",
            "AiOnboardingForm",
            form.Id.ToString(),
            "Public onboarding form submitted.",
            httpContext.Connection.RemoteIpAddress?.ToString()));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new { error = "Este formulário já foi enviado." });
        }

        return Results.Ok(new { submitted = true, submittedAt = form.SubmittedAt });
    }

    private static async Task<AiOnboardingForm?> FindByTokenAsync(
        string? token,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 200)
            return null;

        var tokenHash = HashToken(token);
        return await db.AiOnboardingForms.IgnoreQueryFilters()
            .SingleOrDefaultAsync(form => form.TokenHash == tokenHash, cancellationToken);
    }

    private static string? GetToken(HttpContext httpContext) =>
        httpContext.Request.Headers[TokenHeaderName].FirstOrDefault();

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private static string ResolveStatus(AiOnboardingForm form) =>
        form.Status == AiOnboardingFormStatus.Pending && form.ExpiresAt <= DateTime.UtcNow
            ? "Expired"
            : form.Status.ToString();

    private static string? Validate(AiOnboardingAnswers value)
    {
        if (!HasLength(value.CompanyName, 2, 200)) return "Informe o nome da empresa.";
        if (!BusinessActivities.Contains(value.BusinessActivity)) return "Selecione a atividade principal da empresa.";
        if (!HasLength(value.ProductsOrServices, 2, 4000)) return "Informe os produtos ou serviços oferecidos.";
        if (!HasLength(value.AverageTicket, 1, 100)) return "Informe o ticket médio.";
        if (!HasValidSelections(value.AiGoals, AiGoals)) return "Selecione ao menos um objetivo válido para a IA.";
        if (!HasValidSelections(value.HandoffConditions, HandoffConditions)) return "Selecione ao menos uma regra de encaminhamento.";
        if (!ServiceDays.Contains(value.ServiceDays)) return "Selecione os dias de atendimento.";
        if (!ServiceHours.Contains(value.ServiceHours)) return "Selecione o horário de atendimento.";
        if (value.ServiceHours == "Outro horário" && !HasLength(value.CustomServiceHours, 2, 500)) return "Descreva o outro horário de atendimento.";
        if (!HasMaxLength(value.CustomServiceHours, 500)) return "O horário personalizado ultrapassa o limite permitido.";
        if (!OutsideHoursBehaviors.Contains(value.OutsideHoursBehavior)) return "Selecione o comportamento fora do expediente.";
        if (!HasValidSelections(value.Departments, Departments)) return "Selecione ao menos um setor da empresa.";
        if (value.Departments?.Contains("Outros") == true && !HasLength(value.OtherDepartment, 2, 300)) return "Informe os outros setores da empresa.";
        if (!HasMaxLength(value.OtherDepartment, 300)) return "A descrição dos setores ultrapassa o limite permitido.";
        if (!WhatsAppSituations.Contains(value.WhatsAppSituation)) return "Selecione a situação atual do WhatsApp.";
        if (!RestrictionAnswers.Contains(value.PreviousRestriction)) return "Informe se já houve bloqueio ou restrição.";
        if (value.PreviousRestriction == "Sim, já aconteceu" && !RestrictionTypes.Contains(value.RestrictionType ?? string.Empty)) return "Selecione o problema ocorrido com o WhatsApp.";
        if (!HasValidSelections(value.InformationSources, InformationSources)) return "Selecione onde estão as informações da empresa.";
        if (!HasMaxLength(value.InformationNotes, 2000)) return "As observações sobre os materiais ultrapassam o limite permitido.";
        if (!HasLength(value.AiInstructions, 2, 4000)) return "Informe ao menos uma orientação para a IA.";
        return null;
    }

    private static AiOnboardingAnswers Normalize(AiOnboardingAnswers value) => value with
    {
        CompanyName = value.CompanyName.Trim(),
        BusinessActivity = value.BusinessActivity.Trim(),
        ProductsOrServices = value.ProductsOrServices.Trim(),
        AverageTicket = value.AverageTicket.Trim(),
        AiGoals = value.AiGoals.Distinct().ToList(),
        HandoffConditions = value.HandoffConditions.Distinct().ToList(),
        CustomServiceHours = value.CustomServiceHours?.Trim(),
        Departments = value.Departments.Distinct().ToList(),
        OtherDepartment = value.OtherDepartment?.Trim(),
        RestrictionType = value.RestrictionType?.Trim(),
        InformationSources = value.InformationSources.Distinct().ToList(),
        InformationNotes = value.InformationNotes?.Trim(),
        AiInstructions = value.AiInstructions.Trim()
    };

    private static bool HasValidSelections(IReadOnlyCollection<string>? values, HashSet<string> allowed) =>
        values is { Count: > 0 } && allowed.IsSupersetOf(values);

    private static bool HasLength(string? value, int minimum, int maximum) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length >= minimum && value.Trim().Length <= maximum;

    private static bool HasMaxLength(string? value, int maximum) =>
        string.IsNullOrEmpty(value) || value.Length <= maximum;
}

public sealed record CreateAiOnboardingFormRequest(Guid TenantId);

public sealed record AiOnboardingAnswers(
    string CompanyName,
    string BusinessActivity,
    string ProductsOrServices,
    string AverageTicket,
    List<string> AiGoals,
    List<string> HandoffConditions,
    string ServiceDays,
    string ServiceHours,
    string? CustomServiceHours,
    string OutsideHoursBehavior,
    List<string> Departments,
    string? OtherDepartment,
    string WhatsAppSituation,
    string PreviousRestriction,
    string? RestrictionType,
    List<string> InformationSources,
    string? InformationNotes,
    string AiInstructions);

public sealed record AiOnboardingFormSummary(
    Guid Id,
    Guid TenantId,
    string TenantName,
    string? CompanyName,
    string Status,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    DateTime? SubmittedAt);

public sealed record AiOnboardingFormDetail(
    Guid Id,
    Guid TenantId,
    string TenantName,
    string Status,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    DateTime? SubmittedAt,
    AiOnboardingAnswers? Answers);

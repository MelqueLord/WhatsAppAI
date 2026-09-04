using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Automation;
using WhatsAppAI.Application.Automation.Context;
using WhatsAppAI.Application.Automation.Policy;
using WhatsAppAI.Domain;
using WhatsAppAI.Domain.Audit;
using WhatsAppAI.Domain.Integrations;
using WhatsAppAI.Infrastructure.Identity;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.WebApi.Integrations;

public static class AiProviderEndpoints
{
    public static IEndpointRouteBuilder MapAiProviderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/integrations/ai")
            .WithTags("AI Provider")
            .RequireAuthorization("RequireTenantContext");

        group.MapGet("/", GetConfigAsync)
            .WithName("GetAiConfig");

        group.MapGet("/providers", GetProvidersAsync)
            .WithName("GetAiProviders");

        group.MapPost("/toggle", ToggleAsync)
            .WithName("ToggleAi");

        group.MapPut("/instructions", UpdateInstructionsAsync)
            .WithName("UpdateAiInstructions");

        group.MapPost("/test-connection", TestConnectionAsync)
            .WithName("TestAiConnection");

        group.MapPost("/simulate", SimulateAsync)
            .WithName("SimulateAiDecision");

        group.MapPost("/evaluations/{evaluationId:guid}/rollback", RollbackModelAsync)
            .WithName("RollbackAiModel");

        return app;
    }

    private static IResult GetProvidersAsync(IAiProviderResolver resolver)
    {
        var registered = resolver.GetRegisteredProviders();
        var providers = AiProviderCatalog.Providers
            .Where(definition => registered.Contains(definition.Id, StringComparer.OrdinalIgnoreCase))
            .Select(definition => new
        {
            id = definition.Id,
            name = definition.Name,
            models = definition.Models
        });

        return Results.Ok(providers);
    }

    private static async Task<IResult> GetConfigAsync(
        ICurrentTenant currentTenant,
        IAiProviderCredentialRepository credentialRepository,
        IBotConfigurationRepository botConfigRepository,
        AppDbContext dbContext)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();
        if (currentTenant.UserRole != "TenantOwner")
            return Results.Forbid();

        if (!await dbContext.HasAiEnabledAsync(currentTenant.TenantId.Value))
            return Results.BadRequest(new { error = "AI not available in your plan." });

        var credential = await credentialRepository.GetByTenantAsync(currentTenant.TenantId.Value);
        var botConfig = await botConfigRepository.GetByTenantAsync(currentTenant.TenantId.Value);
        var provider = AiProviderCatalog.NormalizeProvider(credential?.Provider);
        var modelId = AiProviderCatalog.NormalizeModelId(credential?.ModelId);

        return Results.Ok(new
        {
            configured = credential is not null,
            provider = credential is null ? null : provider,
            modelId = credential is null ? null : modelId,
            systemPrompt = credential?.SystemPrompt,
            routingQueueIds = credential?.GetRoutingQueueIds() ?? [],
            routingTagIds = credential?.GetRoutingTagIds() ?? [],
            maxTokensPerResponse = credential?.MaxTokensPerResponse ?? 180,
            confidenceThreshold = botConfig?.ConfidenceThreshold ?? 0.5,
            guidelines = AiGuidelinePolicy.Rules,
            isActive = credential?.IsActive,
            version = credential?.Version,
            botVersion = botConfig?.Version,
            aiActive = botConfig?.Enabled == true && botConfig.Mode == BotMode.AiPowered
        });
    }

    private static async Task<IResult> SaveConfigAsync(
        [FromBody] SaveAiConfigRequest request,
        ICurrentTenant currentTenant,
        IAiProviderCredentialRepository credentialRepository,
        IBotConfigurationRepository botConfigRepository,
        IModelEvaluationRepository evaluationRepository,
        ISecretStore secretStore,
        IAuditLogRepository auditLogRepository,
        AppDbContext dbContext,
        HttpContext httpContext)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();
        if (currentTenant.UserRole != "TenantOwner")
            return Results.Forbid();

        if (!await dbContext.HasAiEnabledAsync(currentTenant.TenantId.Value))
            return Results.BadRequest(new { error = "AI not available in your plan." });

        var ifMatch = httpContext.Request.Headers["If-Match"].FirstOrDefault();
        if (ifMatch is null || !uint.TryParse(ifMatch, out var expectedVersion))
            return Results.BadRequest(new { error = "If-Match header com a versão é obrigatório." });

        var modelId = AiProviderCatalog.NormalizeModelId(request.ModelId);
        if (string.IsNullOrWhiteSpace(modelId))
            return Results.BadRequest(new { error = "Model ID is required." });

        var provider = AiProviderCatalog.NormalizeProvider(request.Provider ?? "openai");
        if (!AiProviderCatalog.IsSupported(provider))
            return Results.BadRequest(new { error = $"Unsupported provider. Use: {string.Join(", ", AiProviderCatalog.Providers.Select(item => item.Id))}" });

        if (!AiModelPolicy.IsAllowed(provider, modelId))
            return Results.BadRequest(new { error = "Modelo inválido. Selecione um modelo disponível no catálogo." });

        await using var transaction = await dbContext.Database.BeginTransactionAsync(httpContext.RequestAborted);
        try
        {
            await AiConfigurationLock.AcquireAsync(dbContext, currentTenant.TenantId.Value, httpContext.RequestAborted);

            var existing = await credentialRepository.GetByTenantAsync(currentTenant.TenantId.Value);
            if (existing is null && expectedVersion != 0)
                return Results.Conflict(new { error = "A configuração do provedor foi alterada por outro usuário." });
            if (existing is not null && existing.Version != expectedVersion)
                return Results.Conflict(new { error = "A configuração do provedor foi alterada por outro usuário." });

            var botConfig = await botConfigRepository.GetByTenantAsync(currentTenant.TenantId.Value);
            var aiIsActive = botConfig?.Enabled == true && botConfig.Mode == BotMode.AiPowered;
            if (aiIsActive && await evaluationRepository.GetApprovedForModelAsync(
                    currentTenant.TenantId.Value, provider, modelId, httpContext.RequestAborted) is null)
                return Results.BadRequest(new { error = "O modelo precisa de uma avaliação aprovada antes da ativação.", code = "model_evaluation_required" });

            var selectedCredential = await credentialRepository.GetByTenantAndProviderAsync(currentTenant.TenantId.Value, provider);
            if (string.IsNullOrWhiteSpace(request.ApiKey) && selectedCredential is null)
                return Results.BadRequest(new { error = "API key is required for a new provider configuration." });

            var secretKey = selectedCredential?.ApiKeyRef ?? $"ai:{currentTenant.TenantId}:{provider}:apikey";
            var configuredCredentialId = selectedCredential?.Id;
            if (!string.IsNullOrWhiteSpace(request.ApiKey))
            {
                secretKey = $"ai:{currentTenant.TenantId}:{provider}:apikey";
                await secretStore.SetAsync(secretKey, request.ApiKey, httpContext.RequestAborted);
            }

            if (existing is not null && existing.Id != selectedCredential?.Id)
            {
                existing.Deactivate();
                await credentialRepository.UpdateAsync(existing, httpContext.RequestAborted);
            }

            if (selectedCredential is not null)
            {
                selectedCredential.Update(modelId, secretKey);
                selectedCredential.Activate();
                await credentialRepository.UpdateAsync(selectedCredential, httpContext.RequestAborted);
            }
            else
            {
                var credential = AiProviderCredential.Create(
                    currentTenant.TenantId.Value, provider, modelId, secretKey);
                configuredCredentialId = credential.Id;
                await credentialRepository.AddAsync(credential, httpContext.RequestAborted);
            }

            await auditLogRepository.AddAsync(AuditLog.Create(
                currentTenant.TenantId.Value,
                currentTenant.UserId,
                "AI.ProviderConfigurationUpdated",
                "AiProviderCredential",
                configuredCredentialId.ToString(),
                $"provider={provider};model={modelId}"),
                httpContext.RequestAborted);

            await transaction.CommitAsync(httpContext.RequestAborted);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new { error = "A configuração do provedor foi alterada por outro usuário." });
        }

        return Results.Ok(new { saved = true });
    }

    private static async Task<IResult> UpdateInstructionsAsync(
        [FromBody] UpdateAiInstructionsRequest request,
        ICurrentTenant currentTenant,
        IAiProviderCredentialRepository credentialRepository,
        IBotConfigurationRepository botConfigRepository,
        IServiceLineRepository queueRepository,
        IClientTagRepository tagRepository,
        IAuditLogRepository auditLogRepository,
        AppDbContext dbContext,
        HttpContext httpContext)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();
        if (currentTenant.UserRole != "TenantOwner")
            return Results.Forbid();

        if (!await dbContext.HasAiEnabledAsync(currentTenant.TenantId.Value))
            return Results.BadRequest(new { error = "AI not available in your plan." });

        var credential = await credentialRepository.GetByTenantAsync(currentTenant.TenantId.Value);
        if (credential is null)
            return Results.BadRequest(new { error = "Configure um provedor de IA antes das diretrizes." });

        var ifMatch = httpContext.Request.Headers["If-Match"].FirstOrDefault();
        if (ifMatch is null || !uint.TryParse(ifMatch, out var expectedVersion))
            return Results.BadRequest(new { error = "If-Match header com a versão é obrigatório." });

        if (request.ConfidenceThreshold is double confidenceThreshold &&
            (double.IsNaN(confidenceThreshold) || confidenceThreshold is < 0 or > 1))
            return Results.BadRequest(new { error = "O limiar de confiança deve estar entre 0 e 1." });

        var requestedQueueIds = (request.RoutingQueueIds ?? []).Distinct().ToArray();
        if (requestedQueueIds.Length > 0 &&
            !await dbContext.HasAutomaticDistributionEnabledAsync(currentTenant.TenantId.Value))
            return Results.BadRequest(new { error = "A distribuição automática não está disponível neste plano." });

        var activeQueues = await queueRepository.GetActiveByTenantAsync(currentTenant.TenantId.Value);
        var activeQueueIds = activeQueues.Select(queue => queue.Id).ToHashSet();
        if (Array.Exists(requestedQueueIds, id => !activeQueueIds.Contains(id)))
            return Results.BadRequest(new { error = "Selecione somente filas ativas desta empresa." });

        var requestedTagIds = (request.RoutingTagIds ?? []).Distinct().ToArray();
        if (requestedTagIds.Length > 0 &&
            !await dbContext.HasTagsEnabledAsync(currentTenant.TenantId.Value))
            return Results.BadRequest(new { error = "As tags não estão disponíveis neste plano." });

        var activeTags = await tagRepository.GetActiveByTenantAsync(currentTenant.TenantId.Value);
        var activeTagIds = activeTags.Select(tag => tag.Id).ToHashSet();
        if (Array.Exists(requestedTagIds, id => !activeTagIds.Contains(id)))
            return Results.BadRequest(new { error = "Selecione somente tags ativas desta empresa." });

        var botConfig = await botConfigRepository.GetByTenantAsync(currentTenant.TenantId.Value);
        if (request.ConfidenceThreshold is not null && botConfig is not null)
        {
            var botIfMatch = httpContext.Request.Headers["If-Match-Bot"].FirstOrDefault();
            if (botIfMatch is null || !uint.TryParse(botIfMatch, out var expectedBotVersion))
                return Results.BadRequest(new { error = "If-Match-Bot com a versão do BOT é obrigatório ao salvar o limiar." });
            if (botConfig.Version != expectedBotVersion)
                return Results.Conflict(new { error = "A configuração do BOT foi alterada por outro usuário." });
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(httpContext.RequestAborted);
        try
        {
            credential.UpdateInstructions(
                request.SystemPrompt,
                request.MaxTokensPerResponse,
                expectedVersion,
                requestedQueueIds,
                requestedTagIds);
            await credentialRepository.UpdateAsync(credential, httpContext.RequestAborted);

            if (botConfig is null)
            {
                botConfig = BotConfiguration.Create(currentTenant.TenantId.Value);
                botConfig.UpdateConfidenceThreshold(request.ConfidenceThreshold ?? botConfig.ConfidenceThreshold);
                await botConfigRepository.AddAsync(botConfig, httpContext.RequestAborted);
            }
            else
            {
                botConfig.UpdateConfidenceThreshold(request.ConfidenceThreshold ?? botConfig.ConfidenceThreshold);
                await botConfigRepository.UpdateAsync(botConfig, httpContext.RequestAborted);
            }

            await auditLogRepository.AddAsync(AuditLog.Create(
                currentTenant.TenantId.Value,
                currentTenant.UserId,
                "AI.InstructionsUpdated",
                "AiProviderCredential",
                credential.Id.ToString(),
                $"provider={credential.Provider};model={credential.ModelId};confidence={botConfig.ConfidenceThreshold.ToString(System.Globalization.CultureInfo.InvariantCulture)}"),
                httpContext.RequestAborted);

            await transaction.CommitAsync(httpContext.RequestAborted);
        }
        catch (ConcurrencyException)
        {
            return Results.Conflict(new { error = "As diretrizes foram alteradas por outro usuário." });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new { error = "As diretrizes foram alteradas por outro usuário." });
        }

        return Results.Ok(new
        {
            saved = true,
            version = credential.Version,
            maxTokensPerResponse = credential.MaxTokensPerResponse,
            confidenceThreshold = botConfig.ConfidenceThreshold
        });
    }

    private static async Task<IResult> ToggleAsync(
        [FromBody] ToggleAiRequest request,
        ICurrentTenant currentTenant,
        IAiProviderCredentialRepository credentialRepository,
        IBotConfigurationRepository botConfigRepository,
        IModelEvaluationRepository evaluationRepository,
        IAuditLogRepository auditLogRepository,
        AppDbContext dbContext,
        HttpContext httpContext)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();
        if (currentTenant.UserRole != "TenantOwner")
            return Results.Forbid();

        var tenantId = currentTenant.TenantId.Value;
        if (!await dbContext.HasAiEnabledAsync(tenantId))
            return Results.BadRequest(new { error = "AI not available in your plan." });

        var ifMatch = httpContext.Request.Headers["If-Match-Bot"].FirstOrDefault();
        if (ifMatch is null || !uint.TryParse(ifMatch, out var expectedVersion))
            return Results.BadRequest(new { error = "If-Match-Bot com a versão do BOT é obrigatório." });

        await using var transaction = await dbContext.Database.BeginTransactionAsync(httpContext.RequestAborted);
        try
        {
            await AiConfigurationLock.AcquireAsync(dbContext, tenantId, httpContext.RequestAborted);
            var botConfig = await botConfigRepository.GetByTenantAsync(tenantId);
            if (botConfig is null)
            {
                if (expectedVersion != 0)
                    return Results.Conflict(new { error = "A configuração do BOT foi alterada por outro usuário." });
                if (!request.Enabled)
                    return Results.Ok(new { aiActive = false, botVersion = 0U });

                var credential = await credentialRepository.GetByTenantAsync(tenantId, httpContext.RequestAborted);
                if (credential is null)
                    return Results.BadRequest(new { error = "Configure um provedor de IA antes de ativar." });
                if (await evaluationRepository.GetApprovedForModelAsync(
                        tenantId, credential.Provider, credential.ModelId, httpContext.RequestAborted) is null)
                    return Results.BadRequest(new { error = "O modelo precisa de uma avaliação aprovada antes da ativação.", code = "model_evaluation_required" });

                botConfig = BotConfiguration.Create(tenantId, BotMode.AiPowered);
                await botConfigRepository.AddAsync(botConfig, httpContext.RequestAborted);
                await auditLogRepository.AddAsync(AuditLog.Create(
                    tenantId, currentTenant.UserId, "AI.ModeChanged", "BotConfiguration", botConfig.Id.ToString(),
                    "mode=AiPowered;enabled=true"), httpContext.RequestAborted);
            }
            else
            {
                if (botConfig.Version != expectedVersion)
                    return Results.Conflict(new { error = "A configuração do BOT foi alterada por outro usuário." });

                if (request.Enabled)
                {
                    var credential = await credentialRepository.GetByTenantAsync(tenantId, httpContext.RequestAborted);
                    if (credential is null)
                        return Results.BadRequest(new { error = "Configure um provedor de IA antes de ativar." });
                    if (await evaluationRepository.GetApprovedForModelAsync(
                            tenantId, credential.Provider, credential.ModelId, httpContext.RequestAborted) is null)
                        return Results.BadRequest(new { error = "O modelo precisa de uma avaliação aprovada antes da ativação.", code = "model_evaluation_required" });
                }

                botConfig.UpdateMode(request.Enabled ? BotMode.AiPowered : BotMode.Manual);
                botConfig.Toggle(request.Enabled);
                await botConfigRepository.UpdateAsync(botConfig, httpContext.RequestAborted);
                await auditLogRepository.AddAsync(AuditLog.Create(
                    tenantId, currentTenant.UserId, "AI.ModeChanged", "BotConfiguration", botConfig.Id.ToString(),
                    $"mode={botConfig.Mode};enabled={botConfig.Enabled}"), httpContext.RequestAborted);
            }

            await transaction.CommitAsync(httpContext.RequestAborted);
            return Results.Ok(new { aiActive = request.Enabled, botVersion = botConfig.Version });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new { error = "A configuração do BOT foi alterada por outro usuário." });
        }
    }

    private static async Task<IResult> TestConnectionAsync(
        ICurrentTenant currentTenant,
        IAiProviderCredentialRepository credentialRepository,
        ISecretStore secretStore,
        IAiProviderResolver aiProviderResolver,
        AppDbContext dbContext)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();
        if (currentTenant.UserRole != "TenantOwner")
            return Results.Forbid();

        if (!await dbContext.HasAiEnabledAsync(currentTenant.TenantId.Value))
            return Results.BadRequest(new { error = "AI not available in your plan." });

        var credential = await credentialRepository.GetByTenantAsync(currentTenant.TenantId.Value);
        if (credential is null || !credential.IsActive)
            return Results.BadRequest(new { error = "AI provider not configured.", step = "config" });
        if (!AiModelPolicy.IsAllowed(credential.Provider, credential.ModelId))
            return Results.BadRequest(new { error = "Configured model is not allowed.", step = "config" });

        var apiKey = await secretStore.GetAsync(credential.ApiKeyRef);
        if (string.IsNullOrEmpty(apiKey))
            return Results.BadRequest(new { error = "API key not available.", step = "secret" });

        try
        {
            var aiProvider = aiProviderResolver.Resolve(credential.Provider);

            var request = new AiRequest
            {
                ModelId = credential.ModelId,
                ApiKey = apiKey,
                Messages = [new AiMessage { Role = "user", Content = "Say 'ok' in one word." }],
                MaxTokens = 10
            };

            var response = await aiProvider.GetResponseAsync(request);

            return Results.Ok(new
            {
                success = true,
                model = credential.ModelId,
                inputTokens = response.InputTokens,
                outputTokens = response.OutputTokens
            });
        }
        catch (Exception ex)
        {
            var sanitizedError = ex.Message.Length > 100
                ? ex.Message[..100] + "..."
                : ex.Message;

            return Results.Ok(new
            {
                success = false,
                step = "api_call",
                error = sanitizedError
            });
        }
    }

    private static async Task<IResult> SimulateAsync(
        [FromBody] SimulateAiRequest request,
        ICurrentTenant currentTenant,
        IAiProviderCredentialRepository credentialRepository,
        IBotConfigurationRepository botConfigRepository,
        ISecretStore secretStore,
        IAiProviderResolver aiProviderResolver,
        ContextAssembler contextAssembler,
        IAuditLogRepository auditLogRepository,
        AppDbContext dbContext,
        HttpContext httpContext)
    {
        if (currentTenant.TenantId is null) return Results.Unauthorized();
        if (currentTenant.UserRole != "TenantOwner") return Results.Forbid();
        if (!await dbContext.HasAiEnabledAsync(currentTenant.TenantId.Value))
            return Results.BadRequest(new { error = "AI not available in your plan." });

        if (string.IsNullOrWhiteSpace(request.Message))
            return Results.BadRequest(new { error = "A mensagem de simulação é obrigatória." });
        if (request.Message.Trim().Length > 500)
            return Results.BadRequest(new { error = "A mensagem de simulação deve ter no máximo 500 caracteres." });

        var credential = await credentialRepository.GetByTenantAsync(currentTenant.TenantId.Value);
        if (credential is null || !credential.IsActive)
            return Results.BadRequest(new { error = "Configure um provedor de IA antes da simulação." });
        var apiKey = await secretStore.GetAsync(credential.ApiKeyRef);
        if (string.IsNullOrWhiteSpace(apiKey))
            return Results.BadRequest(new { error = "Credencial do provedor não disponível." });

        var botConfig = await botConfigRepository.GetByTenantAsync(currentTenant.TenantId.Value);
        var tenant = await dbContext.Tenants.FindAsync(
            [currentTenant.TenantId.Value],
            httpContext.RequestAborted);
        var welcomeMessage = ContextAssembler.ResolveWelcomeMessage(
            botConfig?.WelcomeMessage,
            credential.SystemPrompt,
            tenant?.Name);
        var simulationContext = await contextAssembler.BuildSimulationAsync(
            currentTenant.TenantId.Value,
            request.Message,
            credential.SystemPrompt,
            httpContext.RequestAborted,
            welcomeMessage,
            tenant?.Name);
        var response = await aiProviderResolver.Resolve(credential.Provider).GetResponseAsync(new AiRequest
        {
            ModelId = credential.ModelId,
            ApiKey = apiKey,
            SystemPrompt = simulationContext.SystemPrompt,
            MaxTokens = Math.Clamp(credential.MaxTokensPerResponse, 48, 120),
            Messages = simulationContext.Messages
        });
        response = BehaviorPolicy.SanitizeResponse(response, botConfig?.ConfidenceThreshold ?? 0.5);
        var decision = DefaultGreetingPolicy.Apply(
            response.Decision,
            request.Message,
            isFirstInbound: true,
            personalizedWelcome: welcomeMessage);
        response = response with
        {
            Decision = decision,
            Content = decision.Action == AiAction.Reply ? decision.Text : null
        };

        var userId = Guid.TryParse(httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId)
            ? parsedUserId
            : (Guid?)null;
        await auditLogRepository.AddAsync(AuditLog.Create(
            currentTenant.TenantId.Value,
            userId,
            "AI.Simulation",
            "AiProviderCredential",
            credential.Id.ToString(),
            $"provider={credential.Provider};model={credential.ModelId};decision={decision.Action};confidence={decision.Confidence.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            httpContext.Connection.RemoteIpAddress?.ToString()));

        return Results.Ok(new
        {
            decision = decision.Action.ToString(),
            text = decision.Action == AiAction.Reply ? response.Content : null,
            confidence = decision.Confidence,
            handoffReason = decision.Action == AiAction.Handoff ? decision.HandoffReason : null,
            fallbackReason = decision.Action == AiAction.Handoff && decision.HandoffReason == "low_confidence" ? "A confiança ficou abaixo do limiar configurado." : null
        });
    }

    private static async Task<IResult> RollbackModelAsync(
        Guid evaluationId,
        ICurrentTenant currentTenant,
        IModelEvaluationRepository evaluationRepository,
        IAiProviderCredentialRepository credentialRepository,
        IAuditLogRepository auditLogRepository,
        AppDbContext dbContext,
        HttpContext httpContext)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();
        if (currentTenant.UserRole != "TenantOwner")
            return Results.Forbid();

        var tenantId = currentTenant.TenantId.Value;
        if (!await dbContext.HasAiEnabledAsync(tenantId))
            return Results.BadRequest(new { error = "AI not available in your plan." });

        var evaluation = (await evaluationRepository.GetByTenantAsync(tenantId))
            .FirstOrDefault(item => item.Id == evaluationId);
        if (evaluation is null)
            return Results.NotFound();
        if (!evaluation.IsApproved || string.IsNullOrWhiteSpace(evaluation.RollbackModelId))
            return Results.BadRequest(new { error = "A avaliação aprovada não possui modelo de rollback configurado." });

        var credential = await credentialRepository.GetByTenantAsync(tenantId, httpContext.RequestAborted);
        if (credential is null)
            return Results.BadRequest(new { error = "Configure um provedor de IA antes do rollback." });

        if (!AiModelPolicy.IsAllowed(credential.Provider, evaluation.RollbackModelId) ||
            await evaluationRepository.GetApprovedForModelAsync(
                tenantId, credential.Provider, evaluation.RollbackModelId, httpContext.RequestAborted) is null)
            return Results.BadRequest(new { error = "O modelo de rollback não possui avaliação aprovada." });

        if (!uint.TryParse(httpContext.Request.Headers["If-Match"].FirstOrDefault(), out var expectedVersion))
            return Results.BadRequest(new { error = "If-Match header com a versão é obrigatório." });
        if (credential.Version != expectedVersion)
            return Results.Conflict(new { error = "A configuração do provedor foi alterada por outro usuário." });

        await using var transaction = await dbContext.Database.BeginTransactionAsync(httpContext.RequestAborted);
        try
        {
            credential.Update(evaluation.RollbackModelId, credential.ApiKeyRef);
            credential.Activate();
            await credentialRepository.UpdateAsync(credential, httpContext.RequestAborted);
            await auditLogRepository.AddAsync(AuditLog.Create(
                tenantId,
                currentTenant.UserId,
                "AI.ModelRolledBack",
                "AiProviderCredential",
                credential.Id.ToString(),
                $"provider={credential.Provider};model={credential.ModelId};sourceEvaluation={evaluation.Id}"),
                httpContext.RequestAborted);
            await transaction.CommitAsync(httpContext.RequestAborted);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new { error = "A configuração do provedor foi alterada por outro usuário." });
        }

        return Results.Ok(new { rolledBack = true, modelId = credential.ModelId, version = credential.Version });
    }

}

public sealed record SaveAiConfigRequest
{
    public string? Provider { get; init; }
    public string ModelId { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
}

public sealed record ToggleAiRequest(bool Enabled);
public sealed record SimulateAiRequest(string Message);
public sealed record UpdateAiInstructionsRequest(
    string? SystemPrompt,
    int MaxTokensPerResponse,
    IReadOnlyList<Guid>? RoutingQueueIds,
    IReadOnlyList<Guid>? RoutingTagIds,
    double? ConfidenceThreshold);

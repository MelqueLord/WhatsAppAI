using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Automation;
using WhatsAppAI.Application.Automation.Policy;
using WhatsAppAI.Domain.Audit;
using WhatsAppAI.Domain.Integrations;
using WhatsAppAI.Infrastructure.Identity;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.WebApi.Admin;

public static class AdminAiProviderEndpoints
{
    public static IEndpointRouteBuilder MapAdminAiProviderEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/ai/providers", GetProvidersAsync)
            .WithTags("Admin - AI Provider")
            .RequireAuthorization("PlatformAdmin")
            .WithName("GetAdminAiProviders");

        var group = app.MapGroup("/api/admin/tenants/{tenantId:guid}/ai")
            .WithTags("Admin - AI Provider")
            .RequireAuthorization("PlatformAdmin");

        group.MapGet("/", GetConfigAsync).WithName("GetAdminTenantAiConfig");
        group.MapPost("/", SaveConfigAsync).WithName("SaveAdminTenantAiConfig");
        group.MapPost("/test-connection", TestConnectionAsync).WithName("TestAdminTenantAiConnection");

        return app;
    }

    private static IResult GetProvidersAsync()
    {
        return Results.Ok(AiProviderCatalog.Providers
            .Select(definition => new { id = definition.Id, name = definition.Name, models = definition.Models }));
    }

    private static async Task<IResult> GetConfigAsync(
        Guid tenantId,
        ITenantRepository tenantRepository,
        IAiProviderCredentialRepository credentialRepository,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant is null)
            return Results.NotFound();
        if (!await dbContext.HasAiEnabledAsync(tenantId, cancellationToken))
            return Results.BadRequest(new { error = "AI not available in this plan." });

        var credential = await credentialRepository.GetByTenantAsync(tenantId, cancellationToken);
        return Results.Ok(new
        {
            configured = credential is not null,
            provider = credential?.Provider,
            modelId = credential?.ModelId,
            isActive = credential?.IsActive,
            version = credential?.Version,
            credentialScope = credential?.CredentialScope ?? AiCredentialScopes.TenantProject,
            credentialManagedByPlatform = true
        });
    }

    private static async Task<IResult> SaveConfigAsync(
        Guid tenantId,
        [FromBody] SaveAdminAiConfigRequest request,
        ICurrentTenant currentTenant,
        ITenantRepository tenantRepository,
        IAiProviderCredentialRepository credentialRepository,
        ISecretStore secretStore,
        IAuditLogRepository auditLogRepository,
        AppDbContext dbContext,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant is null)
            return Results.NotFound();
        if (!await dbContext.HasAiEnabledAsync(tenantId, cancellationToken))
            return Results.BadRequest(new { error = "AI not available in this plan." });

        var provider = AiProviderCatalog.NormalizeProvider(request.Provider);
        var modelId = AiProviderCatalog.NormalizeModelId(request.ModelId);
        if (!AiProviderCatalog.IsSupported(provider))
            return Results.BadRequest(new { error = "Unsupported provider." });
        if (!AiModelPolicy.IsAllowed(provider, modelId))
            return Results.BadRequest(new { error = "Modelo inválido. Selecione um modelo disponível no catálogo." });
        var credentialScope = AiCredentialScopes.Normalize(request.CredentialScope);
        if (!AiCredentialScopes.IsSupported(request.CredentialScope ?? AiCredentialScopes.TenantProject))
            return Results.BadRequest(new { error = "Unsupported credential scope." });
        if (string.IsNullOrWhiteSpace(request.ApiKey))
            return Results.BadRequest(new { error = "API key is required for platform provisioning." });
        if (!uint.TryParse(httpContext.Request.Headers["If-Match"].FirstOrDefault(), out var expectedVersion))
            return Results.BadRequest(new { error = "If-Match header com a versão é obrigatório." });

        var existing = await credentialRepository.GetByTenantAndProviderAsync(tenantId, provider, cancellationToken);
        if (existing is not null && existing.Version != expectedVersion)
            return Results.Conflict(new { error = "A configuração do provedor foi alterada por outro usuário." });
        if (existing is null && expectedVersion != 0)
            return Results.Conflict(new { error = "A configuração do provedor foi alterada por outro usuário." });

        var secretKey = credentialScope == AiCredentialScopes.SharedPlatform
            ? $"ai:platform:{provider}:apikey"
            : $"ai:{tenantId}:{provider}:apikey";
        await secretStore.SetAsync(secretKey, request.ApiKey, httpContext.RequestAborted);

        var active = await credentialRepository.GetByTenantAsync(tenantId, httpContext.RequestAborted);
        if (active is not null && active.Id != existing?.Id)
        {
            active.Deactivate();
            await credentialRepository.UpdateAsync(active, httpContext.RequestAborted);
        }

        Guid credentialId;
        if (existing is null)
        {
            var credential = AiProviderCredential.Create(tenantId, provider, modelId, secretKey, credentialScope);
            credentialId = credential.Id;
            await credentialRepository.AddAsync(credential, httpContext.RequestAborted);
        }
        else
        {
            existing.Update(modelId, secretKey, credentialScope);
            existing.Activate();
            credentialId = existing.Id;
            await credentialRepository.UpdateAsync(existing, httpContext.RequestAborted);
        }

        await auditLogRepository.AddAsync(AuditLog.Create(
            tenantId,
            currentTenant.UserId,
            "AI.PlatformCredentialProvisioned",
            "AiProviderCredential",
            credentialId.ToString(),
            $"provider={provider};model={modelId}"), httpContext.RequestAborted);

        return Results.Ok(new { saved = true, provider, modelId, credentialScope });
    }

    private static async Task<IResult> TestConnectionAsync(
        Guid tenantId,
        ICurrentTenant currentTenant,
        IAiProviderCredentialRepository credentialRepository,
        ISecretStore secretStore,
        IAiProviderResolver resolver,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.HasAiEnabledAsync(tenantId, cancellationToken))
            return Results.BadRequest(new { error = "AI not available in this plan." });

        var credential = await credentialRepository.GetByTenantAsync(tenantId, cancellationToken);
        if (credential is null || !credential.IsActive)
            return Results.BadRequest(new { error = "AI provider not configured." });

        var apiKey = await secretStore.GetAsync(credential.ApiKeyRef, cancellationToken);
        if (string.IsNullOrWhiteSpace(apiKey))
            return Results.BadRequest(new { error = "API key not available." });

        try
        {
            var response = await resolver.Resolve(credential.Provider).GetResponseAsync(new AiRequest
            {
                ModelId = credential.ModelId,
                ApiKey = apiKey,
                Messages = [new AiMessage { Role = "user", Content = "Say 'ok' in one word." }],
                MaxTokens = 10
            }, cancellationToken);

            await AuditConnectionAsync(currentTenant, tenantId, credential, true, dbContext, cancellationToken);
            return Results.Ok(new { success = true, model = credential.ModelId, inputTokens = response.InputTokens, outputTokens = response.OutputTokens });
        }
        catch (Exception ex)
        {
            await AuditConnectionAsync(currentTenant, tenantId, credential, false, dbContext, cancellationToken);
            return Results.Ok(new { success = false, step = "api_call", error = ex.Message.Length > 100 ? ex.Message[..100] + "..." : ex.Message });
        }
    }

    private static async Task AuditConnectionAsync(
        ICurrentTenant currentTenant,
        Guid tenantId,
        AiProviderCredential credential,
        bool success,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        dbContext.Set<AuditLog>().Add(AuditLog.Create(
            tenantId,
            currentTenant.UserId,
            "AI.PlatformConnectionTested",
            "AiProviderCredential",
            credential.Id.ToString(),
            $"provider={credential.Provider};model={credential.ModelId};success={success}"));
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public sealed record SaveAdminAiConfigRequest(string Provider, string ModelId, string ApiKey, string? CredentialScope = null);

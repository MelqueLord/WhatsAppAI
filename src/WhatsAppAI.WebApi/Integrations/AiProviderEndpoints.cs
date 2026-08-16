using Microsoft.AspNetCore.Mvc;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Automation;
using WhatsAppAI.Domain.Integrations;
using WhatsAppAI.Infrastructure.Identity;

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

        group.MapPost("/", SaveConfigAsync)
            .WithName("SaveAiConfig");

        group.MapPost("/test-connection", TestConnectionAsync)
            .WithName("TestAiConnection");

        return app;
    }

    private static async Task<IResult> GetConfigAsync(
        ICurrentTenant currentTenant,
        IAiProviderCredentialRepository credentialRepository)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();

        var credential = await credentialRepository.GetByTenantAsync(currentTenant.TenantId.Value);
        if (credential is null)
            return Results.Ok(new { configured = false });

        return Results.Ok(new
        {
            configured = true,
            provider = credential.Provider,
            modelId = credential.ModelId,
            isActive = credential.IsActive,
            version = credential.Version
        });
    }

    private static async Task<IResult> SaveConfigAsync(
        [FromBody] SaveAiConfigRequest request,
        ICurrentTenant currentTenant,
        IAiProviderCredentialRepository credentialRepository,
        ISecretStore secretStore)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.ApiKey))
            return Results.BadRequest(new { error = "API key is required." });

        if (string.IsNullOrWhiteSpace(request.ModelId))
            return Results.BadRequest(new { error = "Model ID is required." });

        var provider = request.Provider ?? "OpenAI";
        var secretKey = $"ai:{currentTenant.TenantId}:{provider}:apikey";

        await secretStore.SetAsync(secretKey, request.ApiKey);

        var existing = await credentialRepository.GetByTenantAsync(currentTenant.TenantId.Value);

        if (existing is not null)
        {
            if (existing.Provider != provider)
            {
                existing.Deactivate();
                await credentialRepository.UpdateAsync(existing);

                var newCredential = AiProviderCredential.Create(
                    currentTenant.TenantId.Value, provider, request.ModelId, secretKey);
                await credentialRepository.AddAsync(newCredential);
            }
            else
            {
                existing.Update(request.ModelId, secretKey);
                await credentialRepository.UpdateAsync(existing);
            }
        }
        else
        {
            var credential = AiProviderCredential.Create(
                currentTenant.TenantId.Value, provider, request.ModelId, secretKey);
            await credentialRepository.AddAsync(credential);
        }

        return Results.Ok(new { saved = true });
    }

    private static async Task<IResult> TestConnectionAsync(
        ICurrentTenant currentTenant,
        IAiProviderCredentialRepository credentialRepository,
        ISecretStore secretStore,
        IAiProvider aiProvider)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();

        var credential = await credentialRepository.GetByTenantAsync(currentTenant.TenantId.Value);
        if (credential is null || !credential.IsActive)
            return Results.BadRequest(new { error = "AI provider not configured.", step = "config" });

        var apiKey = await secretStore.GetAsync(credential.ApiKeyRef);
        if (string.IsNullOrEmpty(apiKey))
            return Results.BadRequest(new { error = "API key not available.", step = "secret" });

        try
        {
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
}

public sealed record SaveAiConfigRequest
{
    public string? Provider { get; init; }
    public string ModelId { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
}

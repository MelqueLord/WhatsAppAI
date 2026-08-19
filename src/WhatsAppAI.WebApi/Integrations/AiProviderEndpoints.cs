using Microsoft.AspNetCore.Mvc;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Automation;
using WhatsAppAI.Domain.Integrations;
using WhatsAppAI.Infrastructure.Identity;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.WebApi.Integrations;

public static class AiProviderEndpoints
{
    private static readonly HashSet<string> SupportedProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        "openai", "gemini", "anthropic", "xiaomi"
    };

    private static readonly Dictionary<string, object[]> ProviderModels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["openai"] =
        [
            new { id = "gpt-4o", name = "GPT-4o" },
            new { id = "gpt-4o-mini", name = "GPT-4o Mini" },
            new { id = "gpt-4.1-mini", name = "GPT-4.1 Mini" }
        ],
        ["gemini"] =
        [
            new { id = "gemini-3.1-pro-preview", name = "Gemini 3.1 Pro Preview" },
            new { id = "gemini-3.6-flash", name = "Gemini 3.6 Flash" }
        ],
        ["anthropic"] =
        [
            new { id = "claude-sonnet-4-20250514", name = "Claude Sonnet 4" },
            new { id = "claude-haiku-3-5-20241022", name = "Claude Haiku 3.5" }
        ],
        ["xiaomi"] =
        [
            new { id = "mimo-v2.5-pro", name = "MiMo v2.5 Pro" },
            new { id = "mimo-v2.5", name = "MiMo v2.5" }
        ]
    };

    public static IEndpointRouteBuilder MapAiProviderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/integrations/ai")
            .WithTags("AI Provider")
            .RequireAuthorization("RequireTenantContext");

        group.MapGet("/", GetConfigAsync)
            .WithName("GetAiConfig");

        group.MapGet("/providers", GetProvidersAsync)
            .WithName("GetAiProviders");

        group.MapPost("/", SaveConfigAsync)
            .WithName("SaveAiConfig");

        group.MapPost("/test-connection", TestConnectionAsync)
            .WithName("TestAiConnection");

        return app;
    }

    private static IResult GetProvidersAsync(IAiProviderResolver resolver)
    {
        var registered = resolver.GetRegisteredProviders();
        var providers = registered.Select(p => new
        {
            id = p.ToLowerInvariant(),
            name = p.ToLowerInvariant() switch
            {
                "openai" => "OpenAI",
                "gemini" => "Google Gemini",
                "anthropic" => "Anthropic",
                "xiaomi" => "Xiaomi MiMo",
                _ => p
            },
            models = ProviderModels.GetValueOrDefault(p) ?? []
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

        if (!await dbContext.HasAiEnabledAsync(currentTenant.TenantId.Value))
            return Results.BadRequest(new { error = "AI not available in your plan." });

        var credential = await credentialRepository.GetByTenantAsync(currentTenant.TenantId.Value);
        var botConfig = await botConfigRepository.GetByTenantAsync(currentTenant.TenantId.Value);

        return Results.Ok(new
        {
            configured = credential is not null,
            provider = credential?.Provider,
            modelId = credential?.ModelId,
            isActive = credential?.IsActive,
            version = credential?.Version,
            botConfig = botConfig is not null
                ? new
                {
                    mode = botConfig.Mode.ToString(),
                    welcomeMessage = botConfig.WelcomeMessage,
                    offlineMessage = botConfig.OfflineMessage,
                    fallbackMessage = botConfig.FallbackMessage,
                    maxTokensPerResponse = botConfig.MaxTokensPerResponse,
                    enabled = botConfig.Enabled
                }
                : new { mode = "Manual", welcomeMessage = (string?)null, offlineMessage = (string?)null, fallbackMessage = (string?)null, maxTokensPerResponse = 500, enabled = true }
        });
    }

    private static async Task<IResult> SaveConfigAsync(
        [FromBody] SaveAiConfigRequest request,
        ICurrentTenant currentTenant,
        IAiProviderCredentialRepository credentialRepository,
        IBotConfigurationRepository botConfigRepository,
        ISecretStore secretStore,
        AppDbContext dbContext)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();

        if (!await dbContext.HasAiEnabledAsync(currentTenant.TenantId.Value))
            return Results.BadRequest(new { error = "AI not available in your plan." });

        if (string.IsNullOrWhiteSpace(request.ModelId))
            return Results.BadRequest(new { error = "Model ID is required." });

        var provider = request.Provider ?? "openai";
        if (!SupportedProviders.Contains(provider))
            return Results.BadRequest(new { error = $"Unsupported provider. Use: {string.Join(", ", SupportedProviders)}" });

        if (provider.Equals("gemini", StringComparison.OrdinalIgnoreCase))
        {
            var modelId = request.ModelId.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
                ? request.ModelId["models/".Length..]
                : request.ModelId;
            var allowedGeminiModels = ProviderModels["gemini"]
                .Select(model => ((dynamic)model).id as string)
                .Where(id => id is not null)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (!allowedGeminiModels.Contains(modelId))
                return Results.BadRequest(new { error = "Modelo Gemini inválido. Selecione um modelo disponível no catálogo." });
        }

        var existing = await credentialRepository.GetByTenantAsync(currentTenant.TenantId.Value);
        if (string.IsNullOrWhiteSpace(request.ApiKey) &&
            (existing is null || !existing.Provider.Equals(provider, StringComparison.OrdinalIgnoreCase)))
            return Results.BadRequest(new { error = "API key is required for a new provider configuration." });

        var secretKey = existing?.ApiKeyRef ?? $"ai:{currentTenant.TenantId}:{provider}:apikey";
        if (!string.IsNullOrWhiteSpace(request.ApiKey))
        {
            secretKey = $"ai:{currentTenant.TenantId}:{provider}:apikey";
            await secretStore.SetAsync(secretKey, request.ApiKey);
        }

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

        // Save bot configuration if provided
        if (request.BotConfig is not null)
        {
            var botConfig = await botConfigRepository.GetByTenantAsync(currentTenant.TenantId.Value);
            var mode = Enum.TryParse<BotMode>(request.BotConfig.Mode, true, out var m) ? m : BotMode.Manual;

            if (botConfig is null)
            {
                botConfig = BotConfiguration.Create(currentTenant.TenantId.Value, mode);
                botConfig.UpdateMessages(request.BotConfig.WelcomeMessage, request.BotConfig.OfflineMessage, request.BotConfig.FallbackMessage, request.BotConfig.HandoffMessage, request.BotConfig.MediaMessage);
                botConfig.UpdateTokenLimit(request.BotConfig.MaxTokensPerResponse ?? 500);
                await botConfigRepository.AddAsync(botConfig);
            }
            else
            {
                botConfig.UpdateMode(mode);
                botConfig.UpdateMessages(request.BotConfig.WelcomeMessage, request.BotConfig.OfflineMessage, request.BotConfig.FallbackMessage, request.BotConfig.HandoffMessage, request.BotConfig.MediaMessage);
                botConfig.UpdateTokenLimit(request.BotConfig.MaxTokensPerResponse ?? botConfig.MaxTokensPerResponse);
                if (!botConfig.Enabled)
                    botConfig.Toggle(true);
                await botConfigRepository.UpdateAsync(botConfig);
            }
        }

        return Results.Ok(new { saved = true });
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

        if (!await dbContext.HasAiEnabledAsync(currentTenant.TenantId.Value))
            return Results.BadRequest(new { error = "AI not available in your plan." });

        var credential = await credentialRepository.GetByTenantAsync(currentTenant.TenantId.Value);
        if (credential is null || !credential.IsActive)
            return Results.BadRequest(new { error = "AI provider not configured.", step = "config" });

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
}

public sealed record SaveAiConfigRequest
{
    public string? Provider { get; init; }
    public string ModelId { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public SaveBotConfigRequest? BotConfig { get; init; }
}

public sealed record SaveBotConfigRequest
{
    public string? Mode { get; init; }
    public string? WelcomeMessage { get; init; }
    public string? OfflineMessage { get; init; }
    public string? FallbackMessage { get; init; }
    public string? HandoffMessage { get; init; }
    public string? MediaMessage { get; init; }
    public int? MaxTokensPerResponse { get; init; }
}

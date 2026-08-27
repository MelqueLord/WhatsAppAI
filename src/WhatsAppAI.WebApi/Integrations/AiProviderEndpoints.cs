using Microsoft.AspNetCore.Mvc;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Automation;
using WhatsAppAI.Application.Automation.Policy;
using WhatsAppAI.Domain.Integrations;
using WhatsAppAI.Infrastructure.Identity;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.WebApi.Integrations;

public static class AiProviderEndpoints
{
    private static readonly HashSet<string> SupportedProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        "openai", "gemini", "anthropic", "xiaomi", "grok", "groq"
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
        ],
        ["grok"] =
        [
            new { id = "grok-4.6", name = "Grok 4.6" },
            new { id = "grok-4.5", name = "Grok 4.5" },
            new { id = "grok-4.3", name = "Grok 4.3" }
        ],
        ["groq"] =
        [
            new { id = "openai/gpt-oss-120b", name = "GPT-OSS 120B" },
            new { id = "openai/gpt-oss-20b", name = "GPT-OSS 20B" },
            new { id = "qwen/qwen3.6-27b", name = "Qwen 3.6 27B" }
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

        group.MapPost("/toggle", ToggleAsync)
            .WithName("ToggleAi");

        group.MapPut("/instructions", UpdateInstructionsAsync)
            .WithName("UpdateAiInstructions");

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
                "grok" => "xAI Grok",
                "groq" => "Groq",
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
            systemPrompt = credential?.SystemPrompt,
            routingQueueIds = credential?.GetRoutingQueueIds() ?? [],
            routingTagIds = credential?.GetRoutingTagIds() ?? [],
            maxTokensPerResponse = credential?.MaxTokensPerResponse ?? 500,
            isActive = credential?.IsActive,
            version = credential?.Version,
            aiActive = botConfig?.Enabled == true && botConfig.Mode == BotMode.AiPowered
        });
    }

    private static async Task<IResult> SaveConfigAsync(
        [FromBody] SaveAiConfigRequest request,
        ICurrentTenant currentTenant,
        IAiProviderCredentialRepository credentialRepository,
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

        if (!AiModelPolicy.IsAllowed(provider, request.ModelId))
            return Results.BadRequest(new { error = "Modelo inválido. Selecione um modelo disponível no catálogo." });

        var existing = await credentialRepository.GetByTenantAsync(currentTenant.TenantId.Value);
        var selectedCredential = await credentialRepository.GetByTenantAndProviderAsync(currentTenant.TenantId.Value, provider);
        if (string.IsNullOrWhiteSpace(request.ApiKey) &&
            selectedCredential is null)
            return Results.BadRequest(new { error = "API key is required for a new provider configuration." });

        var secretKey = selectedCredential?.ApiKeyRef ?? $"ai:{currentTenant.TenantId}:{provider}:apikey";
        if (!string.IsNullOrWhiteSpace(request.ApiKey))
        {
            secretKey = $"ai:{currentTenant.TenantId}:{provider}:apikey";
            await secretStore.SetAsync(secretKey, request.ApiKey);
        }

        if (existing is not null && existing.Id != selectedCredential?.Id)
        {
            existing.Deactivate();
            await credentialRepository.UpdateAsync(existing);
        }

        if (selectedCredential is not null)
        {
            selectedCredential.Update(request.ModelId, secretKey);
            selectedCredential.Activate();
            await credentialRepository.UpdateAsync(selectedCredential);
        }
        else
        {
            var credential = AiProviderCredential.Create(
                currentTenant.TenantId.Value, provider, request.ModelId, secretKey);
            await credentialRepository.AddAsync(credential);
        }

        return Results.Ok(new { saved = true });
    }

    private static async Task<IResult> UpdateInstructionsAsync(
        [FromBody] UpdateAiInstructionsRequest request,
        ICurrentTenant currentTenant,
        IAiProviderCredentialRepository credentialRepository,
        IServiceLineRepository queueRepository,
        IClientTagRepository tagRepository)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();

        var credential = await credentialRepository.GetByTenantAsync(currentTenant.TenantId.Value);
        if (credential is null)
            return Results.BadRequest(new { error = "Configure um provedor de IA antes das diretrizes." });

        var requestedQueueIds = (request.RoutingQueueIds ?? []).Distinct().ToArray();
        var activeQueues = await queueRepository.GetActiveByTenantAsync(currentTenant.TenantId.Value);
        var activeQueueIds = activeQueues.Select(queue => queue.Id).ToHashSet();
        if (Array.Exists(requestedQueueIds, id => !activeQueueIds.Contains(id)))
            return Results.BadRequest(new { error = "Selecione somente filas ativas desta empresa." });

        var requestedTagIds = (request.RoutingTagIds ?? []).Distinct().ToArray();
        var activeTags = await tagRepository.GetActiveByTenantAsync(currentTenant.TenantId.Value);
        var activeTagIds = activeTags.Select(tag => tag.Id).ToHashSet();
        if (Array.Exists(requestedTagIds, id => !activeTagIds.Contains(id)))
            return Results.BadRequest(new { error = "Selecione somente tags ativas desta empresa." });

        credential.UpdateInstructions(
            request.SystemPrompt,
            request.MaxTokensPerResponse,
            requestedQueueIds,
            requestedTagIds);
        await credentialRepository.UpdateAsync(credential);
        return Results.Ok(new { saved = true, maxTokensPerResponse = credential.MaxTokensPerResponse });
    }

    private static async Task<IResult> ToggleAsync(
        [FromBody] ToggleAiRequest request,
        ICurrentTenant currentTenant,
        IAiProviderCredentialRepository credentialRepository,
        IBotConfigurationRepository botConfigRepository,
        AppDbContext dbContext)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();

        var tenantId = currentTenant.TenantId.Value;
        if (!await dbContext.HasAiEnabledAsync(tenantId))
            return Results.BadRequest(new { error = "AI not available in your plan." });

        if (request.Enabled && await credentialRepository.GetByTenantAsync(tenantId) is null)
            return Results.BadRequest(new { error = "Configure um provedor de IA antes de ativar." });

        var botConfig = await botConfigRepository.GetByTenantAsync(tenantId);
        if (botConfig is null)
        {
            if (!request.Enabled)
                return Results.Ok(new { aiActive = false });

            await botConfigRepository.AddAsync(BotConfiguration.Create(tenantId, BotMode.AiPowered));
        }
        else
        {
            botConfig.UpdateMode(request.Enabled ? BotMode.AiPowered : BotMode.Manual);
            botConfig.Toggle(request.Enabled);
            await botConfigRepository.UpdateAsync(botConfig);
        }

        return Results.Ok(new { aiActive = request.Enabled });
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

}

public sealed record SaveAiConfigRequest
{
    public string? Provider { get; init; }
    public string ModelId { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
}

public sealed record ToggleAiRequest(bool Enabled);
public sealed record UpdateAiInstructionsRequest(
    string? SystemPrompt,
    int MaxTokensPerResponse,
    IReadOnlyList<Guid>? RoutingQueueIds,
    IReadOnlyList<Guid>? RoutingTagIds);

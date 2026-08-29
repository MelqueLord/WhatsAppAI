using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Integrations;
using WhatsAppAI.Infrastructure.Identity;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.WebApi.Bot;

public static class BotConfigurationEndpoints
{
    public static IEndpointRouteBuilder MapBotConfigurationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/bot-config")
            .WithTags("Bot Configuration")
            .RequireAuthorization("RequireTenantContext");

        group.MapGet("/", GetAsync);
        group.MapPost("/", SaveAsync);
        group.MapPut("/mode", UpdateModeAsync);
        group.MapPut("/messages", UpdateMessagesAsync);
        group.MapPost("/toggle", ToggleAsync);

        return app;
    }

    private static async Task<IResult> GetAsync(
        ICurrentTenant currentTenant, IBotConfigurationRepository repo)
    {
        if (currentTenant.TenantId is null) return Results.Unauthorized();

        var config = await repo.GetByTenantAsync(currentTenant.TenantId.Value);
        if (config is null)
            return Results.Ok(new { configured = false, mode = "Manual" });

        return Results.Ok(new
        {
            configured = true,
            mode = config.Mode.ToString(),
            welcomeMessage = config.WelcomeMessage,
            returningMessage = config.ReturningMessage,
            flowSteps = ParseFlowSteps(config.FlowStepsJson),
            offlineMessage = config.OfflineMessage,
            fallbackMessage = config.FallbackMessage,
            handoffMessage = config.HandoffMessage,
            queueTransferMessage = config.QueueTransferMessage,
            mediaMessage = config.MediaMessage,
            enabled = config.Enabled,
            version = config.Version
        });
    }

    private static async Task<IResult> SaveAsync(
        [FromBody] SaveBotConfigRequest request,
        ICurrentTenant currentTenant, IBotConfigurationRepository repo,
        AppDbContext dbContext, HttpContext httpContext)
    {
        if (currentTenant.TenantId is null) return Results.Unauthorized();
        if (currentTenant.UserRole != "TenantOwner") return Results.Forbid();

        var config = await repo.GetByTenantAsync(currentTenant.TenantId.Value);
        if (!uint.TryParse(httpContext.Request.Headers["If-Match"].FirstOrDefault(), out var expectedVersion))
            return Results.BadRequest(new { error = "If-Match header com a versão é obrigatório." });
        if (!Enum.TryParse<BotMode>(request.Mode, true, out var mode))
            return Results.BadRequest(new { error = "Invalid mode. Use: Manual, SimpleAutoReply or AiPowered" });
        if (mode == BotMode.SimpleAutoReply &&
            !await dbContext.HasBotEnabledAsync(currentTenant.TenantId.Value))
            return Results.BadRequest(new { error = "BOT automation is not available in your plan." });
        if (!TryValidateFlowSteps(request.FlowSteps, out var flowError))
            return Results.BadRequest(new { error = flowError });

        if (config is null)
        {
            config = BotConfiguration.Create(currentTenant.TenantId.Value, mode);
            config.UpdateMessages(request.WelcomeMessage, request.ReturningMessage, request.OfflineMessage, request.FallbackMessage, request.HandoffMessage, request.QueueTransferMessage, request.MediaMessage);
            config.UpdateFlowSteps(SerializeFlowSteps(request.FlowSteps));
            await repo.AddAsync(config);
        }
        else
        {
            if (config.Version != expectedVersion)
                return Results.Conflict(new { error = "A configuração do BOT foi alterada por outro usuário." });
            config.UpdateMessages(request.WelcomeMessage, request.ReturningMessage, request.OfflineMessage, request.FallbackMessage, request.HandoffMessage, request.QueueTransferMessage, request.MediaMessage);
            config.UpdateFlowSteps(SerializeFlowSteps(request.FlowSteps));
            await repo.UpdateAsync(config);
        }

        return Results.Ok(new { saved = true });
    }

    private static async Task<IResult> UpdateModeAsync(
        [FromBody] UpdateModeRequest request,
        ICurrentTenant currentTenant, IBotConfigurationRepository repo,
        AppDbContext dbContext, HttpContext httpContext)
    {
        if (currentTenant.TenantId is null) return Results.Unauthorized();
        if (currentTenant.UserRole != "TenantOwner") return Results.Forbid();

        var config = await repo.GetByTenantAsync(currentTenant.TenantId.Value);
        if (config is null) return Results.BadRequest(new { error = "Bot not configured." });
        if (!uint.TryParse(httpContext.Request.Headers["If-Match"].FirstOrDefault(), out var expectedVersion))
            return Results.BadRequest(new { error = "If-Match header com a versão é obrigatório." });
        if (config.Version != expectedVersion)
            return Results.Conflict(new { error = "A configuração do BOT foi alterada por outro usuário." });

        if (!Enum.TryParse<BotMode>(request.Mode, true, out var mode))
            return Results.BadRequest(new { error = "Invalid mode. Use: Manual, SimpleAutoReply, AiPowered" });

        if (mode == BotMode.AiPowered && !await dbContext.HasAiEnabledAsync(currentTenant.TenantId.Value))
            return Results.BadRequest(new { error = "AiPowered mode requires IA+BOT plan." });
        if (mode == BotMode.SimpleAutoReply &&
            !await dbContext.HasBotEnabledAsync(currentTenant.TenantId.Value))
            return Results.BadRequest(new { error = "BOT automation is not available in your plan." });

        config.UpdateMode(mode);
        await repo.UpdateAsync(config);
        return Results.Ok(new { mode = mode.ToString(), version = config.Version });
    }

    private static async Task<IResult> UpdateMessagesAsync(
        [FromBody] UpdateMessagesRequest request,
        ICurrentTenant currentTenant, IBotConfigurationRepository repo, HttpContext httpContext)
    {
        if (currentTenant.TenantId is null) return Results.Unauthorized();
        if (currentTenant.UserRole != "TenantOwner") return Results.Forbid();

        var config = await repo.GetByTenantAsync(currentTenant.TenantId.Value);
        if (config is null) return Results.BadRequest(new { error = "Bot not configured." });
        if (!uint.TryParse(httpContext.Request.Headers["If-Match"].FirstOrDefault(), out var expectedVersion))
            return Results.BadRequest(new { error = "If-Match header com a versão é obrigatório." });
        if (config.Version != expectedVersion)
            return Results.Conflict(new { error = "A configuração do BOT foi alterada por outro usuário." });

        config.UpdateMessages(request.WelcomeMessage, request.ReturningMessage, request.OfflineMessage, request.FallbackMessage, request.HandoffMessage, request.QueueTransferMessage, request.MediaMessage);
        await repo.UpdateAsync(config);
        return Results.Ok(new { saved = true, version = config.Version });
    }


    private static async Task<IResult> ToggleAsync(
        [FromBody] ToggleBotRequest request,
        ICurrentTenant currentTenant, IBotConfigurationRepository repo,
        AppDbContext dbContext, HttpContext httpContext)
    {
        if (currentTenant.TenantId is null) return Results.Unauthorized();
        if (currentTenant.UserRole != "TenantOwner") return Results.Forbid();

        var config = await repo.GetByTenantAsync(currentTenant.TenantId.Value);
        if (config is null) return Results.BadRequest(new { error = "Bot not configured." });
        if (!uint.TryParse(httpContext.Request.Headers["If-Match"].FirstOrDefault(), out var expectedVersion))
            return Results.BadRequest(new { error = "If-Match header com a versão é obrigatório." });
        if (config.Version != expectedVersion)
            return Results.Conflict(new { error = "A configuração do BOT foi alterada por outro usuário." });

        if (request.Enabled && !string.IsNullOrWhiteSpace(request.Mode))
        {
            if (!Enum.TryParse<BotMode>(request.Mode, true, out var mode))
                return Results.BadRequest(new { error = "Invalid mode. Use: SimpleAutoReply or AiPowered." });

            if (mode == BotMode.AiPowered && !await dbContext.HasAiEnabledAsync(currentTenant.TenantId.Value))
                return Results.BadRequest(new { error = "AiPowered mode requires IA+BOT plan." });
            if (mode == BotMode.SimpleAutoReply &&
                !await dbContext.HasBotEnabledAsync(currentTenant.TenantId.Value))
                return Results.BadRequest(new { error = "BOT automation is not available in your plan." });

            config.UpdateMode(mode);
        }

        if (request.Enabled && config.Mode == BotMode.SimpleAutoReply &&
            !await dbContext.HasBotEnabledAsync(currentTenant.TenantId.Value))
            return Results.BadRequest(new { error = "BOT automation is not available in your plan." });

        config.Toggle(request.Enabled);
        await repo.UpdateAsync(config);
        return Results.Ok(new { enabled = config.Enabled, mode = config.Mode.ToString(), version = config.Version });
    }

    private static JsonElement[] ParseFlowSteps(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        try { return JsonSerializer.Deserialize<JsonElement[]>(value) ?? []; }
        catch (JsonException) { return []; }
    }

    private static string? SerializeFlowSteps(JsonElement? value) =>
        value is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined }
            ? value.Value.GetRawText()
            : null;

    private static bool TryValidateFlowSteps(JsonElement? value, out string error)
    {
        error = string.Empty;
        if (value is null || value.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return true;
        if (value.Value.ValueKind != JsonValueKind.Array || value.Value.GetArrayLength() > 100)
        {
            error = "FlowSteps deve ser uma lista com no máximo 100 opções.";
            return false;
        }
        foreach (var step in value.Value.EnumerateArray())
        {
            if (step.ValueKind != JsonValueKind.Object ||
                !step.TryGetProperty("title", out var title) || title.ValueKind != JsonValueKind.String || title.GetString()!.Trim().Length is 0 or > 200 ||
                !step.TryGetProperty("keywords", out var keywords) || keywords.ValueKind != JsonValueKind.String || keywords.GetString()!.Length > 500 ||
                !step.TryGetProperty("response", out var response) || response.ValueKind != JsonValueKind.String || response.GetString()!.Trim().Length is 0 or > 4000)
            {
                error = "Cada opção deve ter título, palavras-chave e resposta válidos.";
                return false;
            }
        }
        return true;
    }
}

public sealed record SaveBotConfigRequest(
    string Mode,
    string? WelcomeMessage,
    string? ReturningMessage,
    string? OfflineMessage,
    string? FallbackMessage,
    string? HandoffMessage,
    string? QueueTransferMessage,
    string? MediaMessage,
    JsonElement? FlowSteps);

public sealed record UpdateModeRequest(string Mode);
public sealed record UpdateMessagesRequest(string? WelcomeMessage, string? ReturningMessage, string? OfflineMessage, string? FallbackMessage, string? HandoffMessage, string? QueueTransferMessage, string? MediaMessage);
public sealed record ToggleBotRequest(bool Enabled, string? Mode = null);

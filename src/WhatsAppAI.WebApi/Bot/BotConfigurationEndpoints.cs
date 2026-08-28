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
        ICurrentTenant currentTenant, IBotConfigurationRepository repo)
    {
        if (currentTenant.TenantId is null) return Results.Unauthorized();

        var config = await repo.GetByTenantAsync(currentTenant.TenantId.Value);
        var mode = Enum.TryParse<BotMode>(request.Mode, true, out var m) ? m : BotMode.Manual;

        if (config is null)
        {
            config = BotConfiguration.Create(currentTenant.TenantId.Value, mode);
            config.UpdateMessages(request.WelcomeMessage, request.ReturningMessage, request.OfflineMessage, request.FallbackMessage, request.HandoffMessage, request.QueueTransferMessage, request.MediaMessage);
            config.UpdateFlowSteps(SerializeFlowSteps(request.FlowSteps));
            await repo.AddAsync(config);
        }
        else
        {
            config.UpdateMode(mode);
            config.UpdateMessages(request.WelcomeMessage, request.ReturningMessage, request.OfflineMessage, request.FallbackMessage, request.HandoffMessage, request.QueueTransferMessage, request.MediaMessage);
            config.UpdateFlowSteps(SerializeFlowSteps(request.FlowSteps));
            if (!config.Enabled)
                config.Toggle(true);
            await repo.UpdateAsync(config);
        }

        return Results.Ok(new { saved = true });
    }

    private static async Task<IResult> UpdateModeAsync(
        [FromBody] UpdateModeRequest request,
        ICurrentTenant currentTenant, IBotConfigurationRepository repo,
        AppDbContext dbContext)
    {
        if (currentTenant.TenantId is null) return Results.Unauthorized();

        var config = await repo.GetByTenantAsync(currentTenant.TenantId.Value);
        if (config is null) return Results.BadRequest(new { error = "Bot not configured." });

        if (!Enum.TryParse<BotMode>(request.Mode, true, out var mode))
            return Results.BadRequest(new { error = "Invalid mode. Use: Manual, SimpleAutoReply, AiPowered" });

        if (mode == BotMode.AiPowered && !await dbContext.HasAiEnabledAsync(currentTenant.TenantId.Value))
            return Results.BadRequest(new { error = "AiPowered mode requires IA+BOT plan." });

        config.UpdateMode(mode);
        await repo.UpdateAsync(config);
        return Results.Ok(new { mode = mode.ToString() });
    }

    private static async Task<IResult> UpdateMessagesAsync(
        [FromBody] UpdateMessagesRequest request,
        ICurrentTenant currentTenant, IBotConfigurationRepository repo)
    {
        if (currentTenant.TenantId is null) return Results.Unauthorized();

        var config = await repo.GetByTenantAsync(currentTenant.TenantId.Value);
        if (config is null) return Results.BadRequest(new { error = "Bot not configured." });

        config.UpdateMessages(request.WelcomeMessage, request.ReturningMessage, request.OfflineMessage, request.FallbackMessage, request.HandoffMessage, request.QueueTransferMessage, request.MediaMessage);
        await repo.UpdateAsync(config);
        return Results.Ok(new { saved = true });
    }


    private static async Task<IResult> ToggleAsync(
        [FromBody] ToggleBotRequest request,
        ICurrentTenant currentTenant, IBotConfigurationRepository repo,
        AppDbContext dbContext)
    {
        if (currentTenant.TenantId is null) return Results.Unauthorized();

        var config = await repo.GetByTenantAsync(currentTenant.TenantId.Value);
        if (config is null) return Results.BadRequest(new { error = "Bot not configured." });

        if (request.Enabled && !string.IsNullOrWhiteSpace(request.Mode))
        {
            if (!Enum.TryParse<BotMode>(request.Mode, true, out var mode))
                return Results.BadRequest(new { error = "Invalid mode. Use: SimpleAutoReply or AiPowered." });

            if (mode == BotMode.AiPowered && !await dbContext.HasAiEnabledAsync(currentTenant.TenantId.Value))
                return Results.BadRequest(new { error = "AiPowered mode requires IA+BOT plan." });

            config.UpdateMode(mode);
        }

        config.Toggle(request.Enabled);
        await repo.UpdateAsync(config);
        return Results.Ok(new { enabled = config.Enabled, mode = config.Mode.ToString() });
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

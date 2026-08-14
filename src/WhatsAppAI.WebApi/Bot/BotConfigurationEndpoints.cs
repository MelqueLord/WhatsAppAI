using Microsoft.AspNetCore.Mvc;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Integrations;
using WhatsAppAI.Infrastructure.Identity;

namespace WhatsAppAI.WebApi.Bot;

public static class BotConfigurationEndpoints
{
    public static IEndpointRouteBuilder MapBotConfigurationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/bot-config")
            .WithTags("Bot Configuration")
            .RequireAuthorization();

        group.MapGet("/", GetAsync);
        group.MapPost("/", SaveAsync);
        group.MapPut("/mode", UpdateModeAsync);
        group.MapPut("/messages", UpdateMessagesAsync);
        group.MapPut("/tokens", UpdateTokenLimitAsync);
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
            offlineMessage = config.OfflineMessage,
            fallbackMessage = config.FallbackMessage,
            maxTokensPerResponse = config.MaxTokensPerResponse,
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
            config.UpdateMessages(request.WelcomeMessage, request.OfflineMessage, request.FallbackMessage);
            config.UpdateTokenLimit(request.MaxTokensPerResponse ?? 500);
            await repo.AddAsync(config);
        }
        else
        {
            config.UpdateMode(mode);
            config.UpdateMessages(request.WelcomeMessage, request.OfflineMessage, request.FallbackMessage);
            config.UpdateTokenLimit(request.MaxTokensPerResponse ?? config.MaxTokensPerResponse);
            await repo.UpdateAsync(config);
        }

        return Results.Ok(new { saved = true });
    }

    private static async Task<IResult> UpdateModeAsync(
        [FromBody] UpdateModeRequest request,
        ICurrentTenant currentTenant, IBotConfigurationRepository repo)
    {
        if (currentTenant.TenantId is null) return Results.Unauthorized();

        var config = await repo.GetByTenantAsync(currentTenant.TenantId.Value);
        if (config is null) return Results.BadRequest(new { error = "Bot not configured." });

        if (!Enum.TryParse<BotMode>(request.Mode, true, out var mode))
            return Results.BadRequest(new { error = "Invalid mode. Use: Manual, SimpleAutoReply, AiPowered" });

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

        config.UpdateMessages(request.WelcomeMessage, request.OfflineMessage, request.FallbackMessage);
        await repo.UpdateAsync(config);
        return Results.Ok(new { saved = true });
    }

    private static async Task<IResult> UpdateTokenLimitAsync(
        [FromBody] UpdateTokenLimitRequest request,
        ICurrentTenant currentTenant, IBotConfigurationRepository repo)
    {
        if (currentTenant.TenantId is null) return Results.Unauthorized();

        var config = await repo.GetByTenantAsync(currentTenant.TenantId.Value);
        if (config is null) return Results.BadRequest(new { error = "Bot not configured." });

        config.UpdateTokenLimit(request.MaxTokens);
        await repo.UpdateAsync(config);
        return Results.Ok(new { maxTokensPerResponse = config.MaxTokensPerResponse });
    }

    private static async Task<IResult> ToggleAsync(
        [FromBody] ToggleBotRequest request,
        ICurrentTenant currentTenant, IBotConfigurationRepository repo)
    {
        if (currentTenant.TenantId is null) return Results.Unauthorized();

        var config = await repo.GetByTenantAsync(currentTenant.TenantId.Value);
        if (config is null) return Results.BadRequest(new { error = "Bot not configured." });

        config.Toggle(request.Enabled);
        await repo.UpdateAsync(config);
        return Results.Ok(new { enabled = config.Enabled });
    }
}

public sealed record SaveBotConfigRequest(
    string Mode,
    string? WelcomeMessage,
    string? OfflineMessage,
    string? FallbackMessage,
    int? MaxTokensPerResponse);

public sealed record UpdateModeRequest(string Mode);
public sealed record UpdateMessagesRequest(string? WelcomeMessage, string? OfflineMessage, string? FallbackMessage);
public sealed record UpdateTokenLimitRequest(int MaxTokens);
public sealed record ToggleBotRequest(bool Enabled);

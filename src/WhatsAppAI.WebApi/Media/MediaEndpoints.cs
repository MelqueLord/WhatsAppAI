using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Integrations;
using WhatsAppAI.Domain.Integrations;
using WhatsAppAI.Infrastructure.Identity;

namespace WhatsAppAI.WebApi.Media;

public static class MediaEndpoints
{
    public static IEndpointRouteBuilder MapMediaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/media")
            .WithTags("Media")
            .RequireAuthorization("RequireTenantContext");

        group.MapGet("/{messageId:guid}/download", DownloadMediaAsync)
            .WithName("DownloadMedia");

        return app;
    }

    private static async Task<IResult> DownloadMediaAsync(
        Guid messageId,
        ICurrentTenant currentTenant,
        IMessageRepository messageRepository,
        IWhatsAppAccountRepository accountRepository,
        ISecretStore secretStore,
        IMediaGateway mediaGateway)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();

        var message = await messageRepository.GetByIdAsync(messageId);
        if (message is null || message.TenantId != currentTenant.TenantId)
            return Results.NotFound();

        if (string.IsNullOrEmpty(message.MediaId))
            return Results.BadRequest(new { error = "No media attached to this message." });

        var account = await accountRepository.GetByTenantAsync(currentTenant.TenantId.Value);
        if (account is null)
            return Results.BadRequest(new { error = "WhatsApp not configured." });

        var accessToken = await secretStore.GetAsync(account.AccessTokenRef);
        if (accessToken is null)
            return Results.BadRequest(new { error = "Access token not found." });

        var result = await mediaGateway.DownloadAsync(message.MediaId, accessToken);

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.ErrorMessage });

        return Results.File(result.Content.ToArray(), result.ContentType!);
    }
}

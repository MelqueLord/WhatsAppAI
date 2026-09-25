using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Integrations;
using WhatsAppAI.Domain.Integrations;
using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Infrastructure.Persistence;
using WhatsAppAI.Infrastructure.Secrets;
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
        IMediaGateway mediaGateway,
        AppDbContext dbContext,
        IEncryptionService encryptionService)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();

        var message = await messageRepository.GetByIdAsync(messageId);
        if (message is null || message.TenantId != currentTenant.TenantId)
            return Results.NotFound();

        if (string.IsNullOrEmpty(message.MediaId))
            return Results.BadRequest(new { error = "No media attached to this message." });

        var conversation = await dbContext.Conversations
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.Id == message.ConversationId && item.TenantId == currentTenant.TenantId);
        if (conversation?.PhoneNumberId.StartsWith("qr:", StringComparison.OrdinalIgnoreCase) == true)
        {
            var attachment = await dbContext.InboundMediaAttachments
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(item => item.TenantId == currentTenant.TenantId && item.ExternalMessageId == message.ExternalId);
            if (attachment is null)
                return Results.NotFound();

            try
            {
                return Results.File(Convert.FromBase64String(encryptionService.Decrypt(attachment.EncryptedContent)), attachment.ContentType);
            }
            catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or FormatException)
            {
                return Results.BadRequest(new { error = "Media is no longer available." });
            }
        }

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

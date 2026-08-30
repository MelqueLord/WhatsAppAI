using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Integrations;
using WhatsAppAI.Domain.Messaging;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.WebApi.Webhooks;

public static class WebhookEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static IEndpointRouteBuilder MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/webhooks/meta")
            .WithTags("Webhooks - Meta");

        group.MapGet("/", VerifyChallengeAsync)
            .WithName("VerifyWebhookChallenge")
            .AllowAnonymous();

        group.MapPost("/", ReceiveEventAsync)
            .WithName("ReceiveWebhookEvent")
            .AllowAnonymous()
            .RequireRateLimiting("webhook")
            .DisableAntiforgery();

        app.MapPost("/api/webhooks/whatsapp-web", ReceiveWhatsAppWebEventAsync)
            .WithTags("Webhooks - WhatsApp Web")
            .AllowAnonymous()
            .RequireRateLimiting("webhook")
            .DisableAntiforgery();

        app.MapGet("/api/webhooks/whatsapp-web/session/{sessionId}", GetWhatsAppWebSessionAsync)
            .WithTags("Webhooks - WhatsApp Web")
            .AllowAnonymous();

        app.MapPut("/api/webhooks/whatsapp-web/session/{sessionId}", SaveWhatsAppWebSessionAsync)
            .WithTags("Webhooks - WhatsApp Web")
            .AllowAnonymous()
            .RequireRateLimiting("webhook")
            .DisableAntiforgery();

        app.MapDelete("/api/webhooks/whatsapp-web/session/{sessionId}", DeleteWhatsAppWebSessionAsync)
            .WithTags("Webhooks - WhatsApp Web")
            .AllowAnonymous()
            .RequireRateLimiting("webhook")
            .DisableAntiforgery();

        app.MapPut("/api/webhooks/whatsapp-web/session/{sessionId}/lease", AcquireWhatsAppWebSessionLeaseAsync)
            .WithTags("Webhooks - WhatsApp Web")
            .AllowAnonymous()
            .RequireRateLimiting("webhook")
            .DisableAntiforgery();

        app.MapDelete("/api/webhooks/whatsapp-web/session/{sessionId}/lease", ReleaseWhatsAppWebSessionLeaseAsync)
            .WithTags("Webhooks - WhatsApp Web")
            .AllowAnonymous()
            .RequireRateLimiting("webhook")
            .DisableAntiforgery();

        return app;
    }

    private static async Task<IResult> GetWhatsAppWebSessionAsync(
        string sessionId,
        HttpContext httpContext,
        IConfiguration configuration,
        ISecretStore secretStore,
        AppDbContext dbContext)
    {
        if (!IsAuthorizedWhatsAppWebRequest(httpContext, configuration))
            return Results.Unauthorized();
        if (!IsValidSessionId(sessionId) || !await HasCurrentLeaseOwnershipAsync(sessionId, httpContext, dbContext))
            return Results.BadRequest();

        var payload = await secretStore.GetAsync($"whatsapp-web:auth:{sessionId}");
        return payload is null ? Results.NotFound() : Results.Ok(new { payload });
    }

    private static async Task<IResult> SaveWhatsAppWebSessionAsync(
        string sessionId,
        [FromBody] WhatsAppWebSessionRequest request,
        HttpContext httpContext,
        IConfiguration configuration,
        ISecretStore secretStore,
        AppDbContext dbContext)
    {
        if (!IsAuthorizedWhatsAppWebRequest(httpContext, configuration))
            return Results.Unauthorized();
        if (!IsValidSessionId(sessionId) || string.IsNullOrWhiteSpace(request.Payload) || request.Payload.Length > 1_000_000 ||
            !await HasCurrentLeaseOwnershipAsync(sessionId, httpContext, dbContext))
            return Results.BadRequest();

        await secretStore.SetAsync($"whatsapp-web:auth:{sessionId}", request.Payload);
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteWhatsAppWebSessionAsync(
        string sessionId,
        HttpContext httpContext,
        IConfiguration configuration,
        ISecretStore secretStore,
        AppDbContext dbContext)
    {
        if (!IsAuthorizedWhatsAppWebRequest(httpContext, configuration))
            return Results.Unauthorized();
        if (!IsValidSessionId(sessionId) || !await HasCurrentLeaseOwnershipAsync(sessionId, httpContext, dbContext))
            return Results.BadRequest();

        await secretStore.DeleteAsync($"whatsapp-web:auth:{sessionId}");
        return Results.NoContent();
    }

    private static async Task<IResult> AcquireWhatsAppWebSessionLeaseAsync(
        string sessionId,
        [FromBody] WhatsAppWebSessionLeaseRequest request,
        HttpContext httpContext,
        IConfiguration configuration,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorizedWhatsAppWebRequest(httpContext, configuration))
            return Results.Unauthorized();
        if (!TryParseSessionId(sessionId, out var tenantId, out var lineNumber) ||
            !TryNormalizeInstanceUrl(request.InstanceUrl, out var instanceUrl) ||
            string.IsNullOrWhiteSpace(request.InstanceId) || request.InstanceId.Length > 160)
            return Results.BadRequest();

        var now = DateTime.UtcNow;
        var expiresAt = now.AddSeconds(45);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (dbContext.Database.IsNpgsql())
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtext({sessionId}))",
                cancellationToken);
        }

        var lease = await dbContext.WhatsAppWebSessionLeases
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.SessionId == sessionId, cancellationToken);

        if (lease is not null && !lease.IsExpired(now) && !lease.IsOwnedBy(request.InstanceId))
        {
            await transaction.CommitAsync(cancellationToken);
            return Results.Conflict(new { ownerUrl = lease.OwnerBaseUrl, expiresAt = lease.ExpiresAt });
        }

        if (lease is null)
        {
            lease = WhatsAppWebSessionLease.Create(
                sessionId,
                tenantId,
                lineNumber,
                request.InstanceId,
                instanceUrl,
                expiresAt,
                now);
            dbContext.WhatsAppWebSessionLeases.Add(lease);
        }
        else if (lease.IsOwnedBy(request.InstanceId))
        {
            lease.Renew(instanceUrl, expiresAt, now);
        }
        else
        {
            lease.TransferTo(request.InstanceId, instanceUrl, expiresAt, now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(new { ownerUrl = instanceUrl, expiresAt });
    }

    private static async Task<IResult> ReleaseWhatsAppWebSessionLeaseAsync(
        string sessionId,
        HttpContext httpContext,
        IConfiguration configuration,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorizedWhatsAppWebRequest(httpContext, configuration))
            return Results.Unauthorized();
        if (!IsValidSessionId(sessionId))
            return Results.BadRequest();

        var instanceId = httpContext.Request.Headers["X-WhatsApp-Web-Instance"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(instanceId))
            return Results.BadRequest();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (dbContext.Database.IsNpgsql())
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtext({sessionId}))",
                cancellationToken);
        }

        var lease = await dbContext.WhatsAppWebSessionLeases
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.SessionId == sessionId, cancellationToken);
        if (lease is not null && lease.IsOwnedBy(instanceId))
        {
            dbContext.WhatsAppWebSessionLeases.Remove(lease);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return Results.NoContent();
    }

    private static bool IsAuthorizedWhatsAppWebRequest(HttpContext httpContext, IConfiguration configuration)
    {
        var expected = configuration["WHATSAPP_WEB_WEBHOOK_SECRET"]
            ?? configuration["WhatsAppWeb:WebhookSecret"];
        var received = httpContext.Request.Headers["X-WhatsApp-Web-Secret"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(received))
            return false;

        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var receivedBytes = Encoding.UTF8.GetBytes(received);
        return expectedBytes.Length == receivedBytes.Length &&
            CryptographicOperations.FixedTimeEquals(expectedBytes, receivedBytes);
    }

    private static bool IsValidSessionId(string sessionId)
    {
        return TryParseSessionId(sessionId, out _, out _);
    }

    private static bool TryParseSessionId(string sessionId, out Guid tenantId, out int lineNumber)
    {
        tenantId = Guid.Empty;
        lineNumber = 0;
        var separatorIndex = sessionId.LastIndexOf("-qr-", StringComparison.OrdinalIgnoreCase);
        return separatorIndex > 0 &&
            Guid.TryParse(sessionId[..separatorIndex], out tenantId) &&
            int.TryParse(sessionId[(separatorIndex + 4)..], out lineNumber) &&
            lineNumber is > 0 and <= 100;
    }

    private static bool TryNormalizeInstanceUrl(string? value, out string instanceUrl)
    {
        instanceUrl = string.Empty;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
            return false;

        instanceUrl = uri.GetLeftPart(UriPartial.Authority);
        return true;
    }

    private static async Task<bool> HasCurrentLeaseOwnershipAsync(
        string sessionId,
        HttpContext httpContext,
        AppDbContext dbContext)
    {
        var instanceId = httpContext.Request.Headers["X-WhatsApp-Web-Instance"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(instanceId))
            return false;

        var lease = await dbContext.WhatsAppWebSessionLeases
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.SessionId == sessionId, httpContext.RequestAborted);
        return lease is not null && lease.IsOwnedBy(instanceId) && !lease.IsExpired(DateTime.UtcNow);
    }

    private static async Task<IResult> ReceiveWhatsAppWebEventAsync(
        HttpContext httpContext,
        IConfiguration configuration,
        IWhatsAppAccountRepository accountRepository,
        IWebhookEventRepository webhookEventRepository,
        AppDbContext dbContext,
        ILogger<Program> logger)
    {
        var expectedSecret = configuration["WHATSAPP_WEB_WEBHOOK_SECRET"]
            ?? configuration["WhatsAppWeb:WebhookSecret"];
        var receivedSecret = httpContext.Request.Headers["X-WhatsApp-Web-Secret"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(expectedSecret) || receivedSecret != expectedSecret)
            return Results.Unauthorized();

        using var reader = new StreamReader(httpContext.Request.Body, Encoding.UTF8);
        var rawBody = await reader.ReadToEndAsync();
        WebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<WebhookPayload>(rawBody, JsonOptions);
        }
        catch (JsonException)
        {
            return Results.BadRequest("Invalid payload");
        }

        var value = payload?.Entry?.FirstOrDefault()?.Changes?.FirstOrDefault()?.Value;
        var phoneNumberId = value?.Metadata?.PhoneNumberId;
        var messageId = value?.Messages?.FirstOrDefault()?.Id;
        logger.LogInformation("WhatsApp Web webhook received: phoneNumberId={PhoneNumberId}, messageCount={MessageCount}",
            phoneNumberId, value?.Messages?.Count ?? 0);
        if (string.IsNullOrWhiteSpace(phoneNumberId) || string.IsNullOrWhiteSpace(messageId))
            return Results.BadRequest("Missing WhatsApp Web event identifiers");

        var idempotencyKey = $"whatsapp-web:{phoneNumberId}:{messageId}";
        if (await webhookEventRepository.GetByIdempotencyKeyAsync(idempotencyKey) is not null)
            return Results.Ok("OK");

        await EnsureWhatsAppWebAccountAsync(phoneNumberId, accountRepository, dbContext);

        var webhookEvent = WebhookEvent.Create(
            phoneNumberId,
            idempotencyKey,
            rawBody,
            "whatsapp-web");
        await webhookEventRepository.AddAsync(webhookEvent);

        logger.LogInformation("WhatsApp Web event {EventId} received for {PhoneNumberId}",
            webhookEvent.Id, phoneNumberId);
        return Results.Ok("OK");
    }

    private static async Task EnsureWhatsAppWebAccountAsync(
        string phoneNumberId,
        IWhatsAppAccountRepository accountRepository,
        AppDbContext dbContext)
    {
        if (!phoneNumberId.StartsWith("qr:", StringComparison.OrdinalIgnoreCase))
            return;

        var parts = phoneNumberId.Split(':', 3);
        if (parts.Length != 3 || !Guid.TryParse(parts[1], out var tenantId) ||
            !int.TryParse(parts[2], out var lineNumber) || lineNumber < 1)
            return;

        var existingAccount = await accountRepository.GetByTenantAndSlotAsync(
            tenantId,
            WhatsAppConnectionType.QrCode,
            lineNumber);
        if (existingAccount is not null)
        {
            if (!string.Equals(existingAccount.PhoneNumberId, phoneNumberId, StringComparison.Ordinal))
            {
                existingAccount.Update(
                    existingAccount.WabaId,
                    phoneNumberId,
                    existingAccount.AccessTokenRef);
                await accountRepository.UpdateAsync(existingAccount);
            }
            return;
        }

        var account = WhatsAppAccount.Create(
            tenantId,
            "whatsapp-web",
            phoneNumberId,
            $"whatsapp-web:session:{tenantId:D}:{lineNumber}",
            WhatsAppConnectionType.QrCode,
            lineNumber);
        try
        {
            await accountRepository.AddAsync(account);
        }
        catch (DbUpdateException)
        {
            // Another concurrent event may have created this unique slot first.
            dbContext.Entry(account).State = EntityState.Detached;
        }
    }

    private static async Task<IResult> VerifyChallengeAsync(
        [FromQuery] string hub_mode,
        [FromQuery] string hub_verify_token,
        [FromQuery] string hub_challenge,
        ISecretStore secretStore,
        ILogger<Program> logger)
    {
        var verifyToken = await secretStore.GetAsync("meta:verify_token");

        if (verifyToken is null)
        {
            logger.LogWarning("Verify token not configured");
            return Results.StatusCode(500);
        }

        if (hub_mode != "subscribe" || hub_verify_token != verifyToken)
        {
            logger.LogWarning("Invalid webhook verification attempt");
            return Results.BadRequest("Invalid verification");
        }

        logger.LogInformation("Webhook verification successful");
        return Results.Ok(hub_challenge);
    }

    private static async Task<IResult> ReceiveEventAsync(
        HttpContext httpContext,
        ISecretStore secretStore,
        IWebhookEventRepository webhookEventRepository,
        ILogger<Program> logger)
    {
        // Read raw body for signature verification
        httpContext.Request.EnableBuffering();
        using var reader = new StreamReader(httpContext.Request.Body, Encoding.UTF8, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync();
        httpContext.Request.Body.Position = 0;

        // Validate signature
        var signature = httpContext.Request.Headers["X-Hub-Signature-256"].FirstOrDefault();
        if (string.IsNullOrEmpty(signature))
        {
            logger.LogWarning("Missing webhook signature");
            return Results.BadRequest("Missing signature");
        }

        var appSecret = await secretStore.GetAsync("meta:app_secret");
        if (appSecret is null)
        {
            logger.LogWarning("App secret not configured");
            return Results.StatusCode(500);
        }

        if (!ValidateSignature(rawBody, signature, appSecret))
        {
            logger.LogWarning("Invalid webhook signature");
            return Results.BadRequest("Invalid signature");
        }

        // Parse event to extract phone_number_id for tenant resolution
        WebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<WebhookPayload>(rawBody, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to parse webhook payload");
            return Results.BadRequest("Invalid payload");
        }

        if (payload is null || payload.Entry is null || payload.Entry.Count == 0)
        {
            return Results.Ok("OK");
        }

        // Extract phone_number_id for tenant resolution
        var phoneNumberId = payload.Entry?
            .FirstOrDefault()?.Changes?
            .FirstOrDefault()?.Value?.Metadata?.PhoneNumberId;

        if (string.IsNullOrEmpty(phoneNumberId))
        {
            logger.LogWarning("Missing phone_number_id in webhook");
            // Still accept the webhook to avoid retries
            return Results.Ok("OK");
        }

        // Create idempotency key from entry ID + timestamp
        var entryId = payload.Entry?.FirstOrDefault()?.Id ?? "unknown";
        var idempotencyKey = $"{entryId}:{payload.Entry?.FirstOrDefault()?.Changes?.FirstOrDefault()?.Value?.Metadata?.DisplayPhoneNumber}";

        // Check for duplicate
        var existingEvent = await webhookEventRepository.GetByIdempotencyKeyAsync(idempotencyKey);
        if (existingEvent is not null)
        {
            logger.LogInformation("Duplicate webhook event {IdempotencyKey}", idempotencyKey);
            return Results.Ok("OK");
        }

        // Create webhook event
        var webhookEvent = WebhookEvent.Create(
            phoneNumberId: phoneNumberId,
            idempotencyKey: idempotencyKey,
            rawPayload: rawBody,
            signature: signature);

        await webhookEventRepository.AddAsync(webhookEvent);

        logger.LogInformation("Webhook event {EventId} received for {PhoneNumberId}",
            webhookEvent.Id, phoneNumberId);

        return Results.Ok("OK");
    }

    private static bool ValidateSignature(string payload, string signatureHeader, string appSecret)
    {
        if (!signatureHeader.StartsWith("sha256="))
            return false;

        var expectedSignature = signatureHeader["sha256=".Length..];

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var computedSignature = Convert.ToHexString(hash).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedSignature),
            Encoding.UTF8.GetBytes(computedSignature));
    }
}

// Webhook payload models
public sealed class WebhookPayload
{
    public string? Object { get; set; }
    public List<WebhookEntry>? Entry { get; set; }
}

public sealed class WebhookEntry
{
    public string? Id { get; set; }
    public long Time { get; set; }
    public List<WebhookChange>? Changes { get; set; }
}

public sealed class WebhookChange
{
    public string? Field { get; set; }
    public WebhookValue? Value { get; set; }
}

public sealed class WebhookValue
{
    [JsonPropertyName("messaging_product")]
    public string? MessagingProduct { get; set; }
    public WebhookMetadata? Metadata { get; set; }
    public List<WebhookContact>? Contacts { get; set; }
    public List<WebhookMessage>? Messages { get; set; }
    public List<WebhookStatus>? Statuses { get; set; }
    public string? Type { get; set; }
    public List<WebhookError>? Errors { get; set; }
}

public sealed class WebhookMetadata
{
    [JsonPropertyName("display_phone_number")]
    public string? DisplayPhoneNumber { get; set; }

    [JsonPropertyName("phone_number_id")]
    public string? PhoneNumberId { get; set; }
}

public sealed class WebhookContact
{
    [JsonPropertyName("wa_id")]
    public string? WaId { get; set; }
    public WebhookProfile? Profile { get; set; }
}

public sealed class WebhookProfile
{
    public string? Name { get; set; }
}

public sealed class WebhookMessage
{
    public string? From { get; set; }
    public string? Id { get; set; }
    public long Timestamp { get; set; }
    public string? Type { get; set; }
    public WebhookText? Text { get; set; }
    public WebhookImage? Image { get; set; }
    public WebhookDocument? Document { get; set; }
    public WebhookAudio? Audio { get; set; }
    public WebhookContext? Context { get; set; }
}

public sealed class WebhookText
{
    public string? Body { get; set; }
}

public sealed class WebhookImage
{
    public string? Id { get; set; }
    public string? Mime { get; set; }
    public string? Caption { get; set; }
}

public sealed class WebhookDocument
{
    public string? Id { get; set; }
    public string? Mime { get; set; }
    public string? Filename { get; set; }
    public string? Caption { get; set; }
}

public sealed class WebhookAudio
{
    public string? Id { get; set; }
    public string? Mime { get; set; }
    public bool? Voice { get; set; }
}

public sealed class WebhookContext
{
    public string? From { get; set; }
    public string? Id { get; set; }
}

public sealed class WebhookStatus
{
    public string? Id { get; set; }
    public string? Status { get; set; }
    public long Timestamp { get; set; }
    public string? RecipientId { get; set; }
    public WebhookConversation? Conversation { get; set; }
    public WebhookPricing? Pricing { get; set; }
}

public sealed class WebhookConversation
{
    public string? Id { get; set; }
    public WebhookConversationOrigin? Origin { get; set; }
}

public sealed class WebhookConversationOrigin
{
    public string? Type { get; set; }
}

public sealed class WebhookPricing
{
    public bool? Billable { get; set; }
    public string? PricingModel { get; set; }
    public string? Category { get; set; }
}

public sealed class WebhookError
{
    public int Code { get; set; }
    public string? Title { get; set; }
    public string? Message { get; set; }
    public string? ErrorData { get; set; }
}

public sealed record WhatsAppWebSessionRequest
{
    public string Payload { get; init; } = string.Empty;
}

public sealed record WhatsAppWebSessionLeaseRequest
{
    public string InstanceId { get; init; } = string.Empty;
    public string InstanceUrl { get; init; } = string.Empty;
}

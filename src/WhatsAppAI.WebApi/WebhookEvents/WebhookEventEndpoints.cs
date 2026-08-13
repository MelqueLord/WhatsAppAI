using Microsoft.AspNetCore.Mvc;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Messaging;
using WhatsAppAI.Infrastructure.Identity;

namespace WhatsAppAI.WebApi.WebhookEvents;

public static class WebhookEventEndpoints
{
    public static IEndpointRouteBuilder MapWebhookEventEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/webhook-events")
            .WithTags("Webhook Events")
            .RequireAuthorization("PlatformAdmin");

        group.MapGet("/", ListEventsAsync)
            .WithName("ListWebhookEvents");

        group.MapGet("/{eventId:guid}", GetEventByIdAsync)
            .WithName("GetWebhookEventById");

        group.MapPost("/{eventId:guid}/reprocess", ReprocessEventAsync)
            .WithName("ReprocessWebhookEvent")
            ;

        return app;
    }

    private static async Task<IResult> ListEventsAsync(
        [FromQuery] string? status,
        [FromQuery] int limit = 50,
        IWebhookEventRepository webhookEventRepository = null!)
    {
        // This is a simplified version - in production, you'd want pagination
        var events = status?.ToLowerInvariant() switch
        {
            "unknown" => await webhookEventRepository.GetPendingEventsAsync(limit),
            "failed" => await webhookEventRepository.GetRetryableEventsAsync(limit),
            _ => await webhookEventRepository.GetPendingEventsAsync(limit)
        };

        return Results.Ok(events.Select(e => new WebhookEventResponse
        {
            Id = e.Id,
            PhoneNumberId = e.PhoneNumberId,
            Status = e.Status.ToString(),
            CreatedAt = e.CreatedAt,
            ProcessedAt = e.ProcessedAt,
            RetryCount = e.RetryCount,
            ErrorMessage = e.ErrorMessage
        }));
    }

    private static async Task<IResult> GetEventByIdAsync(
        Guid eventId,
        IWebhookEventRepository webhookEventRepository)
    {
        var webhookEvent = await webhookEventRepository.GetByIdAsync(eventId);
        if (webhookEvent is null)
            return Results.NotFound();

        return Results.Ok(new WebhookEventResponse
        {
            Id = webhookEvent.Id,
            PhoneNumberId = webhookEvent.PhoneNumberId,
            TenantId = webhookEvent.TenantId,
            Status = webhookEvent.Status.ToString(),
            CreatedAt = webhookEvent.CreatedAt,
            ProcessedAt = webhookEvent.ProcessedAt,
            RetryCount = webhookEvent.RetryCount,
            ErrorMessage = webhookEvent.ErrorMessage
        });
    }

    private static async Task<IResult> ReprocessEventAsync(
        Guid eventId,
        IWebhookEventRepository webhookEventRepository,
        ICurrentTenant currentTenant,
        ILogger<Program> logger)
    {
        var webhookEvent = await webhookEventRepository.GetByIdAsync(eventId);
        if (webhookEvent is null)
            return Results.NotFound();

        if (webhookEvent.Status != WebhookEventStatus.Failed &&
            webhookEvent.Status != WebhookEventStatus.Dead)
        {
            return Results.BadRequest(new { error = "Only failed or dead events can be reprocessed." });
        }

        // Reset event for reprocessing
        webhookEvent.MarkProcessing();
        await webhookEventRepository.UpdateAsync(webhookEvent);

        logger.LogInformation(
            "Webhook event {EventId} queued for reprocessing by user {UserId}",
            eventId, currentTenant.UserId);

        return Results.Ok(new WebhookEventResponse
        {
            Id = webhookEvent.Id,
            PhoneNumberId = webhookEvent.PhoneNumberId,
            Status = webhookEvent.Status.ToString(),
            Message = "Event queued for reprocessing."
        });
    }
}

public sealed class WebhookEventResponse
{
    public Guid Id { get; init; }
    public string PhoneNumberId { get; init; } = string.Empty;
    public Guid? TenantId { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? ProcessedAt { get; init; }
    public int RetryCount { get; init; }
    public string? ErrorMessage { get; init; }
    public string? Message { get; init; }
}

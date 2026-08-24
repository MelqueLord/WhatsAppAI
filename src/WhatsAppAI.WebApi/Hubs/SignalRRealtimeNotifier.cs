using Microsoft.AspNetCore.SignalR;
using WhatsAppAI.Application.Abstractions;

namespace WhatsAppAI.WebApi.Hubs;

/// <summary>
/// IRealtimeNotifier implementation that pushes events to the InboxHub tenant group.
/// Registered in WebApi so Infrastructure never needs to reference WebApi types.
/// </summary>
public sealed class SignalRRealtimeNotifier(IHubContext<InboxHub> hubContext) : IRealtimeNotifier
{
    public Task NotifyTenantAsync(Guid tenantId, string eventName, object payload, CancellationToken cancellationToken = default)
        => hubContext.Clients
            .Group($"tenant:{tenantId}")
            .SendAsync(eventName, payload, cancellationToken);
}

namespace WhatsAppAI.Application.Abstractions;

/// <summary>
/// Sends real-time push notifications (e.g. SignalR) to all connected clients
/// belonging to a given tenant, without creating a circular dependency between
/// Infrastructure and WebApi.
/// </summary>
public interface IRealtimeNotifier
{
    Task NotifyTenantAsync(Guid tenantId, string eventName, object payload, CancellationToken cancellationToken = default);
}

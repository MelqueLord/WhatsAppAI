using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using WhatsAppAI.Application.Abstractions;

namespace WhatsAppAI.WebApi.Hubs;

[Authorize]
public class InboxHub(ICurrentTenant currentTenant) : Hub
{
    public override async Task OnConnectedAsync()
    {
        if (currentTenant.TenantId is null)
        {
            await Clients.Caller.SendAsync("Error", "No tenant context");
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant:{currentTenant.TenantId}");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (currentTenant.TenantId is not null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"tenant:{currentTenant.TenantId}");
        }
        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinConversation(Guid conversationId)
    {
        if (currentTenant.TenantId is null) return;
        await Groups.AddToGroupAsync(Context.ConnectionId, $"conversation:{conversationId}");
    }

    public async Task LeaveConversation(Guid conversationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conversation:{conversationId}");
    }
}

public static class InboxHubMethods
{
    public const string NewMessage = "NewMessage";
    public const string MessageStatusUpdated = "MessageStatusUpdated";
    public const string ConversationUpdated = "ConversationUpdated";
    public const string TypingIndicator = "TypingIndicator";
}

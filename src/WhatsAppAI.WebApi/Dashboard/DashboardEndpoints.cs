using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Identity;
using WhatsAppAI.Domain.Messaging;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.WebApi.Dashboard;

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dashboard")
            .WithTags("Dashboard")
            .RequireAuthorization("RequireTenantContext");

        group.MapGet("/stats", GetStatsAsync);

        return app;
    }

    private static async Task<IResult> GetStatsAsync(
        ICurrentTenant currentTenant,
        AppDbContext dbContext)
    {
        if (currentTenant.TenantId is null) return Results.Unauthorized();

        var tenantId = currentTenant.TenantId.Value;

        var operatorCount = await dbContext.TenantMemberships
            .Where(m => m.TenantId == tenantId && m.Role == MembershipRole.Operator && m.Status == MembershipStatus.Active)
            .CountAsync();

        var today = DateTime.UtcNow.Date;
        var messagesToday = await dbContext.Messages
            .Where(m => m.TenantId == tenantId && m.Direction == MessageDirection.Inbound && m.CreatedAt >= today)
            .CountAsync();

        var activeConversations = await dbContext.Conversations
            .Where(c => c.TenantId == tenantId && c.Mode != ConversationMode.Paused)
            .CountAsync();

        return Results.Ok(new
        {
            operatorCount,
            messagesToday,
            activeConversations
        });
    }
}

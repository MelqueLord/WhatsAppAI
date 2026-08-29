using Microsoft.AspNetCore.Mvc;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Automation.Policy;
using WhatsAppAI.Domain.Audit;
using WhatsAppAI.Domain.Usage;
using WhatsAppAI.Infrastructure.Identity;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.WebApi.Usage;

public static class UsageEndpoints
{
    public static IEndpointRouteBuilder MapUsageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/usage")
            .WithTags("Usage")
            .RequireAuthorization("RequireTenantContext");

        group.MapGet("/", GetUsageAsync)
            .WithName("GetUsage");

        return app;
    }

    private static async Task<IResult> GetUsageAsync(
        ICurrentTenant currentTenant,
        IUsageLedgerRepository usageRepository,
        IAuditLogRepository auditLogRepository,
        AppDbContext dbContext)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();

        var tenant = await dbContext.Tenants.FindAsync(currentTenant.TenantId.Value);
        if (tenant is null)
            return Results.Unauthorized();

        var monthStart = new DateTime(
            DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthlyAiResponsesUsed = await usageRepository.GetTotalQuantityAsync(
            tenant.Id,
            UsageMetricNames.AiResponses,
            monthStart,
            monthStart.AddMonths(1));
        var monthlyTopUps = await usageRepository.GetTotalQuantityAsync(
            tenant.Id,
            UsageMetricNames.AiResponseTopUps,
            monthStart,
            monthStart.AddMonths(1));
        var monthlyLimit = AiResponseQuotaPolicy.GetEffectiveMonthlyLimit(
            tenant.MonthlyAiResponseLimit, monthlyTopUps);
        double? utilizationPercentage = null;
        if (monthlyLimit is > 0)
            utilizationPercentage = Math.Round(monthlyAiResponsesUsed * 100d / monthlyLimit.Value, 2);
        else if (monthlyLimit == 0)
            utilizationPercentage = 100d;

        var quotaAlerts = await auditLogRepository.GetByTenantAsync(
            tenant.Id, DateTime.UtcNow.AddMonths(-3), DateTime.UtcNow, 20);

        return Results.Ok(new
        {
            aiResponseQuota = new
            {
                baseLimit = tenant.MonthlyAiResponseLimit,
                topUps = monthlyTopUps,
                limit = monthlyLimit,
                used = monthlyAiResponsesUsed,
                remaining = monthlyLimit is null
                    ? (long?)null
                    : Math.Max(0, monthlyLimit.Value - monthlyAiResponsesUsed),
                utilizationPercentage,
                status = AiQuotaAlertPolicy.GetStatus(monthlyLimit, monthlyAiResponsesUsed)
                    .ToString().ToLowerInvariant(),
                aiSuspended = AiQuotaAlertPolicy.GetStatus(monthlyLimit, monthlyAiResponsesUsed) == AiQuotaStatus.Exhausted
            },
            quotaAlerts = quotaAlerts
                .Where(alert => alert.EntityType == "AiResponseQuota")
                .Select(alert => new
                {
                    action = alert.Action,
                    entityId = alert.EntityId,
                    details = alert.Details,
                    occurredAt = alert.OccurredAt
                })
                .ToList()
        });
    }
}

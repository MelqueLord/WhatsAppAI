using Microsoft.AspNetCore.Mvc;
using WhatsAppAI.Application.Abstractions;
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
        AppDbContext dbContext,
        string? provider = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();

        var startDate = from ?? DateTime.UtcNow.AddDays(-30);
        var endDate = to ?? DateTime.UtcNow;

        var tenant = await dbContext.Tenants.FindAsync(currentTenant.TenantId.Value);
        if (tenant is null)
            return Results.Unauthorized();

        var entries = await usageRepository.GetByTenantAsync(
            currentTenant.TenantId.Value, startDate, endDate);

        var filtered = provider is not null
            ? entries.Where(e => e.Provider.Equals(provider, StringComparison.OrdinalIgnoreCase)).ToList()
            : entries;

        var summary = filtered
            .GroupBy(e => new { e.Provider, e.Metric })
            .Select(g => new
            {
                provider = g.Key.Provider,
                metric = g.Key.Metric,
                totalQuantity = g.Sum(e => e.Quantity),
                totalCostMinorUnits = g.Sum(e => e.CostMinorUnits ?? 0),
                currency = g.FirstOrDefault(e => e.Currency != null)?.Currency,
                unit = g.FirstOrDefault(e => e.Unit != null)?.Unit,
                count = g.Count()
            })
            .OrderByDescending(s => s.totalQuantity)
            .ToList();

        var monthStart = new DateTime(
            DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthlyAiResponsesUsed = await usageRepository.GetTotalQuantityAsync(
            tenant.Id,
            UsageMetricNames.AiResponses,
            monthStart,
            monthStart.AddMonths(1));
        var monthlyLimit = tenant.MonthlyAiResponseLimit;
        double? utilizationPercentage = null;
        if (monthlyLimit is > 0)
            utilizationPercentage = Math.Round(monthlyAiResponsesUsed * 100d / monthlyLimit.Value, 2);
        else if (monthlyLimit == 0)
            utilizationPercentage = 100d;

        return Results.Ok(new
        {
            from = startDate,
            to = endDate,
            entries = summary,
            aiResponseQuota = new
            {
                limit = monthlyLimit,
                used = monthlyAiResponsesUsed,
                remaining = monthlyLimit is null
                    ? (long?)null
                    : Math.Max(0, monthlyLimit.Value - monthlyAiResponsesUsed),
                utilizationPercentage,
            },
            disclaimer = "Usage estimates only. Not an invoice."
        });
    }
}

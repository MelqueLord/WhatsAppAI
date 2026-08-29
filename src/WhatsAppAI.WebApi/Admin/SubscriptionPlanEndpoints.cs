using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.WebApi.Admin;

public static class SubscriptionPlanEndpoints
{
    public static IEndpointRouteBuilder MapSubscriptionPlanEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/plans")
            .WithTags("Plans");

        group.MapGet("/", GetActivePlansAsync)
            .WithName("GetActivePlans");

        return app;
    }

    private static async Task<IResult> GetActivePlansAsync(
        AppDbContext dbContext)
    {
        var plans = await dbContext.SubscriptionPlans
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new PlanResponse
            {
                Id = p.Id,
                Name = p.Name,
                Code = p.Code,
                Description = p.Description,
                AiEnabled = p.AiEnabled,
                BotEnabled = p.BotEnabled,
                TagsEnabled = p.TagsEnabled,
                AutomaticDistributionEnabled = p.AutomaticDistributionEnabled,
                IsSelectable = p.IsSelectable,
                DefaultOfficialApiLineCount = p.DefaultOfficialApiLineCount,
                DefaultOperatorLimit = p.DefaultOperatorLimit,
                DefaultMonthlyAiResponseLimit = p.DefaultMonthlyAiResponseLimit,
                MaxOperators = p.MaxOperators
            })
            .ToListAsync();

        return Results.Ok(plans);
    }
}

public sealed class PlanResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool AiEnabled { get; init; }
    public bool BotEnabled { get; init; }
    public bool TagsEnabled { get; init; }
    public bool AutomaticDistributionEnabled { get; init; }
    public bool IsSelectable { get; init; }
    public int DefaultOfficialApiLineCount { get; init; }
    public int DefaultOperatorLimit { get; init; }
    public int? DefaultMonthlyAiResponseLimit { get; init; }
    public int? MaxOperators { get; init; }
}

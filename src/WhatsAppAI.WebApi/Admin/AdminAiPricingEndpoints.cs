using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Domain.Usage;
using WhatsAppAI.Infrastructure.Persistence;

namespace WhatsAppAI.WebApi.Admin;

public static class AdminAiPricingEndpoints
{
    public static IEndpointRouteBuilder MapAdminAiPricingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/ai-pricing")
            .WithTags("Admin - AI Pricing")
            .RequireAuthorization("PlatformAdmin");

        group.MapGet("/", ListAsync).WithName("ListAiModelPricing");
        group.MapPost("/", CreateVersionAsync).WithName("CreateAiModelPricingVersion");

        return app;
    }

    private static async Task<IResult> ListAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var prices = await dbContext.AiModelPricing
            .AsNoTracking()
            .OrderBy(price => price.Provider)
            .ThenBy(price => price.ModelId)
            .ThenByDescending(price => price.Version)
            .ToListAsync(cancellationToken);

        return Results.Ok(prices.Select(ToResponse));
    }

    private static async Task<IResult> CreateVersionAsync(
        [FromBody] CreateAiModelPricingRequest request,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Provider) || string.IsNullOrWhiteSpace(request.ModelId))
            return Results.BadRequest(new { error = "Provider and model are required." });
        if (request.InputCostPer1KMinorUnits < 0 || request.OutputCostPer1KMinorUnits < 0)
            return Results.BadRequest(new { error = "Token prices cannot be negative." });
        if (string.IsNullOrWhiteSpace(request.Currency) || request.Currency.Trim().Length != 3)
            return Results.BadRequest(new { error = "Currency must be an ISO 4217 code." });

        var provider = request.Provider.Trim().ToLowerInvariant();
        var modelId = request.ModelId.Trim();
        var effectiveFrom = request.EffectiveFrom?.ToUniversalTime() ?? DateTime.UtcNow;
        var previous = await dbContext.AiModelPricing
            .Where(price => price.Provider == provider && price.ModelId == modelId)
            .OrderByDescending(price => price.Version)
            .FirstOrDefaultAsync(cancellationToken);
        var currency = request.Currency.Trim().ToUpperInvariant();

        if (previous is not null && effectiveFrom <= previous.EffectiveFrom)
            return Results.Conflict(new { error = "Effective date must be after the latest price version." });
        if (previous is not null && !string.Equals(previous.Currency, currency, StringComparison.Ordinal))
            return Results.BadRequest(new { error = "Currency cannot change between price versions." });

        var pricing = AiModelPricing.Create(
            provider,
            modelId,
            request.InputCostPer1KMinorUnits,
            request.OutputCostPer1KMinorUnits,
            currency,
            (previous?.Version ?? 0) + 1,
            effectiveFrom);

        if (previous is not null && previous.EffectiveTo is null)
            previous.CloseAt(effectiveFrom);

        dbContext.AiModelPricing.Add(pricing);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Results.Conflict(new { error = "Another price version was created concurrently. Refresh and try again." });
        }

        return Results.Created($"/api/admin/ai-pricing/{pricing.Provider}/{pricing.ModelId}", ToResponse(pricing));
    }

    private static object ToResponse(AiModelPricing pricing) => new
    {
        id = pricing.Id,
        provider = pricing.Provider,
        modelId = pricing.ModelId,
        inputCostPer1KMinorUnits = pricing.InputCostPer1KMinorUnits,
        outputCostPer1KMinorUnits = pricing.OutputCostPer1KMinorUnits,
        currency = pricing.Currency,
        version = pricing.Version,
        effectiveFrom = pricing.EffectiveFrom,
        effectiveTo = pricing.EffectiveTo,
        createdAt = pricing.CreatedAt
    };
}

public sealed class CreateAiModelPricingRequest
{
    public string Provider { get; init; } = string.Empty;
    public string ModelId { get; init; } = string.Empty;
    public decimal InputCostPer1KMinorUnits { get; init; }
    public decimal OutputCostPer1KMinorUnits { get; init; }
    public string Currency { get; init; } = string.Empty;
    public DateTime? EffectiveFrom { get; init; }
}

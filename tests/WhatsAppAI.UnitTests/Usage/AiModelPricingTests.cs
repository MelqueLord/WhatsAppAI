using WhatsAppAI.Domain.Usage;

namespace WhatsAppAI.UnitTests.Usage;

public sealed class AiModelPricingTests
{
    [Fact]
    public void CalculateCost_CeilsEachTokenMetricSeparately()
    {
        var pricing = AiModelPricing.Create(
            "OpenAI", "gpt-4.1-mini", 0.15m, 0.60m, "usd", 2,
            new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(1, pricing.CalculateCostMinorUnits(1_001, input: true));
        Assert.Equal(1, pricing.CalculateCostMinorUnits(1_001, input: false));
    }

    [Fact]
    public void Create_NormalizesProviderAndCurrency_AndKeepsVersion()
    {
        var pricing = AiModelPricing.Create(
            " OpenAI ", "gpt-4.1-mini", 15m, 60m, "usd", 3,
            new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal("openai", pricing.Provider);
        Assert.Equal("USD", pricing.Currency);
        Assert.Equal(3, pricing.Version);
    }

    [Fact]
    public void CloseAt_RejectsDateBeforeVersionStart()
    {
        var pricing = AiModelPricing.Create(
            "openai", "gpt-4.1-mini", 15m, 60m, "USD", 1,
            new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc));

        Assert.Throws<ArgumentException>(() => pricing.CloseAt(
            new DateTime(2026, 8, 9, 0, 0, 0, DateTimeKind.Utc)));
    }
}

using WhatsAppAI.Domain.Identity;
using Xunit;

namespace WhatsAppAI.UnitTests.Identity;

public class SubscriptionPlanTests
{
    [Fact]
    public void CreateBot_ReturnsCorrectProperties()
    {
        var plan = SubscriptionPlan.CreateBot();

        Assert.Equal("BOT", plan.Code);
        Assert.Equal("BOT", plan.Name);
        Assert.False(plan.AiEnabled);
        Assert.False(plan.OpenAiRequired);
        Assert.False(plan.AiMetrics);
        Assert.True(plan.IsActive);
    }

    [Fact]
    public void CreateAiBot_ReturnsCorrectProperties()
    {
        var plan = SubscriptionPlan.CreateAiBot();

        Assert.Equal("IA_BOT", plan.Code);
        Assert.Equal("IA + BOT", plan.Name);
        Assert.True(plan.AiEnabled);
        Assert.True(plan.OpenAiRequired);
        Assert.True(plan.AiMetrics);
        Assert.True(plan.IsActive);
    }

    [Theory]
    [InlineData("STAR", 1, 2, 1500, false, false, false)]
    [InlineData("FLOW", 2, 4, 5000, true, true, true)]
    [InlineData("SCALA", 3, 8, 12000, true, true, true)]
    public void CreateCommercialPlan_ReturnsConfiguredEntitlements(
        string code,
        int lines,
        int operators,
        int aiResponses,
        bool botEnabled,
        bool tagsEnabled,
        bool automaticDistributionEnabled)
    {
        var plan = code switch
        {
            "STAR" => SubscriptionPlan.CreateStar(),
            "FLOW" => SubscriptionPlan.CreateFlow(),
            _ => SubscriptionPlan.CreateScala()
        };

        Assert.Equal(code, plan.Code);
        Assert.True(plan.AiEnabled);
        Assert.True(plan.IsSelectable);
        Assert.Equal(lines, plan.DefaultLineCount);
        Assert.Equal(operators, plan.DefaultOperatorLimit);
        Assert.Equal(aiResponses, plan.DefaultMonthlyAiResponseLimit);
        Assert.Equal(botEnabled, plan.BotEnabled);
        Assert.Equal(tagsEnabled, plan.TagsEnabled);
        Assert.Equal(automaticDistributionEnabled, plan.AutomaticDistributionEnabled);
    }

    [Fact]
    public void Create_WithCustomValues_SetsProperties()
    {
        var plan = SubscriptionPlan.Create("Custom", "CUSTOM", "Description", true);

        Assert.Equal("CUSTOM", plan.Code);
        Assert.Equal("Custom", plan.Name);
        Assert.Equal("Description", plan.Description);
        Assert.True(plan.AiEnabled);
    }

    [Fact]
    public void Create_TrimsNameAndCode()
    {
        var plan = SubscriptionPlan.Create("  Test  ", "  CODE  ", null, false);

        Assert.Equal("Test", plan.Name);
        Assert.Equal("CODE", plan.Code);
    }

    [Fact]
    public void Deactivate_SetsIsActiveToFalse()
    {
        var plan = SubscriptionPlan.CreateBot();
        plan.Deactivate();

        Assert.False(plan.IsActive);
        Assert.NotNull(plan.UpdatedAt);
    }

    [Fact]
    public void Activate_SetsIsActiveToTrue()
    {
        var plan = SubscriptionPlan.CreateBot();
        plan.Deactivate();
        plan.Activate();

        Assert.True(plan.IsActive);
    }

    [Fact]
    public void Update_ChangesNameAndDescription()
    {
        var plan = SubscriptionPlan.CreateBot();
        plan.Update("New Name", "New Description");

        Assert.Equal("New Name", plan.Name);
        Assert.Equal("New Description", plan.Description);
        Assert.NotNull(plan.UpdatedAt);
    }
}

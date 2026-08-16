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

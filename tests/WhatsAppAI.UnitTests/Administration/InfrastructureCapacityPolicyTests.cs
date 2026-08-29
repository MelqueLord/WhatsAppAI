using WhatsAppAI.Application.Administration;

namespace WhatsAppAI.UnitTests.Administration;

public sealed class InfrastructureCapacityPolicyTests
{
    [Fact]
    public void Evaluate_BelowWarning_ReturnsNormal()
    {
        var result = InfrastructureCapacityPolicy.Evaluate(19, 25);

        Assert.Equal(76, result.UtilizationPercentage);
        Assert.Equal(InfrastructureCapacityStatus.Normal, result.Status);
    }

    [Fact]
    public void Evaluate_AtWarningThreshold_ReturnsWarning()
    {
        var result = InfrastructureCapacityPolicy.Evaluate(20, 25);

        Assert.Equal(80, result.UtilizationPercentage);
        Assert.Equal(InfrastructureCapacityStatus.Warning, result.Status);
    }

    [Fact]
    public void Evaluate_AtLimit_RequiresMigration()
    {
        var result = InfrastructureCapacityPolicy.Evaluate(25, 25);

        Assert.Equal(100, result.UtilizationPercentage);
        Assert.Equal(InfrastructureCapacityStatus.MigrationRequired, result.Status);
    }

    [Fact]
    public void Evaluate_AboveLimit_CapsPercentageAndRequiresMigration()
    {
        var result = InfrastructureCapacityPolicy.Evaluate(26, 25);

        Assert.Equal(100, result.UtilizationPercentage);
        Assert.Equal(InfrastructureCapacityStatus.MigrationRequired, result.Status);
    }
}

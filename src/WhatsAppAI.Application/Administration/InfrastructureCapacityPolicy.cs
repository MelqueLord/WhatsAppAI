namespace WhatsAppAI.Application.Administration;

public static class InfrastructureCapacityPolicy
{
    private const int WarningPercentage = 80;

    public static InfrastructureCapacityIndicator Evaluate(int current, int limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(current);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);

        var percentage = (int)Math.Min(100L, (long)current * 100 / limit);
        var status = InfrastructureCapacityStatus.Normal;
        if (current >= limit)
            status = InfrastructureCapacityStatus.MigrationRequired;
        else if (percentage >= WarningPercentage)
            status = InfrastructureCapacityStatus.Warning;

        return new InfrastructureCapacityIndicator(current, limit, percentage, status);
    }
}

public sealed record InfrastructureCapacityIndicator(
    int Current,
    int Limit,
    int UtilizationPercentage,
    InfrastructureCapacityStatus Status);

public enum InfrastructureCapacityStatus
{
    Normal,
    Warning,
    MigrationRequired
}

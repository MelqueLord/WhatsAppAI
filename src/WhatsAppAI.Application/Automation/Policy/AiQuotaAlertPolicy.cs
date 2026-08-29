namespace WhatsAppAI.Application.Automation.Policy;

public enum AiQuotaAlertLevel
{
    Warning,
    Exhausted
}

public enum AiQuotaStatus
{
    Normal,
    Warning,
    Exhausted,
    Unlimited
}

public static class AiQuotaAlertPolicy
{
    public static AiQuotaStatus GetStatus(int? monthlyLimit, long responsesUsed)
    {
        var level = GetLevel(monthlyLimit, responsesUsed);
        return level switch
        {
            AiQuotaAlertLevel.Warning => AiQuotaStatus.Warning,
            AiQuotaAlertLevel.Exhausted => AiQuotaStatus.Exhausted,
            null when monthlyLimit is null => AiQuotaStatus.Unlimited,
            _ => AiQuotaStatus.Normal
        };
    }

    public static AiQuotaAlertLevel? GetLevel(int? monthlyLimit, long responsesUsed)
    {
        if (monthlyLimit is < 0)
            throw new ArgumentOutOfRangeException(nameof(monthlyLimit));
        ArgumentOutOfRangeException.ThrowIfNegative(responsesUsed);

        if (monthlyLimit is null)
            return null;
        if (monthlyLimit == 0 || responsesUsed >= monthlyLimit.Value)
            return AiQuotaAlertLevel.Exhausted;

        var warningThreshold = (long)Math.Ceiling(monthlyLimit.Value * 0.8d);
        return responsesUsed >= warningThreshold ? AiQuotaAlertLevel.Warning : null;
    }

    public static string GetAuditAction(AiQuotaAlertLevel level) => level switch
    {
        AiQuotaAlertLevel.Warning => "AiQuota.WarningReached",
        AiQuotaAlertLevel.Exhausted => "AiQuota.Exhausted",
        _ => throw new ArgumentOutOfRangeException(nameof(level))
    };
}

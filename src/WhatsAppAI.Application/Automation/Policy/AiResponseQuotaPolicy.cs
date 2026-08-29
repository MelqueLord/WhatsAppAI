namespace WhatsAppAI.Application.Automation.Policy;

public static class AiResponseQuotaPolicy
{
    public const int TopUpQuantity = 500;

    public static int? GetEffectiveMonthlyLimit(int? baseMonthlyLimit, long monthlyTopUps)
    {
        if (baseMonthlyLimit is < 0)
            throw new ArgumentOutOfRangeException(nameof(baseMonthlyLimit));
        ArgumentOutOfRangeException.ThrowIfNegative(monthlyTopUps);

        if (baseMonthlyLimit is null)
            return null;

        return checked(baseMonthlyLimit.Value + (int)monthlyTopUps);
    }

    public static bool HasAvailableResponse(int? monthlyLimit, long responsesUsed)
    {
        if (monthlyLimit is < 0)
            throw new ArgumentOutOfRangeException(nameof(monthlyLimit));
        ArgumentOutOfRangeException.ThrowIfNegative(responsesUsed);

        return monthlyLimit is null || responsesUsed < monthlyLimit.Value;
    }
}

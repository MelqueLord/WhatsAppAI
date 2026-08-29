namespace WhatsAppAI.Application.Automation.Policy;

public static class AiResponseQuotaPolicy
{
    public static bool HasAvailableResponse(int? monthlyLimit, long responsesUsed)
    {
        if (monthlyLimit is < 0)
            throw new ArgumentOutOfRangeException(nameof(monthlyLimit));
        ArgumentOutOfRangeException.ThrowIfNegative(responsesUsed);

        return monthlyLimit is null || responsesUsed < monthlyLimit.Value;
    }
}

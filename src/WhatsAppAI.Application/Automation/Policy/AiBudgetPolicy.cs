namespace WhatsAppAI.Application.Automation.Policy;

public static class AiBudgetPolicy
{
    public static bool HasAvailableBudget(long monthlyLimit, long tokensUsed, long estimatedTokens)
    {
        return monthlyLimit > 0 && tokensUsed >= 0 && estimatedTokens >= 0 &&
            tokensUsed + estimatedTokens <= monthlyLimit;
    }
}

namespace WhatsAppAI.Application.Automation.Policy;

public static class AiBudgetPolicy
{
    public static bool HasAvailableBudget(long? monthlyLimit, long used, long reserved, long estimate)
    {
        if (monthlyLimit is null)
            return true;
        return monthlyLimit >= 0 && used >= 0 && reserved >= 0 && estimate >= 0 &&
            used <= monthlyLimit && reserved <= monthlyLimit - used && estimate <= monthlyLimit - used - reserved;
    }

    public static bool HasAvailableBudget(long monthlyLimit, long tokensUsed, long estimatedTokens)
    {
        return monthlyLimit > 0 && tokensUsed >= 0 && estimatedTokens >= 0 &&
            tokensUsed + estimatedTokens <= monthlyLimit;
    }

    public static long EstimateInputTokens(IEnumerable<string?> contents, string? systemPrompt, int maxOutputTokens)
    {
        var characters = contents.Sum(content => content?.Length ?? 0) + (systemPrompt?.Length ?? 0);
        return Math.Max(1, (characters + 3) / 4) + Math.Max(0, maxOutputTokens);
    }
}

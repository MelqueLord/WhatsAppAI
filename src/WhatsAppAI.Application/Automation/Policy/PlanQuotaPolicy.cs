namespace WhatsAppAI.Application.Automation.Policy;

public static class PlanQuotaPolicy
{
    public static int? ResolveMonthlyAiResponseLimit(
        int? currentLimit,
        int? currentPlanDefault,
        int? newPlanDefault) =>
        currentLimit == currentPlanDefault ? newPlanDefault : currentLimit;
}

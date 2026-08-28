namespace WhatsAppAI.Application.Automation.Policy;

public static class BehaviorPolicy
{
    public static readonly IReadOnlySet<string> RequiredHandoffReasons = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "sensitive_topic",
        "out_of_scope",
        "customer_request",
        "escalation_needed",
        "complaint",
        "refund_request",
        "legal_issue"
    };

    private static bool ShouldBlock(string? handoffReason)
    {
        return handoffReason is not null && RequiredHandoffReasons.Contains(handoffReason);
    }

    public static AiDecision SanitizeDecision(AiDecision decision, double confidenceThreshold)
    {
        if (decision.Action == AiAction.Reply &&
            (decision.Confidence < confidenceThreshold || ShouldBlock(decision.HandoffReason)))
        {
            return decision with
            {
                Action = AiAction.Handoff,
                HandoffReason = decision.HandoffReason ?? "low_confidence"
            };
        }

        return decision;
    }
}

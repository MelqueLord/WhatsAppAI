namespace WhatsAppAI.Application.Automation.Policy;

public static class BehaviorPolicy
{
    private static bool ShouldBlock(string? handoffReason)
    {
        var blockedReasons = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "sensitive_topic",
            "out_of_scope",
            "customer_request",
            "escalation_needed",
            "complaint",
            "refund_request",
            "legal_issue"
        };

        if (handoffReason is not null && blockedReasons.Contains(handoffReason))
            return true;

        return false;
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

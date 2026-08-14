namespace WhatsAppAI.Application.Automation.Policy;

public static class BehaviorPolicy
{
    public static bool ShouldHandoff(AiDecision decision)
    {
        if (decision.Action == AiAction.Handoff)
            return true;

        if (decision.Confidence < 0.5)
            return true;

        return false;
    }

    public static bool ShouldBlock(double confidence, string? handoffReason)
    {
        var blockedReasons = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "sensitive_topic",
            "out_of_scope",
            "customer_request",
            "low_confidence",
            "escalation_needed",
            "complaint",
            "refund_request",
            "legal_issue"
        };

        if (handoffReason is not null && blockedReasons.Contains(handoffReason))
            return true;

        if (confidence < 0.3)
            return true;

        return false;
    }

    public static AiDecision SanitizeDecision(AiDecision decision)
    {
        if (decision.Action == AiAction.Reply && ShouldBlock(decision.Confidence, decision.HandoffReason))
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

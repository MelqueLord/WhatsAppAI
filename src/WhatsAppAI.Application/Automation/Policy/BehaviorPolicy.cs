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
        "legal_issue",
        AiOutputSafetyPolicy.UnsafeContentHandoffReason
    };

    private static bool ShouldBlock(string? handoffReason)
    {
        return handoffReason is not null && RequiredHandoffReasons.Contains(handoffReason);
    }

    public static AiDecision SanitizeDecision(AiDecision decision, double confidenceThreshold)
    {
        if (decision.Action == AiAction.Reply &&
            !string.IsNullOrWhiteSpace(decision.Text) &&
            !AiOutputSafetyPolicy.IsSafe(decision.Text))
        {
            return decision with
            {
                Action = AiAction.Handoff,
                HandoffReason = AiOutputSafetyPolicy.UnsafeContentHandoffReason,
                Text = null
            };
        }

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

    public static AiResponse SanitizeResponse(AiResponse response, double confidenceThreshold)
    {
        var decision = SanitizeDecision(response.Decision, confidenceThreshold);
        if (decision.Action == AiAction.Reply &&
            !string.IsNullOrWhiteSpace(response.Content) &&
            !AiOutputSafetyPolicy.IsSafe(response.Content))
        {
            decision = decision with
            {
                Action = AiAction.Handoff,
                HandoffReason = AiOutputSafetyPolicy.UnsafeContentHandoffReason,
                Text = null
            };
        }

        return response with
        {
            Decision = decision,
            Content = decision.Action == AiAction.Reply ? response.Content : null
        };
    }
}

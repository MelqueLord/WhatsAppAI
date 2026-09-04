namespace WhatsAppAI.Application.Automation.Policy;

public static class KnownKnowledgeResponsePolicy
{
    public static AiResponse RecoverKnownAnswer(
        AiResponse response,
        IReadOnlyList<string> relevantKnowledge)
    {
        if (response.Decision.Action != AiAction.Handoff ||
            !string.Equals(response.Decision.HandoffReason, "out_of_scope", StringComparison.OrdinalIgnoreCase) ||
            relevantKnowledge.Count == 0)
        {
            return response;
        }

        var answer = string.Join(" ", relevantKnowledge)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
        if (answer.Length > AiOutputSafetyPolicy.MaxReplyCharacters)
            answer = answer[..(AiOutputSafetyPolicy.MaxReplyCharacters - 3)].TrimEnd() + "...";

        if (!AiOutputSafetyPolicy.IsSafe(answer))
            return response;

        var decision = response.Decision with
        {
            Action = AiAction.Reply,
            Text = answer,
            HandoffReason = null,
            Confidence = Math.Max(response.Decision.Confidence, 0.8)
        };
        return response with
        {
            Decision = decision,
            Content = answer
        };
    }
}

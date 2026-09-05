using WhatsAppAI.Application.Automation.Context;
using WhatsAppAI.Domain.Automation;

namespace WhatsAppAI.Application.Automation.Policy;

public static class AiSupervisedLearningPolicy
{
    public static AiResponseExample? CreateExampleFromFeedback(
        Guid tenantId,
        Guid interactionId,
        AiFeedbackRating rating,
        string? customerMessage,
        string? originalResponse,
        string? correctedResponse)
    {
        var answer = rating == AiFeedbackRating.NeedsCorrection
            ? correctedResponse
            : originalResponse;
        var question = AiContextSanitizer.RedactPersonalData(customerMessage).Trim();
        var safeAnswer = AiContextSanitizer.RedactPersonalData(answer).Trim();

        // A note explains the problem but is not an approved answer. It must not
        // become training data until an operator provides the desired response.
        if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(safeAnswer))
            return null;

        if (!AiOutputSafetyPolicy.IsSafe(safeAnswer))
            return null;

        return AiResponseExample.CreateFromOperatorFeedback(
            tenantId,
            interactionId,
            Limit(question, 500),
            safeAnswer);
    }

    private static string Limit(string value, int maxCharacters) =>
        value.Length <= maxCharacters ? value : value[..maxCharacters].TrimEnd();
}

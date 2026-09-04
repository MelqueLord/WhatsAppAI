using WhatsAppAI.Domain.Knowledge;
using WhatsAppAI.Application.Automation.Context;

namespace WhatsAppAI.Application.Automation.Policy;

public static class CompanyMemoryPolicy
{
    private const string MemoryTitlePrefix = "Memória da empresa: ";

    public static KnowledgeItem? CreateFromGroundedReply(
        Guid tenantId,
        string? customerMessage,
        AiResponse response,
        IReadOnlyList<string> relevantKnowledge,
        double confidenceThreshold)
    {
        if (string.IsNullOrWhiteSpace(customerMessage) ||
            response.Decision.Action != AiAction.Reply ||
            string.IsNullOrWhiteSpace(response.Content) ||
            response.Decision.Confidence < Math.Max(confidenceThreshold, 0.8) ||
            relevantKnowledge.Count == 0 ||
            !AiOutputSafetyPolicy.IsSafe(response.Content))
        {
            return null;
        }

        var question = AiContextSanitizer.RedactPersonalData(customerMessage.Trim());
        if (string.IsNullOrWhiteSpace(question))
            return null;

        return KnowledgeItem.Create(
            tenantId,
            $"{MemoryTitlePrefix}{Limit(question, 180)}",
            AiContextSanitizer.RedactPersonalData(response.Content.Trim()),
            priority: -100,
            category: KnowledgeCategories.General);
    }

    public static bool IsMemory(KnowledgeItem item) =>
        item.Title.StartsWith(MemoryTitlePrefix, StringComparison.Ordinal);

    private static string Limit(string value, int maxCharacters) =>
        value.Length <= maxCharacters
            ? value
            : $"{value[..(maxCharacters - 3)].TrimEnd()}...";
}

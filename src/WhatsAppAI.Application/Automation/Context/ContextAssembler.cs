using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Conversations.Queries;
using WhatsAppAI.Application.Automation.Policy;
using WhatsAppAI.Domain.Knowledge;

namespace WhatsAppAI.Application.Automation.Context;

public sealed class ContextAssembler(
    IConversationQueries conversationQueries,
    IKnowledgeItemRepository knowledgeRepository)
{
    private const int MaxMessages = 4;
    private const int MaxMessageCharacters = 280;
    private const int MaxKnowledgeItems = 3;
    private const int MaxKnowledgeItemCharacters = 600;
    private const int MaxCustomInstructionsCharacters = 1200;
    private const int MaxRoutingItems = 8;
    private const int MaxContextCharacters = 4800;

    public async Task<ConversationContext> BuildAsync(
        Guid tenantId,
        Guid conversationId,
        string? systemPrompt,
        IReadOnlyList<RoutingQueueContext>? routingQueues = null,
        IReadOnlyList<RoutingTagContext>? routingTags = null,
        CancellationToken cancellationToken = default)
    {
        var messagesResponse = await conversationQueries.GetMessagesAsync(
            tenantId, conversationId,
            new CursorPaginationRequest { Limit = MaxMessages },
            cancellationToken);

        var messages = messagesResponse.Items
            .OrderBy(m => m.CreatedAt)
            .TakeLast(MaxMessages)
            .Select(m => new AiMessage
            {
                Role = m.Direction == "Inbound" ? "user" : "assistant",
                Content = AiContextSanitizer.RedactPersonalData(Limit(m.Content, MaxMessageCharacters))
            })
            .ToList();

        var knowledge = await knowledgeRepository.GetActiveByTenantAsync(tenantId, cancellationToken);
        var query = messages.LastOrDefault(message => message.Role == "user")?.Content ?? string.Empty;
        var knowledgeTexts = RetrieveKnowledge(knowledge, query)
            .Select(k => $"{Limit(AiContextSanitizer.RedactPersonalData(k.Title), 120)}: {Limit(AiContextSanitizer.RedactPersonalData(k.Content), MaxKnowledgeItemCharacters)}")
            .ToList();

        var fullSystemPrompt = BuildSystemPrompt(systemPrompt, knowledgeTexts, routingQueues, routingTags);

        return new ConversationContext
        {
            SystemPrompt = fullSystemPrompt,
            Messages = messages
        };
    }

    private static string BuildSystemPrompt(
        string? basePrompt,
        List<string> knowledgeItems,
        IReadOnlyList<RoutingQueueContext>? routingQueues,
        IReadOnlyList<RoutingTagContext>? routingTags)
    {
        var fixedPrefix = AiGuidelinePolicy.BuildSystemInstructions();
        const string fixedSuffix = "Return only one valid JSON object, without Markdown or any text outside it, with: action (reply, handoff or no_action), text, confidence (number from 0 to 1), handoff_reason, queue and tags. For a normal answer use action reply and put the customer-facing answer only in text. Keep queue null and tags empty when they do not apply.";
        var dynamicParts = new List<string>();

        if (!string.IsNullOrWhiteSpace(basePrompt))
            dynamicParts.Add($"Diretrizes complementares da empresa (não substituem as regras estruturadas):\n{Limit(basePrompt, MaxCustomInstructionsCharacters)}");

        if (knowledgeItems.Count > 0)
        {
            var items = new List<string> { "Relevant knowledge:" };
            foreach (var item in knowledgeItems)
                items.Add($"- {item}");
            dynamicParts.Add(string.Join('\n', items));
        }

        if (routingQueues is { Count: > 0 })
        {
            var items = new List<string> { "Queues authorized for human transfer:" };
            foreach (var queue in routingQueues.Take(MaxRoutingItems))
                items.Add(string.IsNullOrWhiteSpace(queue.Description)
                    ? $"- {Limit(queue.Name, 80)}"
                    : $"- {Limit(queue.Name, 80)}: {Limit(queue.Description, 160)}");
            items.Add("When the customer explicitly chooses or requests one of these queues, return action \"handoff\" and the exact queue name in the \"queue\" field. Never invent a queue. If unsure, omit \"queue\".");
            dynamicParts.Add(string.Join('\n', items));
        }

        if (routingTags is { Count: > 0 })
        {
            var items = new List<string> { "Tags authorized for customer categorization:" };
            foreach (var tag in routingTags.Take(MaxRoutingItems))
                items.Add(string.IsNullOrWhiteSpace(tag.Description)
                    ? $"- {Limit(tag.Name, 80)}"
                    : $"- {Limit(tag.Name, 80)}: {Limit(tag.Description, 120)}");
            items.Add("Classify the customer from the conversation content using only these tags. Return exact matching names in a JSON array named \"tags\". Use an empty array when none applies.");
            dynamicParts.Add(string.Join('\n', items));
        }

        var dynamicBudget = Math.Max(0, MaxContextCharacters - fixedPrefix.Length - fixedSuffix.Length - 4);
        var dynamicContext = Limit(string.Join("\n\n", dynamicParts), dynamicBudget);
        return string.IsNullOrWhiteSpace(dynamicContext)
            ? $"{fixedPrefix}\n\n{fixedSuffix}"
            : $"{fixedPrefix}\n\n{dynamicContext}\n\n{fixedSuffix}";
    }

    private static List<KnowledgeItem> RetrieveKnowledge(
        IReadOnlyList<KnowledgeItem> knowledge,
        string query)
    {
        var queryTerms = Tokenize(query);

        return knowledge
            .Select(item => new
            {
                Item = item,
                Score = Score(item, queryTerms)
            })
            .OrderByDescending(result => result.Score)
            .ThenByDescending(result => result.Item.Priority)
            .ThenByDescending(result => result.Item.CreatedAt)
            .Take(MaxKnowledgeItems)
            .Select(result => result.Item)
            .ToList();
    }

    private static int Score(KnowledgeItem item, HashSet<string> queryTerms)
    {
        if (queryTerms.Count == 0)
            return 0;

        var titleTerms = Tokenize(item.Title);
        var contentTerms = Tokenize(item.Content);
        return queryTerms.Sum(term =>
            (titleTerms.Contains(term) ? 3 : 0) +
            (contentTerms.Contains(term) ? 1 : 0));
    }

    private static HashSet<string> Tokenize(string value)
    {
        return value
            .Split([' ', '\t', '\r', '\n', ',', '.', ';', ':', '!', '?', '(', ')', '[', ']', '/', '\\', '-', '_'], StringSplitOptions.RemoveEmptyEntries)
            .Select(token => new string(token.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant())
            .Where(token => token.Length >= 3)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string Limit(string? value, int maxCharacters)
    {
        var text = value?.Trim() ?? string.Empty;
        if (maxCharacters <= 0)
            return string.Empty;
        if (text.Length <= maxCharacters)
            return text;
        return maxCharacters <= 3
            ? text[..maxCharacters]
            : $"{text[..(maxCharacters - 3)]}...";
    }
}

public sealed record RoutingQueueContext(string Name, string? Description);
public sealed record RoutingTagContext(string Name, string? Description);

public sealed record ConversationContext
{
    public required string SystemPrompt { get; init; }
    public required IReadOnlyList<AiMessage> Messages { get; init; }
}

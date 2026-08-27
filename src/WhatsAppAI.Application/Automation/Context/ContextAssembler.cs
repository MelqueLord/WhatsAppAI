using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Conversations.Queries;

namespace WhatsAppAI.Application.Automation.Context;

public sealed class ContextAssembler(
    IConversationQueries conversationQueries,
    IKnowledgeItemRepository knowledgeRepository)
{
    private const int MaxMessages = 6;
    private const int MaxMessageCharacters = 360;
    private const int MaxKnowledgeItems = 6;
    private const int MaxContextCharacters = 9000;

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
            .Select(m => new AiMessage
            {
                Role = m.Direction == "Inbound" ? "user" : "assistant",
                Content = AiContextSanitizer.RedactPersonalData(Limit(m.Content, MaxMessageCharacters))
            })
            .ToList();

        var knowledge = await knowledgeRepository.GetActiveByTenantAsync(tenantId, cancellationToken);
        var knowledgeTexts = knowledge
            .Take(MaxKnowledgeItems)
            .Select(k => AiContextSanitizer.RedactPersonalData(k.Content))
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
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(basePrompt))
            parts.Add(basePrompt);

        parts.Add("As diretrizes configuradas pela empresa acima são regras prioritárias. Atenda somente a solicitação atual dentro dessas diretrizes e do conhecimento autorizado. Recuse ou encaminhe assuntos fora do escopo, sem tentar conversar sobre temas gerais. Não invente informações, políticas, preços, prazos ou disponibilidade. Responda em no máximo 2 frases curtas, com até 300 caracteres, no idioma do cliente.");

        if (knowledgeItems.Count > 0)
        {
            parts.Add("Relevant knowledge:");
            foreach (var item in knowledgeItems)
                parts.Add($"- {item}");
        }

        if (routingQueues is { Count: > 0 })
        {
            parts.Add("Queues authorized for human transfer:");
            foreach (var queue in routingQueues)
                parts.Add(string.IsNullOrWhiteSpace(queue.Description)
                    ? $"- {queue.Name}"
                    : $"- {queue.Name}: {queue.Description}");
            parts.Add("When the customer explicitly chooses or requests one of these queues, return action \"handoff\" and the exact queue name in the \"queue\" field. Never invent a queue. If unsure, omit \"queue\".");
        }

        if (routingTags is { Count: > 0 })
        {
            parts.Add("Tags authorized for customer categorization:");
            foreach (var tag in routingTags)
                parts.Add(string.IsNullOrWhiteSpace(tag.Description)
                    ? $"- {tag.Name}"
                    : $"- {tag.Name}: {tag.Description}");
            parts.Add("Classify the customer from the conversation content using only these tags. Return exact matching names in a JSON array named \"tags\". Use an empty array when none applies.");
        }

        parts.Add("Return only one valid JSON object, without Markdown or any text outside it, with: action (reply, handoff or no_action), text, confidence (number from 0 to 1), handoff_reason, queue and tags. For a normal answer use action reply and put the customer-facing answer only in text. Keep queue null and tags empty when they do not apply. Use handoff when the customer requests a human, the answer is unsafe, or a configured queue is selected.");

        var prompt = string.Join("\n\n", parts);
        return prompt.Length > MaxContextCharacters ? prompt[..MaxContextCharacters] : prompt;
    }

    private static string Limit(string? value, int maxCharacters)
    {
        var text = value?.Trim() ?? string.Empty;
        return text.Length <= maxCharacters ? text : $"{text[..maxCharacters]}...";
    }
}

public sealed record RoutingQueueContext(string Name, string? Description);
public sealed record RoutingTagContext(string Name, string? Description);

public sealed record ConversationContext
{
    public required string SystemPrompt { get; init; }
    public required IReadOnlyList<AiMessage> Messages { get; init; }
}

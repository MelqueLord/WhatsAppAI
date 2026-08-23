using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Conversations.Queries;

namespace WhatsAppAI.Application.Automation.Context;

public sealed class ContextAssembler(
    IConversationQueries conversationQueries,
    IKnowledgeItemRepository knowledgeRepository)
{
    private const int MaxMessages = 8;
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
                Content = m.Content ?? string.Empty
            })
            .ToList();

        var knowledge = await knowledgeRepository.GetActiveByTenantAsync(tenantId, cancellationToken);
        var knowledgeTexts = knowledge
            .Take(MaxKnowledgeItems)
            .Select(k => k.Content)
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

        parts.Add("Atenda somente a solicitação atual e as regras explícitas abaixo. Não converse sobre assuntos fora do atendimento. Seja breve e não invente informações.");

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

        if (routingQueues is { Count: > 0 } || routingTags is { Count: > 0 })
            parts.Add("Return only one JSON object with: action (reply, handoff or no_action), text, confidence, handoff_reason, queue and tags. Keep queue null and tags empty when they do not apply.");

        return string.Join("\n\n", parts).Length > MaxContextCharacters
            ? string.Join("\n\n", parts)[..MaxContextCharacters]
            : string.Join("\n\n", parts);
    }
}

public sealed record RoutingQueueContext(string Name, string? Description);
public sealed record RoutingTagContext(string Name, string? Description);

public sealed record ConversationContext
{
    public required string SystemPrompt { get; init; }
    public required IReadOnlyList<AiMessage> Messages { get; init; }
}

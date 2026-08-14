using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Conversations.Queries;

namespace WhatsAppAI.Application.Automation.Context;

public sealed class ContextAssembler(
    IConversationQueries conversationQueries,
    IKnowledgeItemRepository knowledgeRepository)
{
    private const int MaxMessages = 20;
    private const int MaxKnowledgeItems = 10;
    private const int MaxTotalTokens = 3000;

    public async Task<ConversationContext> BuildAsync(
        Guid tenantId,
        Guid conversationId,
        string? systemPrompt,
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

        var fullSystemPrompt = BuildSystemPrompt(systemPrompt, knowledgeTexts);

        return new ConversationContext
        {
            SystemPrompt = fullSystemPrompt,
            Messages = messages
        };
    }

    private static string BuildSystemPrompt(string? basePrompt, List<string> knowledgeItems)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(basePrompt))
            parts.Add(basePrompt);

        if (knowledgeItems.Count > 0)
        {
            parts.Add("Relevant knowledge:");
            foreach (var item in knowledgeItems)
                parts.Add($"- {item}");
        }

        return string.Join("\n\n", parts);
    }
}

public sealed record ConversationContext
{
    public required string SystemPrompt { get; init; }
    public required IReadOnlyList<AiMessage> Messages { get; init; }
}

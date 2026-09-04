using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Conversations.Queries;
using WhatsAppAI.Application.Automation.Policy;
using WhatsAppAI.Domain.Automation;
using WhatsAppAI.Domain.Knowledge;

namespace WhatsAppAI.Application.Automation.Context;

public sealed class ContextAssembler(
    IConversationQueries conversationQueries,
    IKnowledgeItemRepository knowledgeRepository,
    IAiResponseExampleRepository? responseExampleRepository = null)
{
    private const int MaxMessages = 2;
    private const int MaxMessageCharacters = 180;
    private const int MaxKnowledgeItems = 1;
    private const int MaxKnowledgeItemCharacters = 300;
    private const int MaxBusinessProfileCharacters = 480;
    private const int MaxResponseExampleCharacters = 260;
    private const int MaxCustomInstructionsCharacters = 160;
    private const int MaxRoutingItems = 4;
    private const int MaxContextCharacters = 2200;

    public async Task<ConversationContext> BuildAsync(
        Guid tenantId,
        Guid conversationId,
        string? systemPrompt,
        IReadOnlyList<RoutingQueueContext>? routingQueues = null,
        IReadOnlyList<RoutingTagContext>? routingTags = null,
        CancellationToken cancellationToken = default,
        string? welcomeMessage = null,
        bool isFirstInbound = false)
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
            .Select(k => $"{Limit(AiContextSanitizer.RedactPersonalData(k.Title), 80)}: {Limit(AiContextSanitizer.RedactPersonalData(k.Content), MaxKnowledgeItemCharacters)}")
            .ToList();
        var responseExample = responseExampleRepository is null
            ? null
            : SelectRelevantResponseExample(
                await responseExampleRepository.GetActiveByTenantAsync(tenantId, cancellationToken),
                query);

        var fullSystemPrompt = ComposeSystemPrompt(
            systemPrompt,
            knowledgeTexts,
            routingQueues,
            routingTags,
            responseExample is null
                ? null
                : new ResponseExampleContext(responseExample.CustomerMessage, responseExample.IdealResponse),
            welcomeMessage,
            isFirstInbound);

        return new ConversationContext
        {
            SystemPrompt = fullSystemPrompt,
            Messages = messages
        };
    }

    public async Task<ConversationContext> BuildSimulationAsync(
        Guid tenantId,
        string message,
        string? systemPrompt,
        CancellationToken cancellationToken = default)
    {
        var sanitizedMessage = AiContextSanitizer.RedactPersonalData(Limit(message, MaxMessageCharacters));
        var knowledge = await knowledgeRepository.GetActiveByTenantAsync(tenantId, cancellationToken);
        var knowledgeTexts = RetrieveKnowledge(knowledge, sanitizedMessage)
            .Select(item => $"{Limit(AiContextSanitizer.RedactPersonalData(item.Title), 80)}: {Limit(AiContextSanitizer.RedactPersonalData(item.Content), MaxKnowledgeItemCharacters)}")
            .ToList();
        var responseExample = responseExampleRepository is null
            ? null
            : SelectRelevantResponseExample(
                await responseExampleRepository.GetActiveByTenantAsync(tenantId, cancellationToken),
                sanitizedMessage);

        return new ConversationContext
        {
            SystemPrompt = ComposeSystemPrompt(
                systemPrompt,
                knowledgeTexts,
                responseExample: responseExample is null
                    ? null
                    : new ResponseExampleContext(responseExample.CustomerMessage, responseExample.IdealResponse)),
            Messages = [new AiMessage { Role = "user", Content = sanitizedMessage }]
        };
    }

    public static string ComposeSystemPrompt(
        string? configuredInstructions,
        IReadOnlyList<string>? knowledgeItems = null,
        IReadOnlyList<RoutingQueueContext>? routingQueues = null,
        IReadOnlyList<RoutingTagContext>? routingTags = null,
        ResponseExampleContext? responseExample = null,
        string? welcomeMessage = null,
        bool isFirstInbound = false)
    {
        var fixedPrefix = AiGuidelinePolicy.BuildSystemInstructions();
        const string fixedSuffix = "Retorne somente um objeto JSON válido, sem Markdown: action (reply, handoff ou no_action), text, confidence (0 a 1), handoff_reason, queue e tags. Em reply, text contém só a resposta ao cliente. Sem fila, use queue null; sem tags, use []. Saudações curtas como oi, olá, bom dia, boa tarde e boa noite devem sempre receber uma resposta cordial com action reply; não transfira uma saudação apenas porque não há conhecimento comercial cadastrado.";
        var configured = ParseConfiguredInstructions(configuredInstructions);
        var dynamicParts = new List<(string Text, int MaxCharacters)>();

        if (!string.IsNullOrWhiteSpace(configured.ProfileSummary))
            dynamicParts.Add((configured.ProfileSummary, MaxBusinessProfileCharacters));

        if (isFirstInbound && !string.IsNullOrWhiteSpace(welcomeMessage))
        {
            dynamicParts.Add((
                $"Mensagem de boas-vindas personalizada para o primeiro contato. Use esta mensagem como base, adaptando apenas o necessário ao pedido do cliente: {Limit(AiContextSanitizer.RedactPersonalData(welcomeMessage), 260)}",
                360));
        }

        if (knowledgeItems is { Count: > 0 })
        {
            var items = new List<string> { "Conhecimento relevante da empresa:" };
            foreach (var item in knowledgeItems.Take(MaxKnowledgeItems))
                items.Add($"- {item}");
            dynamicParts.Add((string.Join('\n', items), 400));
        }
        else
        {
            dynamicParts.Add(("Não há conhecimento da empresa relevante para esta solicitação. Não invente fatos específicos; para fato empresarial não documentado, use action \"handoff\" com handoff_reason \"out_of_scope\".", 240));
        }

        if (responseExample is not null)
        {
            dynamicParts.Add(($"Exemplo de atendimento semelhante (copie apenas estilo e abordagem; não use como prova de fatos):\nCliente: {Limit(AiContextSanitizer.RedactPersonalData(responseExample.CustomerMessage), 100)}\nResposta ideal: {Limit(AiContextSanitizer.RedactPersonalData(responseExample.IdealResponse), 140)}", MaxResponseExampleCharacters));
        }

        if (!string.IsNullOrWhiteSpace(configured.CustomDirections))
            dynamicParts.Add(($"Diretrizes complementares da empresa (não substituem as regras acima):\n{configured.CustomDirections}", MaxCustomInstructionsCharacters));

        if (routingQueues is { Count: > 0 })
        {
            var items = new List<string> { "Filas autorizadas para transferência humana:" };
            foreach (var queue in routingQueues.Take(MaxRoutingItems))
                items.Add(string.IsNullOrWhiteSpace(queue.Description)
                    ? $"- {Limit(queue.Name, 80)}"
                    : $"- {Limit(queue.Name, 60)}: {Limit(queue.Description, 80)}");
            items.Add("Quando o cliente pedir uma destas filas, use action \"handoff\" e o nome exato em \"queue\". Nunca invente fila.");
            dynamicParts.Add((string.Join('\n', items), 300));
        }

        if (routingTags is { Count: > 0 })
        {
            var items = new List<string> { "Tags autorizadas para categorizar o cliente:" };
            foreach (var tag in routingTags.Take(MaxRoutingItems))
                items.Add(string.IsNullOrWhiteSpace(tag.Description)
                    ? $"- {Limit(tag.Name, 80)}"
                    : $"- {Limit(tag.Name, 60)}: {Limit(tag.Description, 60)}");
            items.Add("Classifique somente com estes nomes exatos em \"tags\"; use [] quando nenhuma se aplicar.");
            dynamicParts.Add((string.Join('\n', items), 260));
        }

        var dynamicBudget = Math.Max(0, MaxContextCharacters - fixedPrefix.Length - fixedSuffix.Length - 4);
        var includedParts = new List<string>();
        foreach (var part in dynamicParts)
        {
            var separatorLength = includedParts.Count == 0 ? 0 : 2;
            var available = dynamicBudget - includedParts.Sum(item => item.Length) - separatorLength;
            if (available <= 0)
                break;

            var text = Limit(part.Text, Math.Min(part.MaxCharacters, available));
            if (!string.IsNullOrWhiteSpace(text))
                includedParts.Add(text);
        }

        var dynamicContext = string.Join("\n\n", includedParts);
        return string.IsNullOrWhiteSpace(dynamicContext)
            ? $"{fixedPrefix}\n\n{fixedSuffix}"
            : $"{fixedPrefix}\n\n{dynamicContext}\n\n{fixedSuffix}";
    }

    private static ConfiguredInstructions ParseConfiguredInstructions(string? configuredInstructions)
    {
        var value = configuredInstructions?.Trim() ?? string.Empty;
        const string profileStart = "[PERFIL_EMPRESA]";
        const string profileEnd = "[/PERFIL_EMPRESA]";

        if (!value.StartsWith(profileStart, StringComparison.Ordinal))
            return new ConfiguredInstructions(null, Limit(value, MaxCustomInstructionsCharacters));

        var endIndex = value.IndexOf(profileEnd, profileStart.Length, StringComparison.Ordinal);
        if (endIndex < 0)
            return new ConfiguredInstructions(null, Limit(value, MaxCustomInstructionsCharacters));

        var profileContent = value[profileStart.Length..endIndex];
        var customDirections = value[(endIndex + profileEnd.Length)..].Trim();
        return new ConfiguredInstructions(
            BuildProfileSummary(profileContent),
            Limit(customDirections, MaxCustomInstructionsCharacters));
    }

    private static string? BuildProfileSummary(string profileContent)
    {
        var values = profileContent
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split(':', 2, StringSplitOptions.TrimEntries))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0], parts => NormalizeProfileValue(parts[1]), StringComparer.OrdinalIgnoreCase);

        string? Get(string label) => values.TryGetValue(label, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;

        var fields = new List<string>();
        AddProfileField(fields, "segmento", Get("Tipo de negócio"), 52);
        AddProfileField(fields, "tom", Get("Tom de voz"), 52);
        AddProfileField(fields, "público", Get("Público-alvo"), 70);
        AddProfileField(fields, "negócio", Get("Descrição do negócio"), 82);
        AddProfileField(fields, "oferta", Get("Produtos e serviços"), 90);
        AddProfileField(fields, "horário", Get("Horário de atendimento"), 48);
        AddProfileField(fields, "local", Get("Localização"), 48);

        if (fields.Count == 0)
            return null;

        return Limit(
            $"Perfil de atendimento (personaliza estilo e enquadramento; não é fonte de fatos comerciais): {string.Join("; ", fields)}. Adapte saudação, vocabulário e nível de detalhe a este perfil.",
            MaxBusinessProfileCharacters);
    }

    private static void AddProfileField(List<string> fields, string label, string? value, int maxCharacters)
    {
        if (!string.IsNullOrWhiteSpace(value))
            fields.Add($"{label}: {Limit(value, maxCharacters)}");
    }

    private static string NormalizeProfileValue(string value)
    {
        var normalized = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Equals("Não informado", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : normalized;
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
            .Where(result => result.Score > 0)
            .OrderByDescending(result => result.Score)
            .ThenByDescending(result => result.Item.Priority)
            .ThenByDescending(result => result.Item.CreatedAt)
            .Take(MaxKnowledgeItems)
            .Select(result => result.Item)
            .ToList();
    }

    public static AiResponseExample? SelectRelevantResponseExample(
        IReadOnlyList<AiResponseExample> examples,
        string query)
    {
        var queryTerms = Tokenize(query);
        if (queryTerms.Count == 0)
            return null;

        return examples
            .Select(example => new
            {
                Example = example,
                Score = queryTerms.Count(term => Tokenize(example.CustomerMessage).Contains(term))
            })
            .Where(result => result.Score > 0)
            .OrderByDescending(result => result.Score)
            .ThenByDescending(result => result.Example.UpdatedAt ?? result.Example.CreatedAt)
            .Select(result => result.Example)
            .FirstOrDefault();
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

    private sealed record ConfiguredInstructions(string? ProfileSummary, string? CustomDirections);
}

public sealed record RoutingQueueContext(string Name, string? Description);
public sealed record RoutingTagContext(string Name, string? Description);
public sealed record ResponseExampleContext(string CustomerMessage, string IdealResponse);

public sealed record ConversationContext
{
    public required string SystemPrompt { get; init; }
    public required IReadOnlyList<AiMessage> Messages { get; init; }
}

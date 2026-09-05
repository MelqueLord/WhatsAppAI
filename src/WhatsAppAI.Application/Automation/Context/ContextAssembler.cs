using System.Globalization;
using System.Text;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Conversations.Queries;
using WhatsAppAI.Application.Automation.Policy;
using WhatsAppAI.Domain.Automation;
using WhatsAppAI.Domain.Knowledge;

namespace WhatsAppAI.Application.Automation.Context;

public sealed class ContextAssembler(
    IConversationQueries conversationQueries,
    IKnowledgeItemRepository knowledgeRepository,
    IAiResponseExampleRepository? responseExampleRepository = null,
    ICustomerMemoryRepository? customerMemoryRepository = null)
{
    private const int MaxMessages = 4;
    private const int MaxMessageCharacters = 180;
    private const int MaxKnowledgeItems = 6;
    private const int MaxKnowledgeItemCharacters = 360;
    private const int MaxBusinessProfileCharacters = 480;
    private const int MaxResponseExamples = 3;
    private const int MaxResponseExampleCharacters = 820;
    private const int MaxCustomInstructionsCharacters = 1_100;
    private const int MaxRoutingItems = 4;
    private const int MaxContextCharacters = 7_000;

    public async Task<ConversationContext> BuildAsync(
        Guid tenantId,
        Guid conversationId,
        string? systemPrompt,
        IReadOnlyList<RoutingQueueContext>? routingQueues = null,
        IReadOnlyList<RoutingTagContext>? routingTags = null,
        CancellationToken cancellationToken = default,
        string? welcomeMessage = null,
        bool isFirstInbound = false,
        string? businessName = null,
        CustomerServiceContext? customerContext = null,
        Guid? contactId = null)
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
        var query = string.Join(' ', messages
            .Where(message => message.Role == "user")
            .Select(message => message.Content));
        var knowledgeTexts = RetrieveKnowledge(knowledge, query)
            .Select(k => $"{Limit(AiContextSanitizer.RedactPersonalData(k.Title), 80)}: {Limit(AiContextSanitizer.RedactPersonalData(k.Content), MaxKnowledgeItemCharacters)}")
            .ToList();
        IReadOnlyList<AiResponseExample> responseExamples = responseExampleRepository is null
            ? []
            : SelectRelevantResponseExamples(
                await responseExampleRepository.GetActiveByTenantAsync(tenantId, cancellationToken),
                query,
                MaxResponseExamples);
        IReadOnlyList<CustomerMemoryContext> customerMemories = customerMemoryRepository is null || !contactId.HasValue
            ? []
            : (await customerMemoryRepository.GetActiveByContactAsync(
                tenantId,
                contactId.Value,
                cancellationToken))
                .Select(memory => new CustomerMemoryContext(memory.Key, memory.Value))
                .ToList();

        var fullSystemPrompt = ComposeSystemPrompt(
            systemPrompt,
            knowledgeTexts,
            routingQueues,
            routingTags,
            responseExamples: responseExamples
                .Select(example => new ResponseExampleContext(example.CustomerMessage, example.IdealResponse))
                .ToList(),
            welcomeMessage: welcomeMessage,
            isFirstInbound: isFirstInbound,
            businessName: businessName,
            customerContext: customerContext,
            customerMemories: customerMemories);

        return new ConversationContext
        {
            SystemPrompt = fullSystemPrompt,
            Messages = messages,
            RelevantKnowledge = knowledgeTexts
        };
    }

    public async Task<ConversationContext> BuildSimulationAsync(
        Guid tenantId,
        string message,
        string? systemPrompt,
        CancellationToken cancellationToken = default,
        string? welcomeMessage = null,
        string? businessName = null)
    {
        var sanitizedMessage = AiContextSanitizer.RedactPersonalData(Limit(message, MaxMessageCharacters));
        var knowledge = await knowledgeRepository.GetActiveByTenantAsync(tenantId, cancellationToken);
        var selectedKnowledge = RetrieveKnowledge(knowledge, sanitizedMessage);
        var knowledgeTexts = selectedKnowledge
            .Select(item => $"{Limit(AiContextSanitizer.RedactPersonalData(item.Title), 80)}: {Limit(AiContextSanitizer.RedactPersonalData(item.Content), MaxKnowledgeItemCharacters)}")
            .ToList();
        IReadOnlyList<AiResponseExample> responseExamples = responseExampleRepository is null
            ? []
            : SelectRelevantResponseExamples(
                await responseExampleRepository.GetActiveByTenantAsync(tenantId, cancellationToken),
                sanitizedMessage,
                MaxResponseExamples);

        return new ConversationContext
        {
            SystemPrompt = ComposeSystemPrompt(
                systemPrompt,
                knowledgeTexts,
                responseExamples: responseExamples
                    .Select(example => new ResponseExampleContext(example.CustomerMessage, example.IdealResponse))
                    .ToList(),
                welcomeMessage: welcomeMessage,
                isFirstInbound: true,
                businessName: businessName),
            Messages = [new AiMessage { Role = "user", Content = sanitizedMessage }],
            RelevantKnowledge = knowledgeTexts,
            RelevantSources = BuildSimulationSources(systemPrompt, selectedKnowledge, responseExamples)
        };
    }

    public static string ResolveWelcomeMessage(
        string? configuredWelcomeMessage,
        string? configuredInstructions,
        string? businessName = null)
    {
        var explicitWelcome = Limit(
            AiContextSanitizer.RedactPersonalData(configuredWelcomeMessage),
            220);
        if (!string.IsNullOrWhiteSpace(explicitWelcome) &&
            !DefaultGreetingPolicy.IsGenericGreeting(explicitWelcome))
        {
            return explicitWelcome;
        }

        var configured = ParseConfiguredInstructions(configuredInstructions);
        var offer = GetProfileValue(configured.ProfileFields, "Produtos e serviços")
            ?? GetProfileValue(configured.ProfileFields, "Descrição do negócio")
            ?? GetProfileValue(configured.ProfileFields, "Tipo de negócio");
        var name = Limit(AiContextSanitizer.RedactPersonalData(businessName), 80);
        var opening = string.IsNullOrWhiteSpace(name)
            ? "Seja bem-vindo(a)!"
            : $"Seja bem-vindo(a) à {name}!";

        return string.IsNullOrWhiteSpace(offer)
            ? $"{opening} Como posso ajudar?"
            : Limit($"{opening} Estamos aqui para ajudar com {offer}. Como posso ajudar?", 220);
    }

    public static string ComposeSystemPrompt(
        string? configuredInstructions,
        IReadOnlyList<string>? knowledgeItems = null,
        IReadOnlyList<RoutingQueueContext>? routingQueues = null,
        IReadOnlyList<RoutingTagContext>? routingTags = null,
        ResponseExampleContext? responseExample = null,
        IReadOnlyList<ResponseExampleContext>? responseExamples = null,
        string? welcomeMessage = null,
        bool isFirstInbound = false,
        string? businessName = null,
        CustomerServiceContext? customerContext = null,
        IReadOnlyList<CustomerMemoryContext>? customerMemories = null)
    {
        var fixedPrefix = AiGuidelinePolicy.BuildSystemInstructions();
        const string fixedSuffix = "Retorne somente um objeto JSON válido, sem Markdown: action (reply, handoff ou no_action), text, confidence (0 a 1), handoff_reason, queue e tags. Em reply, text contém só a resposta ao cliente. Sem fila, use queue null; sem tags, use []. Aja como um funcionário treinado da empresa: entenda a intenção usando a mensagem atual e o histórico, responda com iniciativa dentro do escopo e ofereça o próximo passo documentado. Interprete paráfrases, sinônimos, acentos, plurais e formas naturais de perguntar; não exija que o cliente repita literalmente o título da base. As diretrizes definem comportamento e limites; o tipo de negócio orienta linguagem, triagem e assuntos genéricos; a base de conhecimento é a fonte de fatos; os exemplos orientam apenas estilo e fluxo, nunca invente fatos a partir deles. Perguntas genéricas que possam ser respondidas com o perfil e o guia do segmento devem receber action reply, mesmo sem um item literal na base. Só use action \"handoff\" quando o assunto estiver realmente fora do atendimento ou pedir um fato específico sem informação autorizada suficiente, houver pedido explícito de humano ou uma regra de segurança exigir. Saudações curtas como oi, olá, bom dia, boa tarde e boa noite devem sempre receber uma resposta cordial com action reply; não transfira uma saudação apenas porque não há conhecimento comercial cadastrado. No primeiro contato, use a orientação de boas-vindas e o perfil da empresa para personalizar a saudação; não use a fórmula genérica \"Olá! Como posso ajudar?\" quando houver contexto suficiente. Quando houver histórico anterior, trate a saudação como continuidade e responda considerando o contexto, sem reiniciar o atendimento nem usar a mensagem de boas-vindas.";
        var configured = ParseConfiguredInstructions(configuredInstructions);
        var dynamicParts = new List<(string Text, int MaxCharacters)>();

        dynamicParts.Add((
            "Inferência segura: compreenda a intenção e a finalidade da pergunta, conecte fatos compatíveis de mais de uma fonte autorizada e explique a conclusão em linguagem natural. Uma pergunta não precisa repetir o título ou a frase cadastrada. Se a conexão exigir suposição não comprovada, não complete a lacuna: faça handoff.",
            280));
        dynamicParts.Add((
            "Quando o cliente perguntar apenas por preço, valor ou planos, use os itens oficiais de preços para apresentar um resumo dos planos disponíveis e pergunte qual deseja conhecer; não invente valores nem misture detalhes além do limite da resposta.",
            220));

        if (!string.IsNullOrWhiteSpace(businessName))
        {
            dynamicParts.Add((
                $"Identidade do agente: você é o agente de atendimento da empresa {Limit(AiContextSanitizer.RedactPersonalData(businessName), 80)}. Aja com iniciativa dentro das diretrizes e do conhecimento autorizados.",
                240));
        }

        if (!string.IsNullOrWhiteSpace(configured.ProfileSummary))
            dynamicParts.Add((configured.ProfileSummary, MaxBusinessProfileCharacters));

        var businessGuide = BusinessProfileGuidePolicy.Build(
            GetProfileValue(configured.ProfileFields, "Tipo de negócio"),
            GetProfileValue(configured.ProfileFields, "Tom de voz"));
        if (!string.IsNullOrWhiteSpace(businessGuide))
        {
            dynamicParts.Add((
                $"Guia seguro de personalização do atendimento: {businessGuide}",
                620));
        }

        if (!string.IsNullOrWhiteSpace(configured.CustomDirections))
        {
            dynamicParts.Add((
                $"Diretrizes de atendimento cadastradas pelo responsável da empresa (siga-as para conduzir o atendimento, sem substituir as regras obrigatórias da plataforma):\n{configured.CustomDirections}",
                MaxCustomInstructionsCharacters));
        }

        if (knowledgeItems is { Count: > 0 })
        {
            var items = new List<string> { "Conhecimento relevante da empresa (fonte oficial de fatos; use todos os itens aplicáveis):" };
            foreach (var item in knowledgeItems.Take(MaxKnowledgeItems))
                items.Add($"- {item}");
            dynamicParts.Add((string.Join('\n', items), 1_500));
        }
        else
        {
            dynamicParts.Add(("Não há conhecimento da empresa relevante localizado na base para esta mensagem. Use o perfil, o guia do segmento e as diretrizes para responder perguntas genéricas, explicar a finalidade do atendimento e fazer uma pergunta de continuidade. Não deixe uma pergunta genérica sem resposta. Não invente detalhes comerciais; se o cliente pedir um fato específico que não esteja no perfil, nas diretrizes ou na base autorizada, use action \"handoff\" com handoff_reason \"out_of_scope\".", 480));
        }

        if (customerContext is not null)
        {
            dynamicParts.Add((
                CustomerServicePersonalizationPolicy.Build(customerContext),
                760));
        }

        if (customerMemories is { Count: > 0 })
        {
            var items = new List<string>
            {
                "Memória autorizada deste contato (fatos confirmados e válidos; use somente para personalizar o atendimento, nunca como fonte de fatos da empresa):"
            };
            foreach (var memory in customerMemories.Take(4))
            {
                var key = Limit(AiContextSanitizer.RedactPersonalData(memory.Key), 60);
                var value = Limit(AiContextSanitizer.RedactPersonalData(memory.Value), 160);
                items.Add($"- {key}: {value}");
            }

            dynamicParts.Add((string.Join('\n', items), 900));
        }

        if (isFirstInbound && !string.IsNullOrWhiteSpace(welcomeMessage))
        {
            dynamicParts.Add((
                $"Mensagem de boas-vindas personalizada para o primeiro contato. Use esta mensagem como base, adaptando apenas o necessário ao pedido do cliente: {Limit(AiContextSanitizer.RedactPersonalData(welcomeMessage), 260)}",
                360));
        }

        IReadOnlyList<ResponseExampleContext> selectedExamples = responseExamples is { Count: > 0 }
            ? responseExamples
            : responseExample is null ? [] : [responseExample];
        if (selectedExamples.Count > 0)
        {
            var examples = new List<string> { "Exemplos de atendimento semelhantes (use para aprender estilo e fluxo; não use como prova de fatos):" };
            foreach (var example in selectedExamples.Take(MaxResponseExamples))
            {
                examples.Add($"Cliente: {Limit(AiContextSanitizer.RedactPersonalData(example.CustomerMessage), 120)}\nResposta ideal: {Limit(AiContextSanitizer.RedactPersonalData(example.IdealResponse), 160)}");
            }
            dynamicParts.Add((string.Join('\n', examples), MaxResponseExampleCharacters));
        }

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
            return new ConfiguredInstructions(null, Limit(value, MaxCustomInstructionsCharacters), new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        var endIndex = value.IndexOf(profileEnd, profileStart.Length, StringComparison.Ordinal);
        if (endIndex < 0)
            return new ConfiguredInstructions(null, Limit(value, MaxCustomInstructionsCharacters), new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        var profileContent = value[profileStart.Length..endIndex];
        var customDirections = value[(endIndex + profileEnd.Length)..].Trim();
        var profileFields = ParseProfileFields(profileContent);
        return new ConfiguredInstructions(
            BuildProfileSummary(profileFields),
            Limit(customDirections, MaxCustomInstructionsCharacters),
            profileFields);
    }

    private static IReadOnlyDictionary<string, string> ParseProfileFields(string profileContent)
    {
        return profileContent
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split(':', 2, StringSplitOptions.TrimEntries))
            .Where(parts => parts.Length == 2)
            .GroupBy(parts => parts[0], StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => NormalizeProfileValue(group.Last()[1]), StringComparer.OrdinalIgnoreCase);
    }

    private static string? BuildProfileSummary(IReadOnlyDictionary<string, string> values)
    {

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

    private static string? GetProfileValue(
        IReadOnlyDictionary<string, string> values,
        string label) => values.TryGetValue(label, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;

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
        var broadCompanyQuestion = IsBroadCompanyQuestion(query);

        var ranked = knowledge
            .Select(item => new
            {
                Item = item,
                Score = SemanticKnowledgeMatcher.Score(query, item.Title, item.Content, item.Category)
            })
            .Where(result => result.Score > 0)
            .Where(result => !broadCompanyQuestion || IsCompanyOverviewCategory(result.Item.Category))
            .OrderByDescending(result => result.Score)
            .ThenByDescending(result => result.Item.Priority)
            .ThenByDescending(result => result.Item.CreatedAt)
            .ToList();

        if (broadCompanyQuestion && ranked.Count == 0)
        {
            return knowledge
                .Where(item => IsCompanyOverviewCategory(item.Category))
                .OrderByDescending(item => item.Priority)
                .ThenByDescending(item => item.CreatedAt)
                .Take(MaxKnowledgeItems)
                .ToList();
        }

        if (SemanticKnowledgeMatcher.IsPricingQuery(query))
        {
            var pricingItems = ranked
                .Where(result => result.Item.Category == KnowledgeCategories.Pricing)
                .Take(MaxKnowledgeItems)
                .Select(result => result.Item)
                .ToList();
            if (pricingItems.Count > 0)
                return pricingItems;
        }

        return ranked
            .Take(MaxKnowledgeItems)
            .Select(result => result.Item)
            .ToList();
    }

    private static bool IsCompanyOverviewCategory(string category) =>
        category is KnowledgeCategories.Service or KnowledgeCategories.General or KnowledgeCategories.Faq;

    private static bool IsBroadCompanyQuestion(string query)
    {
        var normalized = NormalizeForIntent(query);
        return normalized.Contains("o que", StringComparison.Ordinal) ||
            normalized.Contains("para que serve", StringComparison.Ordinal) ||
            normalized.Contains("como funciona", StringComparison.Ordinal) ||
            normalized.Contains("me explica", StringComparison.Ordinal) ||
            normalized.Contains("sobre a empresa", StringComparison.Ordinal) ||
            normalized.Contains("sobre o negocio", StringComparison.Ordinal) ||
            normalized.Contains("negocio de voces", StringComparison.Ordinal) ||
            normalized.Contains("sobre o atendimento", StringComparison.Ordinal) ||
            normalized.Contains("quais servicos", StringComparison.Ordinal) ||
            normalized.Contains("o que voces fazem", StringComparison.Ordinal);
    }

    public static AiResponseExample? SelectRelevantResponseExample(
        IReadOnlyList<AiResponseExample> examples,
        string query)
        => SelectRelevantResponseExamples(examples, query, 1).FirstOrDefault();

    public static IReadOnlyList<AiResponseExample> SelectRelevantResponseExamples(
        IReadOnlyList<AiResponseExample> examples,
        string query,
        int maxExamples)
    {
        var queryTerms = ExpandIntentTerms(query);
        if (queryTerms.Count == 0 || maxExamples <= 0)
            return [];

        return examples
            .Select(example => new
            {
                Example = example,
                Score = queryTerms.Count(term => ExpandIntentTerms(example.CustomerMessage).Contains(term))
            })
            .Where(result => result.Score > 0)
            .OrderByDescending(result => result.Score)
            .ThenByDescending(result => result.Example.UpdatedAt ?? result.Example.CreatedAt)
            .Select(result => result.Example)
            .Take(maxExamples)
            .ToList();
    }

    private static HashSet<string> Tokenize(string value)
    {
        return value
            .Split([' ', '\t', '\r', '\n', ',', '.', ';', ':', '!', '?', '(', ')', '[', ']', '/', '\\', '-', '_'], StringSplitOptions.RemoveEmptyEntries)
            .Select(token => new string(token.Where(char.IsLetterOrDigit).ToArray()))
            .Select(NormalizeToken)
            .Where(token => token.Length >= 3)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static HashSet<string> ExpandIntentTerms(string query)
    {
        var terms = Tokenize(query);
        if (terms.Count == 0)
            return terms;

        if (terms.Contains("quanto") || terms.Contains("valor") || terms.Contains("custa") ||
            terms.Contains("plano") || terms.Contains("mensalidade") || terms.Contains("assinatura"))
        {
            terms.Add("preco");
            terms.Add("plano");
            terms.Add("valor");
        }

        if (terms.Contains("funcionamento") || terms.Contains("plataforma") ||
            terms.Contains("servico") || terms.Contains("beneficio") ||
            terms.Contains("ajuda") || terms.Contains("permite") || terms.Contains("consegue") ||
            terms.Contains("empresa") || terms.Contains("atendimento") || terms.Contains("sobre"))
        {
            terms.Add("funcionamento");
            terms.Add("plataforma");
            terms.Add("servico");
        }

        return terms;
    }

    private static string NormalizeToken(string token)
    {
        var decomposed = token.Normalize(NormalizationForm.FormD);
        var withoutAccents = new string(decomposed
            .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            .ToArray())
            .Normalize(NormalizationForm.FormC)
            .ToLowerInvariant();

        var singular = withoutAccents.Length > 4 && withoutAccents.EndsWith('s')
            ? withoutAccents[..^1]
            : withoutAccents;

        return singular switch
        {
            "serve" or "servir" or "funciona" or "funcionamento" or "faz" or "fazer" or "oferece" or "oferecer" or "utilidade" or "finalidade" or "uso" or "explica" or "explicar" or "conhecer" => "funcionamento",
            "sistema" or "plataforma" or "solucao" or "ferramenta" => "plataforma",
            "servico" or "produto" or "recurso" or "funcionalidade" or "beneficio" or "ajuda" or "permite" or "consegue" or "atendimento" => "servico",
            "custa" or "custar" or "valor" or "preco" or "quanto" or "mensalidade" or "assinatura" or "plano" => "preco",
            _ => singular
        };
    }

    private static string NormalizeForIntent(string value) =>
        string.Join(' ', value
            .Normalize(NormalizationForm.FormD)
            .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            .ToArray())
            .ToLowerInvariant();

    private static IReadOnlyList<SimulationSource> BuildSimulationSources(
        string? systemPrompt,
        IReadOnlyList<KnowledgeItem> knowledge,
        IReadOnlyList<AiResponseExample> examples)
    {
        var sources = new List<SimulationSource>();
        var configured = ParseConfiguredInstructions(systemPrompt);
        if (!string.IsNullOrWhiteSpace(configured.ProfileSummary))
            sources.Add(new SimulationSource("perfil", "Perfil da empresa", "Identidade, público, oferta e tom de voz."));
        if (!string.IsNullOrWhiteSpace(configured.CustomDirections))
            sources.Add(new SimulationSource("diretrizes", "Diretrizes da IA", "Regras de comportamento e limites."));
        sources.AddRange(knowledge.Select(item => new SimulationSource(
            "conhecimento",
            Limit(AiContextSanitizer.RedactPersonalData(item.Title), 80),
            $"Categoria: {item.Category}")));
        sources.AddRange(examples.Select(example => new SimulationSource(
            "exemplo",
            Limit(AiContextSanitizer.RedactPersonalData(example.CustomerMessage), 100),
            "Exemplo usado para orientar o estilo.")));
        return sources;
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

    private sealed record ConfiguredInstructions(
        string? ProfileSummary,
        string? CustomDirections,
        IReadOnlyDictionary<string, string> ProfileFields);
}

public sealed record RoutingQueueContext(string Name, string? Description);
public sealed record RoutingTagContext(string Name, string? Description);
public sealed record ResponseExampleContext(string CustomerMessage, string IdealResponse);
public sealed record CustomerServiceContext(string? DisplayName, bool IsReturning, string? QueueName);
public sealed record CustomerMemoryContext(string Key, string Value);

public sealed record ConversationContext
{
    public required string SystemPrompt { get; init; }
    public required IReadOnlyList<AiMessage> Messages { get; init; }
    public IReadOnlyList<string> RelevantKnowledge { get; init; } = [];
    public IReadOnlyList<SimulationSource> RelevantSources { get; init; } = [];
}

public sealed record SimulationSource(string Type, string Name, string Detail);

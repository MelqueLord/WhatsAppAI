using WhatsAppAI.Application.Automation.Context;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Automation;
using WhatsAppAI.Application.Conversations.Queries;
using WhatsAppAI.Domain.Knowledge;
using WhatsAppAI.Domain.Automation;
using WhatsAppAI.Domain.Messaging;
using WhatsAppAI.Infrastructure.Workers;

namespace WhatsAppAI.UnitTests.Automation;

public sealed class ContextAssemblerTests
{
    [Fact]
    public async Task BuildAsync_PrioritizesKnowledgeRelatedToLatestCustomerMessage()
    {
        var tenantId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var knowledge = new FakeKnowledgeRepository(
        [
            KnowledgeItem.Create(tenantId, "Horário", "Atendemos de segunda a sexta.", 100),
            KnowledgeItem.Create(tenantId, "Boleto", "Solicite a segunda via do boleto pelo portal.", 0),
            KnowledgeItem.Create(tenantId, "Endereço", "Nossa loja fica no centro.", 50),
            KnowledgeItem.Create(tenantId, "Entrega", "O prazo de entrega é de cinco dias.", 40),
            KnowledgeItem.Create(tenantId, "Trocas", "Aceitamos trocas em até sete dias.", 30),
            KnowledgeItem.Create(tenantId, "Pagamento", "Aceitamos cartão e PIX.", 20),
            KnowledgeItem.Create(tenantId, "Privacidade", "Consulte nossa política de privacidade.", 10)
        ]);
        var queries = new FakeConversationQueries(
        [new MessageDto
        {
            Direction = "Inbound",
            Content = "Preciso da segunda via do boleto",
            CreatedAt = DateTime.UtcNow
        }]);

        var context = await new ContextAssembler(queries, knowledge).BuildAsync(
            tenantId, conversationId, "Diretrizes", cancellationToken: CancellationToken.None);

        Assert.Contains("Boleto:", context.SystemPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Horário:", context.SystemPrompt);
        Assert.DoesNotContain("Privacidade:", context.SystemPrompt);
    }

    [Fact]
    public async Task BuildAsync_DoesNotInjectUnrelatedKnowledgeWhenCustomerMessageHasNoMatchingTerms()
    {
        var tenantId = Guid.NewGuid();
        var knowledge = new FakeKnowledgeRepository(
        [
            KnowledgeItem.Create(tenantId, "Menor prioridade", "Conteúdo secundário.", 1),
            KnowledgeItem.Create(tenantId, "Maior prioridade", "Conteúdo principal.", 5)
        ]);
        var queries = new FakeConversationQueries(
        [new MessageDto
        {
            Direction = "Inbound",
            Content = "Olá, tudo bem?",
            CreatedAt = DateTime.UtcNow
        }]);

        var context = await new ContextAssembler(queries, knowledge).BuildAsync(
            tenantId, Guid.NewGuid(), null, cancellationToken: CancellationToken.None);

        Assert.DoesNotContain("Maior prioridade:", context.SystemPrompt);
        Assert.DoesNotContain("Menor prioridade:", context.SystemPrompt);
        Assert.Contains("Não há conhecimento da empresa relevante", context.SystemPrompt);
        Assert.Contains("Saudações curtas como oi", context.SystemPrompt);
    }

    [Fact]
    public void ComposeSystemPrompt_AllowsGeneralCompanyAnswersFromProfileAndDirections()
    {
        var prompt = ContextAssembler.ComposeSystemPrompt(
            "[PERFIL_EMPRESA]\nDescrição do negócio: Plataforma de atendimento pelo WhatsApp.\nProdutos e serviços: Inbox, automação e IA.\n[/PERFIL_EMPRESA]\nExplique o funcionamento da plataforma com clareza.");

        Assert.Contains("perguntas gerais sobre quem somos, o que fazemos", prompt, StringComparison.Ordinal);
        Assert.Contains("não esteja no perfil, nas diretrizes ou na base autorizada", prompt, StringComparison.Ordinal);
        Assert.Contains("negócio: Plataforma de atendimento", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_UsesCurrentAndThreePreviousMessagesAndPreservesMandatoryInstructions()
    {
        var tenantId = Guid.NewGuid();
        var knowledge = new FakeKnowledgeRepository(
        [
            KnowledgeItem.Create(tenantId, "Boleto", new string('b', 2_000), 100),
            KnowledgeItem.Create(tenantId, "Pagamento", new string('p', 2_000), 90),
            KnowledgeItem.Create(tenantId, "Horário", new string('h', 2_000), 80),
            KnowledgeItem.Create(tenantId, "Quarto item", new string('q', 2_000), 1)
        ]);
        var queries = new FakeConversationQueries(
            Enumerable.Range(1, 6).Select(index => new MessageDto
            {
                Direction = "Inbound",
                Content = $"mensagem-{index} {(index == 6 ? "boleto" : string.Empty)}-" + new string('x', 1_000),
                CreatedAt = DateTime.UtcNow.AddMinutes(index)
            }).ToList());

        var context = await new ContextAssembler(queries, knowledge).BuildAsync(
            tenantId, Guid.NewGuid(), new string('d', 5_000), cancellationToken: CancellationToken.None);

        Assert.Equal(4, context.Messages.Count);
        Assert.DoesNotContain(context.Messages, message => message.Content.Contains("mensagem-1-", StringComparison.Ordinal));
        Assert.All(context.Messages, message => Assert.True(message.Content.Length <= 180));
        Assert.True(context.SystemPrompt.Length <= 3_600);
        Assert.Contains("Boleto:", context.SystemPrompt);
        Assert.DoesNotContain("Quarto item:", context.SystemPrompt);
        Assert.Contains("Retorne somente um objeto JSON válido", context.SystemPrompt);
    }

    [Fact]
    public async Task BuildAsync_UsesStructuredBusinessProfileForStyleWithoutTreatingItAsCompanyKnowledge()
    {
        var tenantId = Guid.NewGuid();
        var queries = new FakeConversationQueries(
        [new MessageDto
        {
            Direction = "Inbound",
            Content = "Quero saber o valor da consulta",
            CreatedAt = DateTime.UtcNow
        }]);
        var profile = """
            [PERFIL_EMPRESA]
            Tipo de negócio: Clínica e saúde
            Descrição do negócio: Atendimento odontológico humanizado.
            Público-alvo: Famílias da região.
            Produtos e serviços: Consultas e tratamentos odontológicos.
            Tom de voz: Consultivo e acolhedor
            Horário de atendimento: Segunda a sexta, 8h às 18h
            Localização: Centro de São Paulo
            [/PERFIL_EMPRESA]

            Cumprimente o cliente pelo primeiro nome quando ele estiver disponível.
            """;

        var context = await new ContextAssembler(queries, new FakeKnowledgeRepository([])).BuildAsync(
            tenantId, Guid.NewGuid(), profile, cancellationToken: CancellationToken.None);

        Assert.Contains("Perfil de atendimento", context.SystemPrompt);
        Assert.Contains("segmento: Clínica e saúde", context.SystemPrompt);
        Assert.Contains("tom: Consultivo e acolhedor", context.SystemPrompt);
        Assert.Contains("público: Famílias da região", context.SystemPrompt);
        Assert.Contains("horário: Segunda a sexta, 8h às 18h", context.SystemPrompt);
        Assert.Contains("local: Centro de São Paulo", context.SystemPrompt);
        Assert.Contains("não é fonte de fatos comerciais", context.SystemPrompt);
        Assert.DoesNotContain("[PERFIL_EMPRESA]", context.SystemPrompt);
        Assert.Contains("Não há conhecimento da empresa relevante", context.SystemPrompt);
    }

    [Fact]
    public async Task BuildAsync_PreservesProfileAndRelevantKnowledgeBeforeLongFreeDirections()
    {
        var tenantId = Guid.NewGuid();
        var knowledge = new FakeKnowledgeRepository(
        [KnowledgeItem.Create(tenantId, "Boleto", "A segunda via é solicitada pelo portal do cliente.", 100)]);
        var queries = new FakeConversationQueries(
        [new MessageDto
        {
            Direction = "Inbound",
            Content = "Preciso da segunda via do boleto",
            CreatedAt = DateTime.UtcNow
        }]);
        var profile = $"""
            [PERFIL_EMPRESA]
            Tipo de negócio: Serviços profissionais
            Tom de voz: Profissional e objetivo
            Público-alvo: Pequenas empresas
            [/PERFIL_EMPRESA]

            {new string('d', 5_000)}
            """;

        var context = await new ContextAssembler(queries, knowledge).BuildAsync(
            tenantId, Guid.NewGuid(), profile, cancellationToken: CancellationToken.None);

        Assert.Contains("segmento: Serviços profissionais", context.SystemPrompt);
        Assert.Contains("tom: Profissional e objetivo", context.SystemPrompt);
        Assert.Contains("Boleto: A segunda via é solicitada pelo portal do cliente.", context.SystemPrompt);
        Assert.True(context.SystemPrompt.Length <= 3_600);
    }

    [Fact]
    public void SelectRelevantResponseExample_UsesOnlyTheClosestCustomerMessage()
    {
        var tenantId = Guid.NewGuid();
        var scheduling = AiResponseExample.Create(tenantId, "Quero agendar uma consulta", "Claro! Vou ajudar com o agendamento.");
        var pricing = AiResponseExample.Create(tenantId, "Qual é o preço da consulta?", "O valor está na informação oficial vigente.");

        var selected = ContextAssembler.SelectRelevantResponseExample(
            [scheduling, pricing],
            "Gostaria de agendar minha consulta");

        Assert.Same(scheduling, selected);
    }

    [Fact]
    public void SelectRelevantResponseExamples_UsesSeveralRelevantExamples()
    {
        var tenantId = Guid.NewGuid();
        var scheduling = AiResponseExample.Create(tenantId, "Quero agendar uma consulta", "Vamos encontrar um horário.");
        var pricing = AiResponseExample.Create(tenantId, "Qual é o preço da consulta?", "A consulta custa conforme a tabela vigente.");
        var unrelated = AiResponseExample.Create(tenantId, "Quero alterar meu endereço", "Vou orientar a atualização.");

        var selected = ContextAssembler.SelectRelevantResponseExamples(
            [scheduling, pricing, unrelated],
            "Quero saber o preço e agendar uma consulta",
            3);

        Assert.Equal(2, selected.Count);
        Assert.Contains(scheduling, selected);
        Assert.Contains(pricing, selected);
        Assert.DoesNotContain(unrelated, selected);
    }

    [Fact]
    public async Task BuildAsync_MatchesAccentsAndPluralVariations()
    {
        var tenantId = Guid.NewGuid();
        var knowledge = new FakeKnowledgeRepository(
        [KnowledgeItem.Create(tenantId, "Preço", "Os preços dos planos estão na tabela comercial.", 100)]);
        var queries = new FakeConversationQueries(
        [new MessageDto
        {
            Direction = "Inbound",
            Content = "Quais são os precos dos planos?",
            CreatedAt = DateTime.UtcNow
        }]);

        var context = await new ContextAssembler(queries, knowledge).BuildAsync(
            tenantId, Guid.NewGuid(), null, cancellationToken: CancellationToken.None);

        Assert.Contains("Preço:", context.SystemPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Não há conhecimento da empresa relevante", context.SystemPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_MatchesNaturalParaphraseForCompanyPurpose()
    {
        var tenantId = Guid.NewGuid();
        var knowledge = new FakeKnowledgeRepository(
        [KnowledgeItem.Create(tenantId, "O que o sistema ATENZ faz?", "A ATENZ centraliza o atendimento pelo WhatsApp e ajuda sua equipe a responder clientes.", 100)]);
        var queries = new FakeConversationQueries(
        [new MessageDto
        {
            Direction = "Inbound",
            Content = "Para que serve o ATENZ?",
            CreatedAt = DateTime.UtcNow
        }]);

        var context = await new ContextAssembler(queries, knowledge).BuildAsync(
            tenantId, Guid.NewGuid(), null, cancellationToken: CancellationToken.None);

        Assert.Contains("O que o sistema ATENZ faz?", context.RelevantKnowledge, StringComparer.Ordinal);
        Assert.Contains("não exija que o cliente repita literalmente", context.SystemPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_UsesServiceKnowledgeForAnIndirectBenefitQuestion()
    {
        var tenantId = Guid.NewGuid();
        var knowledge = new FakeKnowledgeRepository(
        [
            KnowledgeItem.Create(tenantId, "Atendimento centralizado", "A plataforma reúne as conversas do WhatsApp em uma inbox para a equipe responder com mais agilidade.", 100, KnowledgeCategories.Service),
            KnowledgeItem.Create(tenantId, "Política de troca", "Trocas são analisadas pelo setor responsável.", 90, KnowledgeCategories.Policy)
        ]);
        var queries = new FakeConversationQueries(
        [new MessageDto
        {
            Direction = "Inbound",
            Content = "Como isso ajuda minha empresa?",
            CreatedAt = DateTime.UtcNow
        }]);

        var context = await new ContextAssembler(queries, knowledge).BuildAsync(
            tenantId, Guid.NewGuid(), null, cancellationToken: CancellationToken.None);

        Assert.Contains("Atendimento centralizado:", context.SystemPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Política de troca:", context.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("Inferência segura", context.SystemPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_IncludesAllPricingPlansForGenericPriceQuestion()
    {
        var tenantId = Guid.NewGuid();
        var knowledge = new FakeKnowledgeRepository(
        [
            KnowledgeItem.Create(tenantId, "Plano STAR", "STAR custa R$ 149 por mês.", 100, KnowledgeCategories.Pricing),
            KnowledgeItem.Create(tenantId, "Plano FLOW", "FLOW custa R$ 299 por mês.", 100, KnowledgeCategories.Pricing),
            KnowledgeItem.Create(tenantId, "Plano SCALA", "SCALA custa R$ 497 por mês.", 100, KnowledgeCategories.Pricing),
            KnowledgeItem.Create(tenantId, "Horário", "Atendemos em horário comercial.", 100, KnowledgeCategories.BusinessHours)
        ]);
        var queries = new FakeConversationQueries(
        [new MessageDto
        {
            Direction = "Inbound",
            Content = "Preço",
            CreatedAt = DateTime.UtcNow
        }]);

        var context = await new ContextAssembler(queries, knowledge).BuildAsync(
            tenantId, Guid.NewGuid(), null, cancellationToken: CancellationToken.None);

        Assert.Contains("Plano STAR:", context.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("Plano FLOW:", context.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("Plano SCALA:", context.SystemPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Horário:", context.SystemPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public void KnownKnowledgeResponsePolicy_RecoversOutOfScopeDecisionWhenFactWasFound()
    {
        var response = new AiResponse
        {
            Decision = new AiDecision
            {
                Action = AiAction.Handoff,
                HandoffReason = "out_of_scope",
                Confidence = 0.97
            },
            InputTokens = 10,
            OutputTokens = 5
        };

        var recovered = KnownKnowledgeResponsePolicy.RecoverKnownAnswer(
            response,
            ["Preços dos planos: Plano Flow custa R$ 299 por mês."]);

        Assert.Equal(AiAction.Reply, recovered.Decision.Action);
        Assert.Equal(recovered.Decision.Text, recovered.Content);
        Assert.Contains("Plano Flow", recovered.Content, StringComparison.Ordinal);
        Assert.Null(recovered.Decision.HandoffReason);
    }

    [Fact]
    public void KnownKnowledgeResponsePolicy_RequestsProviderInferenceBeforeFallback()
    {
        var response = new AiResponse
        {
            Decision = new AiDecision
            {
                Action = AiAction.Handoff,
                HandoffReason = "out_of_scope",
                Confidence = 0.97
            },
            InputTokens = 10,
            OutputTokens = 5
        };

        Assert.True(KnownKnowledgeResponsePolicy.ShouldRequestInference(response, ["Serviço: Fato autorizado."]));
        Assert.Contains("combinação de fatos compatíveis", KnownKnowledgeResponsePolicy.BuildInferenceInstruction(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("out_of_scope")]
    [InlineData("low_confidence")]
    [InlineData("escalation_needed")]
    public void KnownKnowledgeResponsePolicy_RequestsInferenceForKnownButUncertainTopics(string reason)
    {
        var response = new AiResponse
        {
            Decision = new AiDecision
            {
                Action = AiAction.Handoff,
                HandoffReason = reason,
                Confidence = 0.4
            }
        };

        Assert.True(KnownKnowledgeResponsePolicy.ShouldRequestInference(
            response,
            ["Serviço: A Atenz é uma solução para atendimento no WhatsApp."]));
    }

    [Fact]
    public void KnownKnowledgeResponsePolicy_SummarizesMultiplePlansWithinReplyLimit()
    {
        var response = new AiResponse
        {
            Decision = new AiDecision
            {
                Action = AiAction.Handoff,
                HandoffReason = "out_of_scope",
                Confidence = 0.97
            },
            InputTokens = 10,
            OutputTokens = 5
        };

        var recovered = KnownKnowledgeResponsePolicy.RecoverKnownAnswer(
            response,
            [
                "Plano STAR: O plano STAR custa R$ 149 por mês, inclui 1 linha.",
                "Plano FLOW: O plano FLOW custa R$ 299 por mês, inclui 2 linhas.",
                "Plano SCALA: O plano SCALA custa R$ 497 por mês, inclui 3 linhas."
            ]);

        Assert.Contains("STAR: R$ 149", recovered.Content, StringComparison.Ordinal);
        Assert.Contains("FLOW: R$ 299", recovered.Content, StringComparison.Ordinal);
        Assert.Contains("SCALA: R$ 497", recovered.Content, StringComparison.Ordinal);
        Assert.True(recovered.Content!.Length <= AiOutputSafetyPolicy.MaxReplyCharacters);
    }

    [Fact]
    public void KnownKnowledgeResponsePolicy_ReplacesUnsupportedPricingReplyWithAuthorizedFacts()
    {
        var response = new AiResponse
        {
            Decision = new AiDecision
            {
                Action = AiAction.Reply,
                Text = "Temos o plano Premium por R$ 999.",
                Confidence = 0.99
            },
            Content = "Temos o plano Premium por R$ 999.",
            InputTokens = 10,
            OutputTokens = 5
        };

        var result = KnownKnowledgeResponsePolicy.EnforceAuthorizedPricing(
            response,
            "Quero saber os preços",
            ["Plano FLOW: R$ 299 por mês."]);

        Assert.Equal(AiAction.Reply, result.Decision.Action);
        Assert.Contains("FLOW", result.Content, StringComparison.Ordinal);
        Assert.Contains("299", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Premium", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("999", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void ContextAssembler_PricingQuestionPrefersPricingCategoryItems()
    {
        var tenantId = Guid.NewGuid();
        var knowledge = new FakeKnowledgeRepository(
        [
            KnowledgeItem.Create(tenantId, "Plano real", "Plano FLOW custa R$ 299.", 1, KnowledgeCategories.Pricing),
            KnowledgeItem.Create(tenantId, "Plano antigo", "Plano Premium custa R$ 999.", 100),
            KnowledgeItem.Create(tenantId, "Atendimento", "Atendemos pelo WhatsApp.", 100)
        ]);

        var context = new ContextAssembler(
            new FakeConversationQueries([]),
            knowledge).BuildSimulationAsync(
                tenantId,
                "Quais são os preços?",
                null,
                CancellationToken.None).GetAwaiter().GetResult();

        Assert.Contains("Plano real", context.SystemPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Plano antigo", context.SystemPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public void KnownKnowledgeResponsePolicy_DoesNotOverrideOtherHandoffReasons()
    {
        var response = new AiResponse
        {
            Decision = new AiDecision
            {
                Action = AiAction.Handoff,
                HandoffReason = "customer_request",
                Confidence = 0.97
            },
            InputTokens = 10,
            OutputTokens = 5
        };

        var result = KnownKnowledgeResponsePolicy.RecoverKnownAnswer(response, ["Fato autorizado."]);

        Assert.Equal(AiAction.Handoff, result.Decision.Action);
        Assert.Equal("customer_request", result.Decision.HandoffReason);
    }

    [Fact]
    public void ComposeSystemPrompt_MarksResponseExampleAsStyleOnly()
    {
        var prompt = ContextAssembler.ComposeSystemPrompt(
            null,
            ["Agenda: O atendimento ocorre de segunda a sexta."],
            responseExample: new ResponseExampleContext(
                "Quero agendar uma consulta",
                "Claro! Vou ajudar com o agendamento."));

        Assert.Contains("Exemplo de atendimento semelhante", prompt);
        Assert.Contains("copie apenas estilo e abordagem", prompt);
        Assert.Contains("não use como prova de fatos", prompt);
        Assert.Contains("Conhecimento relevante da empresa", prompt);
        Assert.True(prompt.Length <= 2_200);
    }

    [Fact]
    public void ComposeSystemPrompt_IncludesConfiguredWelcomeOnlyForFirstInbound()
    {
        var firstContactPrompt = ContextAssembler.ComposeSystemPrompt(
            null,
            welcomeMessage: "Olá! Somos a Clínica Aurora. Como podemos ajudar?",
            isFirstInbound: true);
        var returningContactPrompt = ContextAssembler.ComposeSystemPrompt(
            null,
            welcomeMessage: "Olá! Somos a Clínica Aurora. Como podemos ajudar?",
            isFirstInbound: false);

        Assert.Contains("Mensagem de boas-vindas personalizada", firstContactPrompt);
        Assert.Contains("Clínica Aurora", firstContactPrompt);
        Assert.DoesNotContain("Mensagem de boas-vindas personalizada", returningContactPrompt);
    }

    [Fact]
    public void ResolveWelcomeMessage_UsesBusinessProfileWhenConfiguredWelcomeIsGeneric()
    {
        var profile = """
            [PERFIL_EMPRESA]
            Tipo de negócio: Clínica e saúde
            Descrição do negócio: Atendimento odontológico humanizado.
            Produtos e serviços: Consultas e tratamentos odontológicos.
            [/PERFIL_EMPRESA]
            """;

        var welcome = ContextAssembler.ResolveWelcomeMessage(
            "Olá! Como posso ajudar?",
            profile,
            "Clínica Aurora");

        Assert.Equal(
            "Seja bem-vindo(a) à Clínica Aurora! Estamos aqui para ajudar com Consultas e tratamentos odontológicos. Como posso ajudar?",
            welcome);
    }

    [Fact]
    public void ComposeSystemPrompt_PreservesBusinessDirectionsAsAgentInstructions()
    {
        var directions = """
            [PERFIL_EMPRESA]
            Tipo de negócio: Serviços profissionais
            Tom de voz: Consultivo e acolhedor
            [/PERFIL_EMPRESA]

            Ao responder, conduza o cliente como um agente da empresa, faça uma pergunta objetiva e ofereça o próximo passo documentado.
            """;

        var prompt = ContextAssembler.ComposeSystemPrompt(
            directions,
            welcomeMessage: "Seja bem-vindo(a)! Estamos aqui para ajudar.",
            isFirstInbound: true,
            businessName: "Empresa Aurora");

        Assert.Contains("agente de atendimento da empresa Empresa Aurora", prompt);
        Assert.Contains("Diretrizes de atendimento cadastradas pelo responsável", prompt);
        Assert.Contains("ofereça o próximo passo documentado", prompt);
        Assert.Contains("Seja bem-vindo(a)!", prompt);
    }

    [Fact]
    public void ApplyGreetingPolicySynchronizesContentWithPersonalizedDecision()
    {
        var response = new AiResponse
        {
            Decision = new AiDecision
            {
                Action = AiAction.Reply,
                Text = "Olá! Como posso ajudar?",
                Confidence = 0.9
            },
            Content = "Olá! Como posso ajudar?",
            InputTokens = 1,
            OutputTokens = 1
        };

        var result = AiOrchestrationWorker.ApplyGreetingPolicy(
            response,
            "oi",
            isFirstInbound: true,
            personalizedWelcome: "Seja bem-vindo(a) à Empresa Aurora!");

        Assert.Equal(result.Decision.Text, result.Content);
        Assert.Equal("Seja bem-vindo(a) à Empresa Aurora!", result.Content);
    }

    [Fact]
    public async Task BuildSimulationAsync_UsesRelevantTenantKnowledgeAndExample()
    {
        var tenantId = Guid.NewGuid();
        var knowledge = new FakeKnowledgeRepository(
        [
            KnowledgeItem.Create(tenantId, "Preço da consulta", "A consulta custa R$ 150.", 100),
            KnowledgeItem.Create(tenantId, "Endereço", "A clínica fica no centro.", 90)
        ]);
        var pricingExample = AiResponseExample.Create(
            tenantId,
            "Qual é o preço da consulta?",
            "Claro! Vou informar o valor da consulta.");
        var schedulingExample = AiResponseExample.Create(
            tenantId,
            "Quero agendar uma consulta",
            "Vamos encontrar o melhor horário.");
        var examples = new FakeResponseExampleRepository([pricingExample, schedulingExample]);

        var context = await new ContextAssembler(
            new FakeConversationQueries([]),
            knowledge,
            examples).BuildSimulationAsync(
                tenantId,
                "Qual o preço da consulta? Meu e-mail é cliente@example.com",
                "Seja acolhedor.",
                CancellationToken.None);

        Assert.Single(context.Messages);
        Assert.Contains("[redacted]", context.Messages[0].Content, StringComparison.Ordinal);
        Assert.Contains("Preço da consulta: A consulta custa R$ 150.", context.SystemPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Endereço:", context.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains(pricingExample.IdealResponse, context.SystemPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain(schedulingExample.IdealResponse, context.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains(context.RelevantSources, source => source.Type == "conhecimento" && source.Name == "Preço da consulta");
        Assert.Contains(context.RelevantSources, source => source.Type == "exemplo" && source.Name == pricingExample.CustomerMessage);
        Assert.DoesNotContain(context.RelevantSources, source => source.Name == "Endereço");
    }

    private sealed class FakeConversationQueries(IReadOnlyList<MessageDto> messages) : IConversationQueries
    {
        public Task<CursorPaginationResponse<ConversationDto>> GetConversationsAsync(
            Guid tenantId, CursorPaginationRequest request, string? operatorUserId = null,
            List<string>? phoneNumberIds = null, Guid? queueId = null,
            ConversationStatus? status = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new CursorPaginationResponse<ConversationDto>());

        public Task<CursorPaginationResponse<MessageDto>> GetMessagesAsync(
            Guid tenantId, Guid conversationId, CursorPaginationRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new CursorPaginationResponse<MessageDto> { Items = messages });

        public Task<ConversationDto?> GetConversationByIdAsync(
            Guid tenantId, Guid conversationId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ConversationDto?>(null);
    }

    private sealed class FakeKnowledgeRepository(IReadOnlyList<KnowledgeItem> items) : IKnowledgeItemRepository
    {
        public Task<KnowledgeItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(items.FirstOrDefault(item => item.Id == id));

        public Task<IReadOnlyList<KnowledgeItem>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(items);

        public Task<IReadOnlyList<KnowledgeItem>> GetActiveByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(items);

        public Task AddAsync(KnowledgeItem item, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(KnowledgeItem item, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeResponseExampleRepository(IReadOnlyList<AiResponseExample> examples)
        : IAiResponseExampleRepository
    {
        public Task<AiResponseExample?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(examples.FirstOrDefault(example => example.Id == id));

        public Task<IReadOnlyList<AiResponseExample>> GetByTenantAsync(
            Guid tenantId,
            CancellationToken cancellationToken = default) => Task.FromResult(examples);

        public Task<IReadOnlyList<AiResponseExample>> GetActiveByTenantAsync(
            Guid tenantId,
            CancellationToken cancellationToken = default) => Task.FromResult(examples);

        public Task AddAsync(AiResponseExample example, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpdateAsync(AiResponseExample example, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}

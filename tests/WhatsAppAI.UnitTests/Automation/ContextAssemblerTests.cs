using WhatsAppAI.Application.Automation.Context;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Application.Conversations.Queries;
using WhatsAppAI.Domain.Knowledge;

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

        var boletoPosition = context.SystemPrompt.IndexOf("Boleto:", StringComparison.Ordinal);
        var horarioPosition = context.SystemPrompt.IndexOf("Horário:", StringComparison.Ordinal);

        Assert.True(boletoPosition >= 0);
        Assert.True(horarioPosition >= 0);
        Assert.True(boletoPosition < horarioPosition);
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
        Assert.Contains("No relevant company knowledge was found", context.SystemPrompt);
    }

    [Fact]
    public async Task BuildAsync_UsesBoundedRecentContextAndPreservesMandatoryInstructions()
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
        Assert.All(context.Messages, message => Assert.True(message.Content.Length <= 280));
        Assert.True(context.SystemPrompt.Length <= 4_800);
        Assert.Contains("Boleto:", context.SystemPrompt);
        Assert.DoesNotContain("Quarto item:", context.SystemPrompt);
        Assert.Contains("Return only one valid JSON object", context.SystemPrompt);
    }

    private sealed class FakeConversationQueries(IReadOnlyList<MessageDto> messages) : IConversationQueries
    {
        public Task<CursorPaginationResponse<ConversationDto>> GetConversationsAsync(
            Guid tenantId, CursorPaginationRequest request, string? operatorUserId = null,
            List<string>? phoneNumberIds = null, Guid? queueId = null,
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
}

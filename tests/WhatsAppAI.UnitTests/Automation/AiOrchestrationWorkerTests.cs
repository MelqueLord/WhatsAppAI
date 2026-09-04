using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Integrations;
using WhatsAppAI.Domain.Messaging;
using WhatsAppAI.Infrastructure.Identity;
using WhatsAppAI.Infrastructure.Persistence;
using WhatsAppAI.Infrastructure.Persistence.Repositories;
using WhatsAppAI.Infrastructure.Workers;

namespace WhatsAppAI.UnitTests.Automation;

public sealed class AiOrchestrationWorkerTests
{
    [Fact]
    public void ApplyUnavailableAiFallback_FinalizesInboundAndCreatesFallbackMessage()
    {
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var inbound = Message.CreateInbound(
            tenantId, Guid.NewGuid(), contactId, "external-id", MessageType.Text, "Olá");
        var botConfig = BotConfiguration.Create(tenantId, BotMode.AiPowered);
        botConfig.UpdateMessages(null, null, null, "Fallback configurado", "Handoff configurado", null, null);

        var fallback = AiOrchestrationWorker.ApplyUnavailableAiFallback(
            inbound, botConfig);

        Assert.True(inbound.ProcessedByAi);
        Assert.Equal("Handoff configurado", fallback.Content);
        Assert.Equal($"ai-unavailable:{inbound.Id}", fallback.IdempotencyKey);
        Assert.Equal(MessageStatus.Queued, fallback.Status);
    }

    [Fact]
    public void ResolveHandoffMessage_UsesTenantFallbackWhenHandoffMessageIsMissing()
    {
        var botConfig = BotConfiguration.Create(Guid.NewGuid(), BotMode.AiPowered);
        botConfig.UpdateMessages(null, null, null, "Fallback do tenant", null, null, null);

        var message = AiOrchestrationWorker.ResolveHandoffMessage(botConfig);

        Assert.Equal("Fallback do tenant", message);
    }

    [Fact]
    public void ResolveHandoffMessage_UsesDefaultOnlyWithoutTenantConfiguration()
    {
        var message = AiOrchestrationWorker.ResolveHandoffMessage(null);

        Assert.Equal("Vou encaminhar voce para um atendente.", message);
    }

    [Fact]
    public void ResolveQueueTransferMessage_UsesTenantConfiguration()
    {
        var botConfig = BotConfiguration.Create(Guid.NewGuid(), BotMode.SimpleAutoReply);
        botConfig.UpdateMessages(null, null, null, null, null, "Transferência configurada", null);

        Assert.Equal("Transferência configurada", AiOrchestrationWorker.ResolveQueueTransferMessage(botConfig));
    }

    [Fact]
    public void SelectBotRoutingQueue_UsesAuthorizedKeywordQueueAndLetsExplicitKeywordReplacePriorAssignment()
    {
        var tenantId = Guid.NewGuid();
        var matchingQueue = ServiceLine.Create(tenantId, "Financeiro");
        matchingQueue.SetKeywords("boleto, cobrança");
        var otherQueue = ServiceLine.Create(tenantId, "Vendas");
        otherQueue.SetKeywords("preço");

        Assert.Equal(matchingQueue.Id, AiOrchestrationWorker
            .SelectBotRoutingQueue(null, [matchingQueue, otherQueue], "Preciso da segunda via do boleto")?.Id);
        Assert.Equal(matchingQueue.Id, AiOrchestrationWorker
            .SelectBotRoutingQueue(matchingQueue.Id, [matchingQueue, otherQueue], "Quero saber sobre cobrança")?.Id);
        Assert.Equal(matchingQueue.Id, AiOrchestrationWorker
            .SelectBotRoutingQueue(otherQueue.Id, [matchingQueue, otherQueue], "Preciso da segunda via do boleto")?.Id);
        Assert.Null(AiOrchestrationWorker
            .SelectBotRoutingQueue(null, [matchingQueue], "Quero saber o preço"));
    }

    [Theory]
    [InlineData("low_confidence")]
    [InlineData("customer_request")]
    [InlineData("sensitive_topic")]
    [InlineData("out_of_scope")]
    [InlineData("escalation_needed")]
    [InlineData("complaint")]
    [InlineData("refund_request")]
    [InlineData("legal_issue")]
    [InlineData("invalid_response")]
    [InlineData("queue_selection")]
    [InlineData("ai_unavailable")]
    [InlineData("data_processing_not_authorized")]
    [InlineData("ai_budget_exhausted")]
    [InlineData("ai_quota_exhausted")]
    [InlineData("ai_retry_exhausted")]
    public async Task RegisterAutomaticHandoffAsync_RecordsSupportedHandoffReason(string reason)
    {
        var tenantId = Guid.NewGuid();
        var conversation = Conversation.Create(tenantId, Guid.NewGuid(), "phone-number-id");
        var conversationRepository = new FakeConversationRepository();
        var handoffRepository = new FakeHandoffEventRepository();

        await AiOrchestrationWorker.RegisterAutomaticHandoffAsync(
            tenantId, conversation, reason, conversationRepository, handoffRepository, CancellationToken.None);

        var handoff = Assert.Single(handoffRepository.Events);
        Assert.Equal(ConversationMode.Human, conversation.Mode);
        Assert.Same(conversation, Assert.Single(conversationRepository.Updated));
        Assert.Equal(ConversationMode.Automatic, handoff.FromMode);
        Assert.Equal(ConversationMode.Human, handoff.ToMode);
        Assert.Null(handoff.OperatorUserId);
        Assert.Equal(reason, handoff.Reason);
    }

    [Fact]
    public async Task RegisterAutomaticHandoffAsync_DoesNotRecordWhenAlreadyHuman()
    {
        var tenantId = Guid.NewGuid();
        var conversation = Conversation.Create(tenantId, Guid.NewGuid(), "phone-number-id");
        conversation.SwitchMode(ConversationMode.Human, conversation.Version);
        var conversationRepository = new FakeConversationRepository();
        var handoffRepository = new FakeHandoffEventRepository();

        var registered = await AiOrchestrationWorker.RegisterAutomaticHandoffAsync(
            tenantId, conversation, "low_confidence", conversationRepository, handoffRepository, CancellationToken.None);

        Assert.False(registered);
        Assert.Empty(conversationRepository.Updated);
        Assert.Empty(handoffRepository.Events);
    }

    [Fact]
    public async Task PersistAutomaticHandoffAsync_PersistsHandoffMessageAndOutboxTogether()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var tenantId = Guid.NewGuid();
        var currentTenant = new TestCurrentTenant(tenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new AppDbContext(options, currentTenant);
        await db.Database.EnsureCreatedAsync();

        var contact = WhatsAppAI.Domain.Messaging.Contact.Create(tenantId, "+5511999999999");
        var conversation = Conversation.Create(tenantId, contact.Id, "manual");
        var inbound = Message.CreateInbound(tenantId, conversation.Id, contact.Id, "inbound-1", MessageType.Text, "Olá");
        db.Contacts.Add(contact);
        db.Conversations.Add(conversation);
        db.Messages.Add(inbound);
        await db.SaveChangesAsync();

        var messageRepository = new MessageRepository(db);
        var conversationRepository = new ConversationRepository(db);
        var outboxRepository = new OutboxMessageRepository(db);
        var handoffRepository = new HandoffEventRepository(db);

        var registered = await AiOrchestrationWorker.PersistAutomaticHandoffAsync(
            tenantId, inbound, conversation, "low_confidence", "Vou encaminhar para um atendente.",
            "ai-handoff", db, messageRepository, conversationRepository, outboxRepository,
            handoffRepository, CancellationToken.None);

        Assert.True(registered);
        Assert.True(inbound.ProcessedByAi);
        Assert.Single(await db.HandoffEvents.IgnoreQueryFilters().ToListAsync());
        Assert.Single(await db.Messages.IgnoreQueryFilters().Where(message => message.Direction == MessageDirection.Outbound).ToListAsync());
        Assert.Single(await db.OutboxMessages.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task PersistAutomaticHandoffAsync_AssignsQueueAndRecordsTransfer()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var tenantId = Guid.NewGuid();
        var queueId = Guid.NewGuid();
        var priorQueueId = Guid.NewGuid();
        var currentTenant = new TestCurrentTenant(tenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(options, currentTenant);
        await db.Database.EnsureCreatedAsync();

        var contact = Contact.Create(tenantId, "+5511999999999");
        var conversation = Conversation.Create(tenantId, contact.Id, "manual");
        conversation.AssignQueue(priorQueueId);
        var inbound = Message.CreateInbound(tenantId, conversation.Id, contact.Id, "inbound-queue", MessageType.Text, "financeiro");
        db.Contacts.Add(contact);
        db.Conversations.Add(conversation);
        db.Messages.Add(inbound);
        await db.SaveChangesAsync();

        var registered = await AiOrchestrationWorker.PersistAutomaticHandoffAsync(
            tenantId, inbound, conversation, "queue_selection", "Fila financeira", "bot-queue-transfer", db,
            new MessageRepository(db), new ConversationRepository(db), new OutboxMessageRepository(db),
            new HandoffEventRepository(db), CancellationToken.None, queueId);

        Assert.True(registered);
        Assert.Equal(queueId, conversation.QueueId);
        Assert.Equal(ConversationMode.Human, conversation.Mode);
        Assert.Equal("Fila financeira", (await db.Messages.IgnoreQueryFilters()
            .SingleAsync(message => message.Direction == MessageDirection.Outbound)).Content);
        Assert.Single(await db.HandoffEvents.IgnoreQueryFilters().ToListAsync());
    }

    private sealed class FakeConversationRepository : IConversationRepository
    {
        public List<Conversation> Updated { get; } = [];

        public Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Conversation?>(null);
        public Task<Conversation?> GetByContactAndPhoneAsync(Guid tenantId, Guid contactId, string phoneNumberId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Conversation?>(null);
        public Task<IReadOnlyList<Conversation>> GetByTenantAsync(Guid tenantId, int limit = 50, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Conversation>>([]);
        public Task<IReadOnlyList<Conversation>> GetOpenByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Conversation>>([]);
        public Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateAsync(Conversation conversation, CancellationToken cancellationToken = default)
        {
            Updated.Add(conversation);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeHandoffEventRepository : IHandoffEventRepository
    {
        public List<HandoffEvent> Events { get; } = [];

        public Task AddAsync(HandoffEvent handoffEvent)
        {
            Events.Add(handoffEvent);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<HandoffEvent>> GetByConversationAsync(Guid tenantId, Guid conversationId) =>
            Task.FromResult<IReadOnlyList<HandoffEvent>>([]);
    }

    private sealed class TestCurrentTenant(Guid tenantId) : ICurrentTenant
    {
        public Guid? TenantId => tenantId;
        public Guid? UserId => null;
        public string? UserRole => "TenantOwner";
        public bool IsPlatformAdmin => false;
        public bool IsAuthenticated => true;
        public SupportSessionInfo? SupportSession => null;
        public void SetContext(Guid? tenantId, Guid userId, string role, bool isPlatformAdmin) { }
        public void EnterSupportSession(Guid tenantId, string reason) { }
        public void ExitSupportSession() { }
        public void Clear() { }
    }
}

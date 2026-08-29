using WhatsAppAI.Application.Abstractions;
using WhatsAppAI.Domain.Integrations;
using WhatsAppAI.Domain.Messaging;
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
}

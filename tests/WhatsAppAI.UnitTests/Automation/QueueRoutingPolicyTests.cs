using WhatsAppAI.Application.Automation;
using WhatsAppAI.Application.Automation.Policy;

namespace WhatsAppAI.UnitTests.Automation;

public sealed class QueueRoutingPolicyTests
{
    [Fact]
    public void Apply_AuthorizedQueue_AssignsQueueWithoutHumanTakeover()
    {
        var queueId = Guid.NewGuid();
        var decision = new AiDecision
        {
            Action = AiAction.Reply,
            Confidence = 0.9,
            QueueName = "Financeiro"
        };

        var result = QueueRoutingPolicy.Apply(
            decision,
            [new RoutingQueueCandidate(queueId, "Financeiro")],
            null);

        Assert.Equal(queueId, result.QueueId);
        Assert.Equal(AiAction.NoAction, result.Decision.Action);
        Assert.Equal("queue_selection", result.Decision.HandoffReason);
        Assert.Null(result.Decision.Text);
    }

    [Fact]
    public void Apply_UnauthorizedQueue_DoesNotRoute()
    {
        var decision = new AiDecision
        {
            Action = AiAction.Reply,
            Confidence = 0.9,
            QueueName = "Outro tenant"
        };

        var result = QueueRoutingPolicy.Apply(decision, [], null);

        Assert.Null(result.QueueId);
        Assert.Equal(AiAction.Reply, result.Decision.Action);
        Assert.Null(result.Decision.QueueName);
    }

    [Fact]
    public void Apply_CurrentQueueSelection_DoesNotReplaceAQueuedConversationWithHuman()
    {
        var queueId = Guid.NewGuid();
        var decision = new AiDecision
        {
            Action = AiAction.Handoff,
            Confidence = 0.9,
            QueueName = "Vendas"
        };

        var result = QueueRoutingPolicy.Apply(
            decision,
            [new RoutingQueueCandidate(queueId, "Vendas")],
            queueId);

        Assert.Null(result.QueueId);
        Assert.Equal(AiAction.NoAction, result.Decision.Action);
        Assert.Equal("queue_selection", result.Decision.HandoffReason);
        Assert.Null(result.Decision.QueueName);
    }

    [Fact]
    public void Apply_CurrentQueueEcho_PreservesValidReply()
    {
        var queueId = Guid.NewGuid();
        var decision = new AiDecision
        {
            Action = AiAction.Reply,
            Text = "O plano começa em R$ 29.",
            Confidence = 0.95,
            QueueName = "Vendas"
        };

        var result = QueueRoutingPolicy.Apply(
            decision,
            [new RoutingQueueCandidate(queueId, "Vendas")],
            queueId);

        Assert.Null(result.QueueId);
        Assert.Equal(AiAction.Reply, result.Decision.Action);
        Assert.Equal("O plano começa em R$ 29.", result.Decision.Text);
        Assert.Null(result.Decision.QueueName);
    }

    [Fact]
    public void Apply_DifferentAuthorizedQueue_RoutesWithoutHumanTakeover()
    {
        var assignedQueueId = Guid.NewGuid();
        var selectedQueueId = Guid.NewGuid();
        var decision = new AiDecision
        {
            Action = AiAction.Handoff,
            Confidence = 0.9,
            QueueName = "Vendas"
        };

        var result = QueueRoutingPolicy.Apply(
            decision,
            [new RoutingQueueCandidate(selectedQueueId, "Vendas")],
            assignedQueueId);

        Assert.Equal(selectedQueueId, result.QueueId);
        Assert.Equal(AiAction.NoAction, result.Decision.Action);
    }

    [Fact]
    public void Apply_ExplicitHumanRequest_DoesNotTreatQueueAsAutomaticRouting()
    {
        var decision = new AiDecision
        {
            Action = AiAction.Handoff,
            HandoffReason = "queue_selection",
            Confidence = 0.9,
            QueueName = "Atendimento"
        };

        var result = QueueRoutingPolicy.Apply(
            decision,
            [new RoutingQueueCandidate(Guid.NewGuid(), "Atendimento")],
            null,
            "Quero falar com um atendente");

        Assert.Null(result.QueueId);
        Assert.Null(result.Decision.QueueName);
        Assert.Equal(AiAction.Handoff, result.Decision.Action);
    }

    [Fact]
    public void Apply_UnauthorizedQueueSelection_DoesNotCreateHumanHandoff()
    {
        var decision = new AiDecision
        {
            Action = AiAction.Handoff,
            HandoffReason = "queue_selection",
            Confidence = 0.9,
            QueueName = "Fila inexistente"
        };

        var result = QueueRoutingPolicy.Apply(decision, [], null);

        Assert.Null(result.QueueId);
        Assert.Equal(AiAction.NoAction, result.Decision.Action);
        Assert.Null(result.Decision.QueueName);
        Assert.Null(result.Decision.HandoffReason);
    }

    [Fact]
    public void Apply_CriticalHandoffReason_WinsOverAuthorizedQueue()
    {
        var decision = new AiDecision
        {
            Action = AiAction.Handoff,
            HandoffReason = "sensitive_topic",
            Confidence = 0.9,
            QueueName = "Suporte"
        };

        var result = QueueRoutingPolicy.Apply(
            decision,
            [new RoutingQueueCandidate(Guid.NewGuid(), "Suporte")],
            null);

        Assert.Null(result.QueueId);
        Assert.Equal(AiAction.Handoff, result.Decision.Action);
        Assert.Equal("sensitive_topic", result.Decision.HandoffReason);
        Assert.Null(result.Decision.QueueName);
    }
}

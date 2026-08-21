using WhatsAppAI.Application.Automation;
using WhatsAppAI.Application.Automation.Policy;

namespace WhatsAppAI.UnitTests.Automation;

public sealed class QueueRoutingPolicyTests
{
    [Fact]
    public void Apply_AuthorizedQueue_ForcesHandoffAndReturnsQueue()
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
            false);

        Assert.Equal(queueId, result.QueueId);
        Assert.Equal(AiAction.Handoff, result.Decision.Action);
        Assert.Equal("queue_selection", result.Decision.HandoffReason);
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

        var result = QueueRoutingPolicy.Apply(decision, [], false);

        Assert.Null(result.QueueId);
        Assert.Equal(AiAction.Reply, result.Decision.Action);
    }

    [Fact]
    public void Apply_AlreadyAssigned_DoesNotReroute()
    {
        var decision = new AiDecision
        {
            Action = AiAction.Handoff,
            Confidence = 0.9,
            QueueName = "Vendas"
        };

        var result = QueueRoutingPolicy.Apply(
            decision,
            [new RoutingQueueCandidate(Guid.NewGuid(), "Vendas")],
            true);

        Assert.Null(result.QueueId);
    }
}

namespace WhatsAppAI.Application.Automation.Policy;

public static class QueueRoutingPolicy
{
    public static QueueRoutingResult Apply(
        AiDecision decision,
        IReadOnlyList<RoutingQueueCandidate> authorizedQueues,
        bool conversationAlreadyAssigned)
    {
        if (conversationAlreadyAssigned || string.IsNullOrWhiteSpace(decision.QueueName))
            return new QueueRoutingResult(decision, null);

        var matchedQueue = authorizedQueues.FirstOrDefault(queue =>
            queue.Name.Equals(decision.QueueName, StringComparison.OrdinalIgnoreCase));
        if (matchedQueue is null)
            return new QueueRoutingResult(decision, null);

        return new QueueRoutingResult(
            decision with
            {
                Action = AiAction.Handoff,
                HandoffReason = "queue_selection",
                QueueName = matchedQueue.Name
            },
            matchedQueue.Id);
    }
}

public sealed record RoutingQueueCandidate(Guid Id, string Name);
public sealed record QueueRoutingResult(AiDecision Decision, Guid? QueueId);

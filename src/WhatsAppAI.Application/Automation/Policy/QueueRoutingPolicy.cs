namespace WhatsAppAI.Application.Automation.Policy;

public static class QueueRoutingPolicy
{
    public static QueueRoutingResult Apply(
        AiDecision decision,
        IReadOnlyList<RoutingQueueCandidate> authorizedQueues,
        Guid? assignedQueueId,
        string? messageContent = null)
    {
        if (HumanHandoffRequestPolicy.IsExplicitHumanRequest(messageContent))
            return new QueueRoutingResult(decision with { QueueName = null }, null);

        if (string.IsNullOrWhiteSpace(decision.QueueName))
        {
            if (assignedQueueId.HasValue &&
                decision.Action == AiAction.Handoff &&
                decision.HandoffReason is "queue_selection")
            {
                return new QueueRoutingResult(
                    decision with
                    {
                        Action = AiAction.NoAction,
                        Text = null
                    },
                    null);
            }

            return new QueueRoutingResult(decision, null);
        }

        // A queue must never mask a mandatory human handoff. This protects
        // sensitive, unsafe, legal, complaint and genuinely missing-fact
        // decisions even if a provider returns a queue alongside them.
        if (decision.Action == AiAction.Handoff &&
            decision.HandoffReason is not null &&
            BehaviorPolicy.RequiredHandoffReasons.Contains(decision.HandoffReason))
        {
            return new QueueRoutingResult(
                decision with { QueueName = null },
                null);
        }

        var matchedQueue = authorizedQueues.FirstOrDefault(queue =>
            queue.Name.Equals(decision.QueueName, StringComparison.OrdinalIgnoreCase));
        if (matchedQueue is null)
        {
            var sanitizedDecision = decision with { QueueName = null };
            if (decision.Action == AiAction.Handoff &&
                decision.HandoffReason is "queue_selection")
            {
                sanitizedDecision = sanitizedDecision with
                {
                    Action = AiAction.NoAction,
                    HandoffReason = null,
                    Text = null
                };
            }

            return new QueueRoutingResult(sanitizedDecision, null);
        }

        if (assignedQueueId == matchedQueue.Id)
        {
            // A provider may echo the current queue while answering. Preserve
            // a valid answer instead of replacing it with a waiting notice.
            if (decision.Action == AiAction.Reply)
                return new QueueRoutingResult(decision with { QueueName = null }, null);

            return new QueueRoutingResult(
                decision with
                {
                    Action = AiAction.NoAction,
                    HandoffReason = "queue_selection",
                    Text = null,
                    QueueName = null
                },
                null);
        }

        return new QueueRoutingResult(
            decision with
            {
                Action = AiAction.NoAction,
                HandoffReason = "queue_selection",
                Text = null,
                QueueName = matchedQueue.Name
            },
            matchedQueue.Id);
    }
}

public sealed record RoutingQueueCandidate(Guid Id, string Name);
public sealed record QueueRoutingResult(AiDecision Decision, Guid? QueueId);

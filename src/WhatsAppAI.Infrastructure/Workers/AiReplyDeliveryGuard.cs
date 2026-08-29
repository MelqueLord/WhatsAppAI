using System.Globalization;
using WhatsAppAI.Domain.Messaging;

namespace WhatsAppAI.Infrastructure.Workers;

internal static class AiReplyDeliveryGuard
{
    private static readonly string[] AutomatedPrefixes =
    [
        "ai:",
        "simple-auto-reply:",
        "ai-empty-reply:",
        "ai-unavailable:",
        "ai-quota:",
        "ai-retry-exhausted:",
        "ai-handoff:",
        "ai-queue-transfer:"
    ];

    public static string CreateIdempotencyKey(Guid inboundMessageId, uint conversationVersion) =>
        $"ai:{inboundMessageId}:v{conversationVersion}";

    public static string CreateAutomatedIdempotencyKey(
        string kind,
        Guid inboundMessageId,
        uint conversationVersion) =>
        $"{kind}:{inboundMessageId}:v{conversationVersion}";

    public static bool IsAiReply(string? idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) ||
            !idempotencyKey.StartsWith("ai:", StringComparison.Ordinal))
            return false;

        var idEnd = idempotencyKey.IndexOf(':', 3);
        var idLength = idEnd < 0 ? idempotencyKey.Length - 3 : idEnd - 3;
        return Guid.TryParse(idempotencyKey.AsSpan(3, idLength), out _);
    }

    public static bool IsAutomated(string? idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return false;

        var prefix = Array.Find(AutomatedPrefixes, item =>
            idempotencyKey.StartsWith(item, StringComparison.Ordinal));
        if (prefix is null)
            return false;

        var idEnd = idempotencyKey.IndexOf(':', prefix.Length);
        var idLength = idEnd < 0 ? idempotencyKey.Length - prefix.Length : idEnd - prefix.Length;
        return Guid.TryParse(idempotencyKey.AsSpan(prefix.Length, idLength), out _);
    }

    public static bool TryGetExpectedVersion(string? idempotencyKey, out uint expectedVersion)
    {
        expectedVersion = 0;
        if (!IsAutomated(idempotencyKey))
            return false;

        var versionMarker = idempotencyKey!.LastIndexOf(":v", StringComparison.Ordinal);
        return versionMarker > 0 && uint.TryParse(
            idempotencyKey.AsSpan(versionMarker + 2),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out expectedVersion);
    }

    public static bool CanSendAutomatedNotice(
        Conversation? conversation,
        uint expectedVersion,
        DateTime utcNow) =>
        conversation is not null &&
        conversation.Version == expectedVersion &&
        conversation.Mode is ConversationMode.Automatic or ConversationMode.Human &&
        conversation.IsWindowOpen(utcNow);

    public static bool CanSend(
        Conversation? conversation,
        uint expectedVersion,
        DateTime utcNow) =>
        conversation is not null &&
        conversation.Version == expectedVersion &&
        conversation.Mode == ConversationMode.Automatic &&
        conversation.IsWindowOpen(utcNow);
}

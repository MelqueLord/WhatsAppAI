using System.Globalization;
using WhatsAppAI.Domain.Messaging;

namespace WhatsAppAI.Infrastructure.Workers;

internal static class AiReplyDeliveryGuard
{
    public static string CreateIdempotencyKey(Guid inboundMessageId, uint conversationVersion) =>
        $"ai:{inboundMessageId}:v{conversationVersion}";

    public static bool IsAiReply(string? idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) ||
            !idempotencyKey.StartsWith("ai:", StringComparison.Ordinal))
            return false;

        var idEnd = idempotencyKey.IndexOf(':', 3);
        var idLength = idEnd < 0 ? idempotencyKey.Length - 3 : idEnd - 3;
        return Guid.TryParse(idempotencyKey.AsSpan(3, idLength), out _);
    }

    public static bool TryGetExpectedVersion(string? idempotencyKey, out uint expectedVersion)
    {
        expectedVersion = 0;
        if (!IsAiReply(idempotencyKey))
            return false;

        var versionMarker = idempotencyKey!.LastIndexOf(":v", StringComparison.Ordinal);
        return versionMarker > 3 && uint.TryParse(
            idempotencyKey.AsSpan(versionMarker + 2),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out expectedVersion);
    }

    public static bool CanSend(
        Conversation? conversation,
        uint expectedVersion,
        DateTime utcNow) =>
        conversation is not null &&
        conversation.Version == expectedVersion &&
        conversation.Mode == ConversationMode.Automatic &&
        conversation.IsWindowOpen(utcNow);
}

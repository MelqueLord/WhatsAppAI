export const inboundMessageDeduplicationRetentionMs = 15 * 60 * 1000
export const maximumRememberedInboundMessageIds = 5_000

// The bridge keeps message IDs only in memory to suppress duplicate live events.
// Message content, contacts and conversation history are never retained here.
export function rememberInboundMessage(seenMessageIds, messageId, now = Date.now()) {
  for (const [id, expiresAt] of seenMessageIds) {
    if (expiresAt <= now) seenMessageIds.delete(id)
  }

  if (seenMessageIds.has(messageId)) return false

  seenMessageIds.set(messageId, now + inboundMessageDeduplicationRetentionMs)
  while (seenMessageIds.size > maximumRememberedInboundMessageIds) {
    seenMessageIds.delete(seenMessageIds.keys().next().value)
  }
  return true
}

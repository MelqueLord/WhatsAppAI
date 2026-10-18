export function getInboundRevocation(update, revokeStubType) {
  if (update?.update?.messageStubType !== revokeStubType ||
      update.key?.fromMe !== false || update.update?.key?.fromMe === true) return null

  const originalId = update.key?.id
  const remoteJid = update.key?.remoteJid
  if (typeof originalId !== 'string' || originalId.length === 0 || originalId.length > 100 ||
      typeof remoteJid !== 'string' || remoteJid.length === 0 ||
      remoteJid.endsWith('@g.us') || remoteJid === 'status@broadcast') return null

  const revocationId = update.update?.key?.id
  return {
    originalId,
    remoteJid,
    remoteJidAlt: update.key.remoteJidAlt,
    eventId: typeof revocationId === 'string' && revocationId.length > 0 && revocationId.length <= 100
      ? revocationId
      : originalId,
  }
}

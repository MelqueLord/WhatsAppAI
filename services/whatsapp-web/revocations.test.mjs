import assert from 'node:assert/strict'
import test from 'node:test'
import { getInboundRevocation } from './revocations.mjs'

const revokeStub = 1

test('identifies an inbound customer revocation by the original message key', () => {
  const result = getInboundRevocation({
    key: { id: 'original-id', remoteJid: '5511999999999@s.whatsapp.net', fromMe: false },
    update: { messageStubType: revokeStub, key: { id: 'revocation-id', fromMe: false } },
  }, revokeStub)

  assert.deepEqual(result, {
    originalId: 'original-id',
    remoteJid: '5511999999999@s.whatsapp.net',
    remoteJidAlt: undefined,
    eventId: 'revocation-id',
  })
})

test('ignores own deletions, unrelated updates and group revocations', () => {
  const update = {
    key: { id: 'original-id', remoteJid: '5511999999999@s.whatsapp.net', fromMe: false },
    update: { messageStubType: revokeStub, key: { id: 'revocation-id' } },
  }

  assert.equal(getInboundRevocation({ ...update, key: { ...update.key, fromMe: true } }, revokeStub), null)
  assert.equal(getInboundRevocation({ ...update, update: { messageStubType: 2 } }, revokeStub), null)
  assert.equal(getInboundRevocation({ ...update, key: { ...update.key, remoteJid: 'group@g.us' } }, revokeStub), null)
})

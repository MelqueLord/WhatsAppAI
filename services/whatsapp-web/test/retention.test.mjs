import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'
import {
  inboundMessageDeduplicationRetentionMs,
  rememberInboundMessage,
} from '../retention.mjs'

test('deduplication IDs expire and are not persisted', async () => {
  const seen = new Map()
  const now = 1_000

  assert.equal(rememberInboundMessage(seen, 'message-1', now), true)
  assert.equal(rememberInboundMessage(seen, 'message-1', now + 1), false)
  assert.equal(rememberInboundMessage(seen, 'message-1', now + inboundMessageDeduplicationRetentionMs + 1), true)

  const server = await readFile(new URL('../server.mjs', import.meta.url), 'utf8')
  assert.doesNotMatch(server, /inbox\\.json/)
  assert.doesNotMatch(server, /messaging-history\\.set/)
  assert.doesNotMatch(server, /conversations/)
  assert.match(server, /\/sessions\/:tenantId\/send-media/)
  assert.match(server, /X-WhatsApp-Web-Media-Sha256/)
})

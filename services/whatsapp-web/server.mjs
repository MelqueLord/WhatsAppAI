import express from 'express'
import { randomUUID, timingSafeEqual } from 'node:crypto'
import { mkdir, readFile, readdir, rm, writeFile } from 'node:fs/promises'
import { promisify } from 'node:util'
import { gzip, gunzip } from 'node:zlib'
import QRCode from 'qrcode'
import makeWASocket, { DisconnectReason, useMultiFileAuthState } from '@whiskeysockets/baileys'

const app = express()
const port = Number(process.env.PORT ?? 3020)
const apiWebhookUrl = process.env.WHATSAPP_WEB_API_URL ?? 'http://localhost:5000/api/webhooks/whatsapp-web'
const apiWebhookSecret = process.env.WHATSAPP_WEB_WEBHOOK_SECRET ?? 'development-whatsapp-web-secret'
const isProduction = process.env.NODE_ENV === 'production'
const configuredInstanceId = process.env.WHATSAPP_WEB_INSTANCE_ID
const instanceId = `${configuredInstanceId ?? 'local'}-${randomUUID()}`
const instanceUrl = normalizeInstanceUrl(process.env.WHATSAPP_WEB_INSTANCE_URL ?? `http://localhost:${port}`)
const sessions = new Map()
const botConfigs = new Map()
const reconnectTimers = new Map()
const authBackupTimers = new Map()
const authBackupPromises = new Map()
const lastAuthPayloads = new Map()
const reconnectAttempts = new Map()
const leaseRenewTimers = new Map()
const gzipAsync = promisify(gzip)
const gunzipAsync = promisify(gunzip)
const sessionIdPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}-qr-(?:[1-9]\d?|100)$/i
const bridgeSecretHeader = 'x-whatsapp-web-secret'
let isShuttingDown = false

if (isProduction && (apiWebhookSecret === 'development-whatsapp-web-secret' || apiWebhookSecret.length < 32)) {
  throw new Error('WHATSAPP_WEB_WEBHOOK_SECRET must be a production secret with at least 32 characters.')
}

if (isProduction && (!configuredInstanceId || !process.env.WHATSAPP_WEB_INSTANCE_URL)) {
  throw new Error('WHATSAPP_WEB_INSTANCE_ID and WHATSAPP_WEB_INSTANCE_URL are required in production.')
}

const defaultBotConfig = {
  configured: true,
  mode: 'SimpleAutoReply',
  welcomeMessage: 'Ola! Sou o atendimento automatico. Digite: 1-precos, 2-horarios, 3-atendente.',
  returningMessage: 'Ola de novo! Digite uma opcao do menu para continuar.',
  offlineMessage: 'No momento estamos fora do horario de atendimento. Retornaremos em breve.',
  fallbackMessage: 'Nao entendi. Escolha uma opcao do menu ou digite atendente.',
  mediaMessage: 'Recebi sua midia. Digite uma opcao do menu ou aguarde atendimento.',
  handoffMessage: 'Vou encaminhar voce para um atendente.',
  flowSteps: [
    { id: 'step-price', title: 'Precos', keywords: '1, preco, preço, valor', response: 'Temos planos conforme sua necessidade. Um atendente pode finalizar a proposta.' },
    { id: 'step-hours', title: 'Horarios', keywords: '2, horario, horário, abre, funciona', response: 'Atendemos de segunda a sexta das 8h as 18h.' },
    { id: 'step-human', title: 'Atendente', keywords: '3, atendente, humano, gerente', response: 'Certo, vou encaminhar voce para um atendente.' },
  ],
  maxTokensPerResponse: 500,
  enabled: true,
  version: 1,
}

app.disable('x-powered-by')
app.use(express.json({ limit: '2mb' }))

app.get('/health', (_req, res) => res.json({ ok: true, status: isShuttingDown ? 'shutting_down' : 'ready' }))

app.use('/sessions', (req, res, next) => {
  const received = req.get(bridgeSecretHeader)
  if (!isAuthorizedBridgeRequest(received)) return res.status(401).json({ error: 'Unauthorized.' })
  next()
})

app.param('tenantId', (req, res, next, tenantId) => {
  if (!sessionIdPattern.test(tenantId)) return res.status(400).json({ error: 'Invalid session id.' })
  next()
})

async function getSession(tenantId) {
  const existing = sessions.get(tenantId)
  const session = existing ?? {
    tenantId,
    status: 'connecting',
    qr: null,
    phoneNumber: null,
    sock: null,
    conversations: new Map(),
    messages: new Map(),
    seenMessageIds: new Set(),
    connecting: null,
  }

  await ensureSessionLease(tenantId, session)

  if (existing?.sock) return existing

  if (!existing) {
    await loadSnapshot(tenantId, session)
    sessions.set(tenantId, session)
  }

  if (session.sock) return session

  if (session.connecting) return session.connecting

  session.connecting = initializeSession(tenantId, session)
  try {
    await session.connecting
  } finally {
    session.connecting = null
  }

  return session
}

async function initializeSession(tenantId, session) {
  try {
    await restoreAuthState(tenantId)
    const { state, saveCreds } = await useMultiFileAuthState(sessionDirectory(tenantId))
    const setKeys = state.keys.set.bind(state.keys)
    state.keys.set = async (data) => {
      await setKeys(data)
      scheduleAuthBackup(tenantId)
    }
    const sock = makeWASocket({
      auth: state,
      browser: ['Mac OS', 'Chrome', '14.4.1'],
      printQRInTerminal: false,
      markOnlineOnConnect: false,
      keepAliveIntervalMs: 15_000,
    })

    session.sock = sock
    session.status = 'connecting'

    sock.ev.on('creds.update', () => {
      void saveCreds()
        .then(() => scheduleAuthBackup(tenantId))
        .catch((error) => logError('Failed to save WhatsApp credentials', tenantId, error))
    })
    sock.ev.on('connection.update', ({ connection, lastDisconnect, qr }) => {
      void handleConnectionUpdate(tenantId, session, sock, connection, lastDisconnect, qr)
        .catch((error) => logError('Failed to process WhatsApp connection update', tenantId, error))
    })

    sock.ev.on('messaging-history.set', ({ chats, contacts, messages }) => {
      const names = new Map((contacts ?? []).map((c) => [c.id, c.name || c.notify || c.verifiedName]))
      for (const chat of chats ?? []) upsertConversation(session, chat.id, names.get(chat.id), chat.conversationTimestamp)
      for (const message of messages ?? []) addMessage(session, message, false)
    })

    sock.ev.on('messages.upsert', ({ messages, type }) => {
      console.log(`WhatsApp messages received: session=${tenantId} type=${type} count=${messages?.length ?? 0}`)
      for (const message of messages ?? []) addMessage(session, message, type === 'notify')
    })
  } catch (error) {
    session.status = 'disconnected'
    session.sock = null
    logError('Failed to initialize WhatsApp session', tenantId, error)
    scheduleReconnect(tenantId)
  }

  return session
}

async function handleConnectionUpdate(tenantId, session, sock, connection, lastDisconnect, qr) {
  if (session.sock !== sock) return

      if (qr) {
        session.qr = qr
        session.status = 'qr_pending'
        console.log(`WhatsApp QR generated: session=${tenantId}`)
      }
      if (connection === 'open') {
        session.status = 'connected'
        session.qr = null
        session.phoneNumber = sock.user?.id?.split(':')[0] ?? null
        clearReconnect(tenantId)
        scheduleAuthBackup(tenantId)
        console.log(`WhatsApp session connected: session=${tenantId}`)
      }
      if (connection === 'close') {
        const code = lastDisconnect?.error?.output?.statusCode
        const shouldReconnect = code !== DisconnectReason.loggedOut
        session.status = shouldReconnect ? 'reconnecting' : 'disconnected'
        session.sock = null
        console.error(`WhatsApp session closed: session=${tenantId} code=${code ?? 'unknown'} reconnect=${shouldReconnect}`)

        if (code === DisconnectReason.loggedOut) {
          clearReconnect(tenantId)
          await releaseSessionLease(tenantId)
          sessions.delete(tenantId)
          await rm(sessionDirectory(tenantId), { recursive: true, force: true })
          await deleteRemoteAuthState(tenantId)
          return
        }

        if (shouldReconnect) scheduleReconnect(tenantId)
      }
}

app.get('/sessions/:tenantId/qr', withSessionOwnership(async (req, res) => {
  const session = await getSession(req.params.tenantId)
  if (!session.qr) return res.status(202).json({ status: session.status })

  const dataUrl = await QRCode.toDataURL(session.qr, { margin: 1, width: 320 })
  res.json({
    status: session.status,
    qrCode: dataUrl.replace(/^data:image\/png;base64,/, ''),
    qrCodeData: session.qr,
  })
}))

app.get('/sessions/:tenantId/status', withSessionOwnership(async (req, res) => {
  let session = sessions.get(req.params.tenantId)
  // If no in-memory session exists, trigger reconnect so creds are reused on restart
  if (!session) {
    session = await getSession(req.params.tenantId)
  }
  res.json({
    isConnected: session?.status === 'connected',
    status: session?.status ?? 'disconnected',
    phoneNumber: session?.phoneNumber ?? null,
  })
}))

app.get('/sessions/:tenantId/conversations', withSessionOwnership(async (req, res) => {
  const session = await getSession(req.params.tenantId)
  const items = Array.from(session.conversations.values())
    .sort((a, b) => new Date(b.lastMessageAt ?? 0) - new Date(a.lastMessageAt ?? 0))
  res.json({ items, nextCursor: null, hasMore: false })
}))

app.get('/sessions/:tenantId/conversations/:id/messages', withSessionOwnership(async (req, res) => {
  const session = await getSession(req.params.tenantId)
  const key = req.params.id.includes('@') ? encodeURIComponent(req.params.id) : req.params.id
  res.json({
    items: session.messages.get(key) ?? [],
    nextCursor: null,
    hasMore: false,
  })
}))

app.post('/sessions/:tenantId/logout', withSessionOwnership(async (req, res) => {
  const session = await getSession(req.params.tenantId)
  clearReconnect(req.params.tenantId)
  try {
    await session?.sock?.logout?.()
  } catch {
    // The local auth folder still must be cleared so the next request emits a fresh QR.
  }
  sessions.delete(req.params.tenantId)
  await rm(sessionDirectory(req.params.tenantId), { recursive: true, force: true })
  await deleteRemoteAuthState(req.params.tenantId)
  await releaseSessionLease(req.params.tenantId)
  res.json({ ok: true })
}))

app.post('/sessions/:tenantId/send-message', withSessionOwnership(async (req, res) => {
  const session = await getSession(req.params.tenantId)
  const { recipientPhone, text } = req.body ?? {}
  if (!session?.sock || !isValidRecipient(recipientPhone) || !isValidMessageText(text)) {
    return res.status(400).json({ success: false, error: 'Session, recipientPhone and text are required.' })
  }

  try {
    const lidJid = `${recipientPhone}@lid`
    const phoneJid = `${recipientPhone}@s.whatsapp.net`
    const recipientJid = session.conversations.has(lidJid) ? lidJid : phoneJid
    const result = await session.sock.sendMessage(recipientJid, { text })
    res.json({ success: true, messageId: result?.key?.id ?? `bridge-${Date.now()}` })
  } catch {
    res.status(502).json({ success: false, error: 'WhatsApp Web message could not be sent.' })
  }
}))

app.get('/sessions/:tenantId/bot-config', async (req, res) => {
  res.json(await getBotConfig(req.params.tenantId))
})

app.post('/sessions/:tenantId/bot-config', async (req, res) => {
  const current = await getBotConfig(req.params.tenantId)
  const next = { ...current, ...req.body, configured: true, version: current.version + 1 }
  botConfigs.set(req.params.tenantId, next)
  await saveBotConfig(req.params.tenantId, next)
  const session = sessions.get(req.params.tenantId)
  if (next.enabled && session) {
    for (const conv of session.conversations.values()) conv.mode = 'Automatic'
    await saveSnapshot(session)
  }
  res.json(next)
})

const server = app.listen(port, () => {
  console.log(`WhatsApp Web service listening on http://localhost:${port}`)
})

function upsertConversation(session, jid, name, timestamp) {
  if (!jid || jid.endsWith('@g.us') || jid === 'status@broadcast') return
  const existing = session.conversations.get(jid)
  session.conversations.set(jid, {
    id: encodeURIComponent(jid),
    contactId: jid,
    contactName: name || existing?.contactName || jid.split('@')[0],
    contactPhone: jid.split('@')[0],
    mode: existing?.mode ?? 'Automatic',
    status: 'Open',
    lastMessage: existing?.lastMessage,
    lastMessageAt: timestamp ? new Date(Number(timestamp) * 1000).toISOString() : existing?.lastMessageAt,
    isWindowOpen: true,
  })
}

function addMessage(session, msg, isLiveInbound = false) {
  const jid = msg.key?.remoteJid
  if (!jid || jid.endsWith('@g.us') || jid === 'status@broadcast') return

  const messageId = msg.key?.id
  if (messageId) {
    if (!msg.key?.fromMe && session.seenMessageIds.has(messageId)) {
      console.log(`Duplicate inbound message ignored: session=${session.tenantId}`)
      return
    }
    if (!msg.key?.fromMe) {
      session.seenMessageIds.add(messageId)
    }
  }

  const text =
    msg.message?.conversation ||
    msg.message?.extendedTextMessage?.text ||
    msg.message?.imageMessage?.caption ||
    msg.message?.videoMessage?.caption ||
    '[midia]'

  const createdAt = new Date(Number(msg.messageTimestamp ?? Date.now() / 1000) * 1000).toISOString()
  upsertConversation(session, jid, msg.pushName, Number(msg.messageTimestamp ?? Date.now() / 1000))

  const conv = session.conversations.get(jid)
  if (conv) {
    conv.lastMessage = text
    conv.lastMessageAt = createdAt
  }

  const key = encodeURIComponent(jid)
  const list = session.messages.get(key) ?? []
  list.push({
    id: messageId ?? `${key}-${Date.now()}`,
    direction: msg.key?.fromMe ? 'Outbound' : 'Inbound',
    status: 'Read',
    type: 'Text',
    content: text,
    createdAt,
    senderName: msg.pushName,
  })
  session.messages.set(key, list)
  void saveSnapshot(session)
  if (!msg.key?.fromMe && isLiveInbound) {
    void forwardInboundMessage(session, msg, text, createdAt)
  }
}

async function forwardInboundMessage(session, msg, text, createdAt) {
  const match = session.tenantId.match(/^(.+)-qr-(\d+)$/)
  if (!match || !msg.key?.id) return

  const [, tenantId, lineNumber] = match
  const payload = {
    object: 'whatsapp_business_account',
    entry: [{
      id: `whatsapp-web-${tenantId}-${lineNumber}`,
      time: Math.floor(new Date(createdAt).getTime() / 1000),
      changes: [{
        field: 'messages',
        value: {
          messaging_product: 'whatsapp',
          metadata: {
            phone_number_id: `qr:${tenantId}:${lineNumber}`,
          },
          contacts: [{
            wa_id: msg.key.remoteJid?.split('@')[0],
            profile: { name: msg.pushName },
          }],
          messages: [{
            from: msg.key.remoteJid?.split('@')[0],
            id: msg.key.id,
            timestamp: Math.floor(new Date(createdAt).getTime() / 1000),
            type: msg.message?.conversation || msg.message?.extendedTextMessage?.text ? 'text' : 'image',
            text: text !== '[midia]' ? { body: text } : undefined,
          }],
        },
      }],
    }],
  }

  for (let attempt = 1; attempt <= 5; attempt += 1) {
    try {
      const response = await fetchWithTimeout(apiWebhookUrl, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'X-WhatsApp-Web-Secret': apiWebhookSecret,
        },
        body: JSON.stringify(payload),
      })
      if (response.ok) {
        console.log(`Inbound message forwarded for ${session.tenantId}: HTTP ${response.status}`)
        return
      }
      if (response.status === 409) return
      throw new Error(`Webhook returned HTTP ${response.status}`)
    } catch (error) {
      if (attempt === 5) {
        logError('Failed to forward WhatsApp Web message', session.tenantId, error)
        return
      }
      await new Promise((resolve) => setTimeout(resolve, attempt * 2000))
    }
  }
}

async function sendAutoReply(session, jid, inboundText) {
  const config = await getBotConfig(session.tenantId)
  const conv = session.conversations.get(jid)
  if (!config.enabled || config.mode === 'Manual' || conv?.mode !== 'Automatic') return

  const content = buildBotReply(session, jid, inboundText, config)
  if (!content) return

  await session.sock?.sendMessage(jid, { text: content })
  const createdAt = new Date().toISOString()
  const key = encodeURIComponent(jid)
  const list = session.messages.get(key) ?? []
  list.push({
    id: `bot-${Date.now()}`,
    direction: 'Outbound',
    status: 'Sent',
    type: 'Text',
    content,
    createdAt,
    senderName: 'Bot',
  })
  session.messages.set(key, list)
  if (conv) {
    conv.lastMessage = content
    conv.lastMessageAt = createdAt
  }
  await saveSnapshot(session)
}

function buildBotReply(session, jid, text, config) {
  const clean = String(text || '').trim()
  const key = encodeURIComponent(jid)
  const list = session.messages.get(key) ?? []
  const inboundCount = list.filter((message) => message.direction === 'Inbound').length
  const alreadyWelcomed = list.some((message) => message.direction === 'Outbound' && message.senderName === 'Bot')
  if (/humano|atendente|gerente|pessoa/i.test(clean)) {
    const conv = session.conversations.get(jid)
    if (conv) conv.mode = 'Human'
    return config.handoffMessage
  }
  if (clean === '[midia]') return config.mediaMessage || config.fallbackMessage
  if (!alreadyWelcomed) {
    return inboundCount <= 1 ? config.welcomeMessage : (config.returningMessage || config.welcomeMessage)
  }
  const step = (config.flowSteps ?? []).find((item) => {
    const words = String(item.keywords ?? '').split(',').map((word) => word.trim()).filter(Boolean)
    return words.some((word) => clean.toLowerCase().includes(word.toLowerCase()))
  })
  if (step?.response) return step.response
  return config.fallbackMessage
}

async function loadSnapshot(tenantId, session) {
  try {
    const raw = await readFile(`${sessionDirectory(tenantId)}/inbox.json`, 'utf8')
    const data = JSON.parse(raw)
    session.conversations = new Map(data.conversations ?? [])
    session.messages = new Map(data.messages ?? [])
  } catch {}
}

async function saveSnapshot(session) {
  const tenantId = session.tenantId
  await mkdir(sessionDirectory(tenantId), { recursive: true })
  await writeFile(`${sessionDirectory(tenantId)}/inbox.json`, JSON.stringify({
    conversations: Array.from(session.conversations.entries()),
    messages: Array.from(session.messages.entries()),
  }))
}

async function getBotConfig(tenantId) {
  if (botConfigs.has(tenantId)) return botConfigs.get(tenantId)
  try {
    const raw = await readFile(`${sessionDirectory(tenantId)}/bot-config.json`, 'utf8')
    const config = { ...defaultBotConfig, ...JSON.parse(raw) }
    botConfigs.set(tenantId, config)
    return config
  } catch {
    botConfigs.set(tenantId, defaultBotConfig)
    return defaultBotConfig
  }
}

async function saveBotConfig(tenantId, config) {
  await mkdir(sessionDirectory(tenantId), { recursive: true })
  await writeFile(`${sessionDirectory(tenantId)}/bot-config.json`, JSON.stringify(config))
}

function sessionDirectory(tenantId) {
  return `sessions/${tenantId}`
}

function sessionStateUrl(tenantId) {
  return `${apiWebhookUrl}/session/${encodeURIComponent(tenantId)}`
}

class SessionOwnedElsewhereError extends Error {
  constructor(ownerUrl) {
    super('WhatsApp session is owned by another bridge instance.')
    this.ownerUrl = ownerUrl
  }
}

function normalizeInstanceUrl(value) {
  const url = new URL(value)
  if (!['http:', 'https:'].includes(url.protocol) || url.username || url.password || url.search || url.hash) {
    throw new Error('WHATSAPP_WEB_INSTANCE_URL must be an absolute HTTP(S) base URL.')
  }
  return url.origin
}

async function ensureSessionLease(tenantId, session) {
  const response = await fetchWithTimeout(`${sessionStateUrl(tenantId)}/lease`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ instanceId, instanceUrl }),
  })

  if (response.status === 409) {
    const body = await response.json().catch(() => ({}))
    throw new SessionOwnedElsewhereError(typeof body.ownerUrl === 'string' ? body.ownerUrl : null)
  }
  if (!response.ok) throw new Error(`Could not acquire WhatsApp session lease: HTTP ${response.status}`)

  const body = await response.json()
  const expiresAt = new Date(body.expiresAt).getTime()
  if (!Number.isFinite(expiresAt)) throw new Error('Bridge lease response is invalid.')

  session.leaseExpiresAt = expiresAt
  scheduleLeaseRenewal(tenantId, session)
}

function scheduleLeaseRenewal(tenantId, session) {
  const current = leaseRenewTimers.get(tenantId)
  if (current) clearTimeout(current)

  const delay = Math.max(1_000, session.leaseExpiresAt - Date.now() - 15_000)
  const timer = setTimeout(() => {
    leaseRenewTimers.delete(tenantId)
    void ensureSessionLease(tenantId, session).catch((error) => {
      stopSessionAfterLeaseLoss(tenantId, session, error)
    })
  }, delay)
  leaseRenewTimers.set(tenantId, timer)
}

function clearLeaseRenewal(tenantId) {
  const timer = leaseRenewTimers.get(tenantId)
  if (timer) clearTimeout(timer)
  leaseRenewTimers.delete(tenantId)
}

function stopSessionAfterLeaseLoss(tenantId, session, error) {
  clearLeaseRenewal(tenantId)
  session.status = 'disconnected'
  session.sock?.end?.(new Error('QR session lease was lost.'))
  session.sock = null
  if (!(error instanceof SessionOwnedElsewhereError)) {
    logError('Failed to renew WhatsApp session lease', tenantId, error)
  }
}

async function releaseSessionLease(tenantId) {
  clearLeaseRenewal(tenantId)
  try {
    const response = await fetchWithTimeout(`${sessionStateUrl(tenantId)}/lease`, { method: 'DELETE' })
    if (!response.ok && response.status !== 404) throw new Error(`HTTP ${response.status}`)
  } catch (error) {
    logError('Failed to release WhatsApp session lease', tenantId, error)
  }
}

function withSessionOwnership(handler) {
  return async (req, res) => {
    try {
      await handler(req, res)
    } catch (error) {
      if (error instanceof SessionOwnedElsewhereError) {
        return res.status(409).json({ ownerUrl: error.ownerUrl })
      }
      logError('WhatsApp session request failed', req.params.tenantId ?? 'unknown', error)
      return res.status(503).json({ error: 'WhatsApp Web session is unavailable.' })
    }
  }
}

function scheduleAuthBackup(tenantId) {
  const existing = authBackupTimers.get(tenantId)
  if (existing) clearTimeout(existing)

  const timer = setTimeout(() => {
    authBackupTimers.delete(tenantId)
    const previous = authBackupPromises.get(tenantId) ?? Promise.resolve()
    const current = previous.then(() => backupAuthState(tenantId))
    authBackupPromises.set(tenantId, current)
    void current.finally(() => {
      if (authBackupPromises.get(tenantId) === current) authBackupPromises.delete(tenantId)
    })
  }, 1500)
  authBackupTimers.set(tenantId, timer)
}

async function backupAuthState(tenantId) {
  try {
    const directory = sessionDirectory(tenantId)
    const entries = await readdir(directory, { withFileTypes: true })
    const files = {}
    for (const entry of entries) {
      if (!entry.isFile() || entry.name === 'inbox.json' || entry.name === 'bot-config.json') continue
      files[entry.name] = (await readFile(`${directory}/${entry.name}`)).toString('base64')
    }
    if (!files['creds.json']) return

    const compressed = await gzipAsync(Buffer.from(JSON.stringify(files)))
    const payload = compressed.toString('base64')
    if (lastAuthPayloads.get(tenantId) === payload) return

    const response = await fetchWithTimeout(sessionStateUrl(tenantId), {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json',
        'X-WhatsApp-Web-Secret': apiWebhookSecret,
      },
      body: JSON.stringify({ payload }),
    })
    if (!response.ok) throw new Error(`HTTP ${response.status}`)
    lastAuthPayloads.set(tenantId, payload)
  } catch (error) {
    logError('Failed to persist WhatsApp auth state', tenantId, error)
  }
}

async function restoreAuthState(tenantId) {
  try {
    await readFile(`${sessionDirectory(tenantId)}/creds.json`)
    return
  } catch {}

  try {
    const response = await fetchWithTimeout(sessionStateUrl(tenantId), {
      headers: { 'X-WhatsApp-Web-Secret': apiWebhookSecret },
    })
    if (response.status === 404) return
    if (!response.ok) throw new Error(`HTTP ${response.status}`)

    const { payload } = await response.json()
    if (typeof payload !== 'string' || !payload) throw new Error('Invalid auth payload')
    const raw = await gunzipAsync(Buffer.from(payload, 'base64'))
    const files = JSON.parse(raw.toString('utf8'))
    await mkdir(sessionDirectory(tenantId), { recursive: true })
    for (const [name, content] of Object.entries(files)) {
      if (name.includes('/') || name.includes('\\') || name === '.' || name === '..') continue
      await writeFile(`${sessionDirectory(tenantId)}/${name}`, Buffer.from(content, 'base64'))
    }
    lastAuthPayloads.set(tenantId, payload)
    console.log(`WhatsApp auth state restored: session=${tenantId}`)
  } catch (error) {
    logError('Failed to restore WhatsApp auth state', tenantId, error)
  }
}

async function deleteRemoteAuthState(tenantId) {
  lastAuthPayloads.delete(tenantId)
  try {
    const response = await fetchWithTimeout(sessionStateUrl(tenantId), {
      method: 'DELETE',
      headers: { 'X-WhatsApp-Web-Secret': apiWebhookSecret },
    })
    if (!response.ok && response.status !== 404) throw new Error(`HTTP ${response.status}`)
  } catch (error) {
    logError('Failed to delete WhatsApp auth state', tenantId, error)
  }
}

function isAuthorizedBridgeRequest(received) {
  if (!received) return false
  const expectedBytes = Buffer.from(apiWebhookSecret)
  const receivedBytes = Buffer.from(received)
  return expectedBytes.length === receivedBytes.length && timingSafeEqual(expectedBytes, receivedBytes)
}

function isValidRecipient(recipientPhone) {
  return typeof recipientPhone === 'string' && /^\d{7,20}$/.test(recipientPhone)
}

function isValidMessageText(text) {
  return typeof text === 'string' && text.trim().length > 0 && text.length <= 4096
}

function fetchWithTimeout(url, options = {}) {
  const headers = new Headers(options.headers)
  headers.set('X-WhatsApp-Web-Secret', apiWebhookSecret)
  headers.set('X-WhatsApp-Web-Instance', instanceId)
  return fetch(url, { ...options, headers, signal: AbortSignal.timeout(15_000) })
}

function clearReconnect(tenantId) {
  const timer = reconnectTimers.get(tenantId)
  if (timer) clearTimeout(timer)
  reconnectTimers.delete(tenantId)
  reconnectAttempts.delete(tenantId)
}

function scheduleReconnect(tenantId) {
  if (isShuttingDown || reconnectTimers.has(tenantId)) return
  const attempts = (reconnectAttempts.get(tenantId) ?? 0) + 1
  reconnectAttempts.set(tenantId, attempts)
  const delay = Math.min(30_000, 1_000 * 2 ** Math.min(attempts - 1, 5)) + Math.floor(Math.random() * 500)
  const timer = setTimeout(() => {
    reconnectTimers.delete(tenantId)
    void getSession(tenantId).catch((error) => logError('Scheduled WhatsApp reconnect failed', tenantId, error))
  }, delay)
  reconnectTimers.set(tenantId, timer)
}

function logError(event, tenantId, error) {
  const errorType = error instanceof Error ? error.name : 'UnknownError'
  console.error(`${event}: session=${tenantId} error=${errorType}`)
}

async function flushAuthBackups() {
  for (const timer of authBackupTimers.values()) clearTimeout(timer)
  authBackupTimers.clear()
  await Promise.all([...authBackupPromises.values()])
  await Promise.all([...sessions.keys()].map((tenantId) => backupAuthState(tenantId)))
}

async function shutdown(signal, exitCode = 0) {
  if (isShuttingDown) return
  isShuttingDown = true
  console.log(`WhatsApp Web service shutting down: signal=${signal}`)
  for (const tenantId of [...reconnectTimers.keys()]) clearReconnect(tenantId)
  await flushAuthBackups()
  for (const session of sessions.values()) session.sock?.end?.(new Error('Service shutdown'))
  await Promise.all([...sessions.keys()].map((tenantId) => releaseSessionLease(tenantId)))
  await new Promise((resolve) => server.close(resolve))
  process.exitCode = exitCode
}

process.once('SIGTERM', () => { void shutdown('SIGTERM') })
process.once('SIGINT', () => { void shutdown('SIGINT') })
process.once('uncaughtException', (error) => {
  logError('Uncaught bridge exception', 'system', error)
  void shutdown('uncaughtException', 1)
})
process.once('unhandledRejection', (error) => {
  logError('Unhandled bridge rejection', 'system', error)
  void shutdown('unhandledRejection', 1)
})

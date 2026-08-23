import express from 'express'
import { mkdir, readFile, rm, writeFile } from 'node:fs/promises'
import QRCode from 'qrcode'
import makeWASocket, { DisconnectReason, useMultiFileAuthState } from '@whiskeysockets/baileys'

const app = express()
const port = Number(process.env.PORT ?? 3020)
const apiWebhookUrl = process.env.WHATSAPP_WEB_API_URL ?? 'http://localhost:5000/api/webhooks/whatsapp-web'
const apiWebhookSecret = process.env.WHATSAPP_WEB_WEBHOOK_SECRET ?? 'development-whatsapp-web-secret'
const sessions = new Map()
const botConfigs = new Map()

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

app.use(express.json())

app.use((req, res, next) => {
  res.setHeader('Access-Control-Allow-Origin', '*')
  res.setHeader('Access-Control-Allow-Methods', 'GET,POST,OPTIONS')
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type')
  if (req.method === 'OPTIONS') return res.sendStatus(204)
  next()
})

async function getSession(tenantId) {
  const existing = sessions.get(tenantId)
  if (existing) return existing

  const { state, saveCreds } = await useMultiFileAuthState(`sessions/${tenantId}`)
  const session = {
    tenantId,
    status: 'connecting',
    qr: null,
    phoneNumber: null,
    sock: null,
    conversations: new Map(),
    messages: new Map(),
  }
  await loadSnapshot(tenantId, session)
  sessions.set(tenantId, session)

  const sock = makeWASocket({
    auth: state,
    browser: ['Mac OS', 'Chrome', '14.4.1'],
    printQRInTerminal: false,
    markOnlineOnConnect: false,
  })
  session.sock = sock

  sock.ev.on('creds.update', saveCreds)
  sock.ev.on('connection.update', async ({ connection, lastDisconnect, qr }) => {
    if (qr) {
      session.qr = qr
      session.status = 'qr_pending'
    }
    if (connection === 'open') {
      session.status = 'connected'
      session.qr = null
      session.phoneNumber = sock.user?.id?.split(':')[0] ?? null
    }
    if (connection === 'close') {
      const code = lastDisconnect?.error?.output?.statusCode
      session.status = 'disconnected'
      session.sock = null
      sessions.delete(tenantId)
      if (code === DisconnectReason.loggedOut) {
        await rm(`sessions/${tenantId}`, { recursive: true, force: true })
      }
    }
  })

  sock.ev.on('messaging-history.set', ({ chats, contacts, messages }) => {
    const names = new Map((contacts ?? []).map((c) => [c.id, c.name || c.notify || c.verifiedName]))
    for (const chat of chats ?? []) upsertConversation(session, chat.id, names.get(chat.id), chat.conversationTimestamp)
    for (const message of messages ?? []) addMessage(session, message, false)
  })

  sock.ev.on('messages.upsert', ({ messages, type }) => {
    for (const message of messages ?? []) addMessage(session, message, type === 'notify')
  })

  return session
}

app.get('/health', (_req, res) => res.json({ ok: true }))

app.get('/sessions/:tenantId/qr', async (req, res) => {
  const session = await getSession(req.params.tenantId)
  if (!session.qr) return res.status(202).json({ status: session.status })

  const dataUrl = await QRCode.toDataURL(session.qr, { margin: 1, width: 320 })
  res.json({
    status: session.status,
    qrCode: dataUrl.replace(/^data:image\/png;base64,/, ''),
    qrCodeData: session.qr,
  })
})

app.get('/sessions/:tenantId/status', async (req, res) => {
  const session = sessions.get(req.params.tenantId)
  res.json({
    isConnected: session?.status === 'connected',
    status: session?.status ?? 'disconnected',
    phoneNumber: session?.phoneNumber ?? null,
  })
})

app.get('/sessions/:tenantId/conversations', async (req, res) => {
  const session = await getSession(req.params.tenantId)
  const items = Array.from(session.conversations.values())
    .sort((a, b) => new Date(b.lastMessageAt ?? 0) - new Date(a.lastMessageAt ?? 0))
  res.json({ items, nextCursor: null, hasMore: false })
})

app.get('/sessions/:tenantId/conversations/:id/messages', async (req, res) => {
  const session = await getSession(req.params.tenantId)
  const key = req.params.id.includes('@') ? encodeURIComponent(req.params.id) : req.params.id
  res.json({
    items: session.messages.get(key) ?? [],
    nextCursor: null,
    hasMore: false,
  })
})

app.post('/sessions/:tenantId/logout', async (req, res) => {
  const session = sessions.get(req.params.tenantId)
  try {
    await session?.sock?.logout?.()
  } catch {
    // The local auth folder still must be cleared so the next request emits a fresh QR.
  }
  sessions.delete(req.params.tenantId)
  await rm(`sessions/${req.params.tenantId}`, { recursive: true, force: true })
  res.json({ ok: true })
})

app.post('/sessions/:tenantId/send-message', async (req, res) => {
  const session = sessions.get(req.params.tenantId)
  const { recipientPhone, text } = req.body ?? {}
  if (!session?.sock || !recipientPhone || !text) {
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
})

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

app.listen(port, () => {
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

function addMessage(session, msg, shouldAutoReply = false) {
  const jid = msg.key?.remoteJid
  if (!jid || jid.endsWith('@g.us') || jid === 'status@broadcast') return

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
    id: msg.key?.id ?? `${key}-${Date.now()}`,
    direction: msg.key?.fromMe ? 'Outbound' : 'Inbound',
    status: 'Read',
    type: 'Text',
    content: text,
    createdAt,
    senderName: msg.pushName,
  })
  session.messages.set(key, list)
  void saveSnapshot(session)
  if (!msg.key?.fromMe) {
    void forwardInboundMessage(session, msg, text, createdAt)
  }
  if (!msg.key?.fromMe && shouldAutoReply && Date.now() >= session.acceptInboundAt) {
    void sendAutoReply(session, jid, text)
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
      const response = await fetch(apiWebhookUrl, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'X-WhatsApp-Web-Secret': apiWebhookSecret,
        },
        body: JSON.stringify(payload),
      })
      if (response.ok) return
      throw new Error(`Webhook returned HTTP ${response.status}`)
    } catch (error) {
      if (attempt === 5) {
        console.error('Failed to forward WhatsApp Web message', error)
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
    const raw = await readFile(`sessions/${tenantId}/inbox.json`, 'utf8')
    const data = JSON.parse(raw)
    session.conversations = new Map(data.conversations ?? [])
    session.messages = new Map(data.messages ?? [])
  } catch {}
}

async function saveSnapshot(session) {
  const tenantId = session.tenantId
  await mkdir(`sessions/${tenantId}`, { recursive: true })
  await writeFile(`sessions/${tenantId}/inbox.json`, JSON.stringify({
    conversations: Array.from(session.conversations.entries()),
    messages: Array.from(session.messages.entries()),
  }))
}

async function getBotConfig(tenantId) {
  if (botConfigs.has(tenantId)) return botConfigs.get(tenantId)
  try {
    const raw = await readFile(`sessions/${tenantId}/bot-config.json`, 'utf8')
    const config = { ...defaultBotConfig, ...JSON.parse(raw) }
    botConfigs.set(tenantId, config)
    return config
  } catch {
    botConfigs.set(tenantId, defaultBotConfig)
    return defaultBotConfig
  }
}

async function saveBotConfig(tenantId, config) {
  await mkdir(`sessions/${tenantId}`, { recursive: true })
  await writeFile(`sessions/${tenantId}/bot-config.json`, JSON.stringify(config))
}

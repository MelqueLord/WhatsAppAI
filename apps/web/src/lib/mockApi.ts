import * as mocks from './mocks'

const delay = (ms: number) => new Promise((r) => setTimeout(r, ms))

const routes: Record<string, (req: Request, params?: Record<string, string>) => Promise<any>> = {
  // Auth
  'GET /api/auth/me': async () => mocks.mockUser,
  'POST /api/auth/login': async () => mocks.mockUser,
  'POST /api/auth/logout': async () => ({}),

  // Conversations
  'GET /api/conversations': async () => ({
    items: mocks.mockConversations,
    nextCursor: null,
    hasMore: false,
  }),

  // Messages (dynamic)
  'GET /api/conversations/:id/messages': async (_req, params) => ({
    items: mocks.mockMessages[params!.id] || [],
    nextCursor: null,
    hasMore: false,
  }),

  'POST /api/conversations/:id/messages': async (req, params) => {
    const body = await req.json().catch(() => ({}))
    const msgs = mocks.mockMessages[params!.id] || []
    const newMsg = {
      id: `m-new-${Date.now()}`,
      direction: 'Outbound',
      status: 'Sent',
      type: 'Text',
      content: body.content || '',
      createdAt: new Date().toISOString(),
    }
    msgs.push(newMsg)
    mocks.mockMessages[params!.id] = msgs
    return { id: newMsg.id, status: 'Sent' }
  },

  'PUT /api/conversations/:id/mode': async (req, params) => {
    const body = await req.json().catch(() => ({}))
    const conv = mocks.mockConversations.find((c) => c.id === params!.id)
    if (conv) {
      conv.mode = body.mode || conv.mode
      conv.version++
    }
    return { id: params!.id, mode: body.mode, version: conv?.version || 1 }
  },

  // Operators
  'GET /api/operators': async () => mocks.mockOperators,
  'POST /api/operators': async () => ({ id: 'op-new', invitationLink: 'https://example.com/activate?invitation=inv-123&token=abc' }),
  'POST /api/operators/:id/deactivate': async () => ({ success: true }),
  'POST /api/operators/:id/reactivate': async () => ({ success: true }),
  'POST /api/operators/:id/resend-invite': async () => ({ invitationLink: 'https://example.com/activate?invitation=inv-456&token=def' }),

  // Knowledge
  'GET /api/knowledge': async () => mocks.mockKnowledge,
  'POST /api/knowledge': async (req) => {
    const body = await req.json().catch(() => ({}))
    const item = { id: `k-${Date.now()}`, ...body, isActive: true, version: 1 }
    mocks.mockKnowledge.push(item)
    return { id: item.id, version: 1 }
  },
  'PUT /api/knowledge/:id': async (req, params) => {
    const body = await req.json().catch(() => ({}))
    const item = mocks.mockKnowledge.find((k) => k.id === params!.id)
    if (item) {
      item.title = body.title ?? item.title
      item.content = body.content ?? item.content
      item.priority = body.priority ?? item.priority
      item.version++
    }
    return { version: item?.version || 1 }
  },
  'POST /api/knowledge/:id/deactivate': async (_req, params) => {
    const item = mocks.mockKnowledge.find((k) => k.id === params!.id)
    if (item) { item.isActive = false; item.version++ }
    return { version: item?.version || 1, isActive: false }
  },
  'POST /api/knowledge/:id/reactivate': async (_req, params) => {
    const item = mocks.mockKnowledge.find((k) => k.id === params!.id)
    if (item) { item.isActive = true; item.version++ }
    return { version: item?.version || 1, isActive: true }
  },

  // AI Config
  'GET /api/integrations/ai': async () => mocks.mockAiConfig,
  'POST /api/integrations/ai': async () => ({ saved: true }),
  'POST /api/integrations/ai/test-connection': async () => ({ success: true, model: 'gpt-4o-mini', inputTokens: 12, outputTokens: 3 }),

  // WhatsApp Config
  'GET /api/integrations/whatsapp': async () => mocks.mockWhatsAppConfig,
  'POST /api/integrations/whatsapp': async () => ({ saved: true }),
  'POST /api/integrations/whatsapp/test-connection': async () => ({ success: true, phoneNumber: '+55 11 99999-0000', qualityRating: 'GREEN' }),

  // Usage
  'GET /api/usage': async () => mocks.mockUsage,

  // Admin
  'GET /api/admin/tenants': async () => mocks.mockTenants,
  'POST /api/admin/tenants': async (req) => {
    const body = await req.json().catch(() => ({}))
    const tenant = { id: `t-${Date.now()}`, name: body.name, status: 'Active', createdAt: new Date().toISOString(), version: 1 }
    mocks.mockTenants.push(tenant)
    return { id: tenant.id, invitationLink: 'https://example.com/activate?invitation=inv-new&token=xyz' }
  },
  'POST /api/admin/tenants/:id/suspend': async () => ({ success: true }),
  'POST /api/admin/tenants/:id/reactivate': async () => ({ success: true }),

  // Webhook Events
  'GET /api/webhook-events': async () => mocks.mockWebhookEvents,

  // Client Tags
  'GET /api/client-tags': async () => mocks.mockClientTags,
  'POST /api/client-tags': async (req) => {
    const body = await req.json().catch(() => ({}))
    const tag = { id: `tag-${Date.now()}`, name: body.name, color: body.color, description: body.description, isActive: true }
    mocks.mockClientTags.push(tag)
    return { id: tag.id }
  },
  'PUT /api/client-tags/:id': async (req, params) => {
    const body = await req.json().catch(() => ({}))
    const tag = mocks.mockClientTags.find((t) => t.id === params!.id)
    if (tag) {
      tag.name = body.name ?? tag.name
      tag.color = body.color ?? tag.color
      tag.description = body.description ?? tag.description
    }
    return { id: tag?.id }
  },
  'POST /api/client-tags/:id/deactivate': async (_req, params) => {
    const tag = mocks.mockClientTags.find((t) => t.id === params!.id)
    if (tag) tag.isActive = false
    return { id: tag?.id, isActive: false }
  },
  'POST /api/client-tags/:id/reactivate': async (_req, params) => {
    const tag = mocks.mockClientTags.find((t) => t.id === params!.id)
    if (tag) tag.isActive = true
    return { id: tag?.id, isActive: true }
  },

  // Contact Tags
  'GET /api/client-tags/contacts/:contactId/tags': async (_req, params) => {
    const tagIds = mocks.mockContactTags[params!.contactId] || []
    return mocks.mockClientTags.filter((t) => tagIds.includes(t.id))
  },
  'POST /api/client-tags/contacts/:contactId/tags/:tagId': async (_req, params) => {
    if (!mocks.mockContactTags[params!.contactId]) mocks.mockContactTags[params!.contactId] = []
    if (!mocks.mockContactTags[params!.contactId].includes(params!.tagId)) {
      mocks.mockContactTags[params!.contactId].push(params!.tagId)
    }
    return { assigned: true }
  },
  'DELETE /api/client-tags/contacts/:contactId/tags/:tagId': async (_req, params) => {
    if (mocks.mockContactTags[params!.contactId]) {
      mocks.mockContactTags[params!.contactId] = mocks.mockContactTags[params!.contactId].filter((t) => t !== params!.tagId)
    }
    return { removed: true }
  },

  // Bot Configuration
  'GET /api/bot-config': async () => mocks.mockBotConfig,
  'POST /api/bot-config': async (req) => {
    const body = await req.json().catch(() => ({}))
    mocks.mockBotConfig = { ...mocks.mockBotConfig, ...body, configured: true, version: mocks.mockBotConfig.version + 1 }
    return { saved: true }
  },
  'PUT /api/bot-config/mode': async (req) => {
    const body = await req.json().catch(() => ({}))
    mocks.mockBotConfig.mode = body.mode || mocks.mockBotConfig.mode
    return { mode: mocks.mockBotConfig.mode }
  },
  'PUT /api/bot-config/messages': async (req) => {
    const body = await req.json().catch(() => ({}))
    mocks.mockBotConfig.welcomeMessage = body.welcomeMessage ?? mocks.mockBotConfig.welcomeMessage
    mocks.mockBotConfig.offlineMessage = body.offlineMessage ?? mocks.mockBotConfig.offlineMessage
    mocks.mockBotConfig.fallbackMessage = body.fallbackMessage ?? mocks.mockBotConfig.fallbackMessage
    return { saved: true }
  },
  'PUT /api/bot-config/tokens': async (req) => {
    const body = await req.json().catch(() => ({}))
    mocks.mockBotConfig.maxTokensPerResponse = body.maxTokens || mocks.mockBotConfig.maxTokensPerResponse
    return { maxTokensPerResponse: mocks.mockBotConfig.maxTokensPerResponse }
  },
  'POST /api/bot-config/toggle': async (req) => {
    const body = await req.json().catch(() => ({}))
    mocks.mockBotConfig.enabled = body.enabled ?? mocks.mockBotConfig.enabled
    return { enabled: mocks.mockBotConfig.enabled }
  },
}

function matchRoute(method: string, url: string): { handler: Function; params: Record<string, string> } | null {
  const cleanUrl = url.split('?')[0]

  for (const [key, handler] of Object.entries(routes)) {
    const [routeMethod, routePath] = key.split(' ')
    if (routeMethod !== method) continue

    const routeParts = routePath.split('/')
    const urlParts = cleanUrl.split('/')

    if (routeParts.length !== urlParts.length) continue

    const params: Record<string, string> = {}
    let match = true

    for (let i = 0; i < routeParts.length; i++) {
      if (routeParts[i].startsWith(':')) {
        params[routeParts[i].slice(1)] = urlParts[i]
      } else if (routeParts[i] !== urlParts[i]) {
        match = false
        break
      }
    }

    if (match) return { handler, params }
  }

  return null
}

export function setupMockApi() {
  const originalFetch = window.fetch

  window.fetch = async (input: RequestInfo | URL, init?: RequestInit): Promise<Response> => {
    const url = typeof input === 'string' ? input : input instanceof URL ? input.href : input.url
    const method = (init?.method || 'GET').toUpperCase()

    // Only intercept /api calls
    if (!url.startsWith('/api')) {
      return originalFetch(input, init)
    }

    const match = matchRoute(method, url)
    if (!match) {
      console.warn(`[Mock] No route: ${method} ${url}`)
      return new Response(JSON.stringify({ error: 'Not found' }), { status: 404 })
    }

    await delay(100 + Math.random() * 200) // Simulate network

    try {
      const req = new Request(url, init)
      const data = await match.handler(req, match.params)
      return new Response(JSON.stringify(data), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      })
    } catch (err) {
      return new Response(JSON.stringify({ error: 'Internal error' }), { status: 500 })
    }
  }

  console.log('[Mock] API interceptors active')
}

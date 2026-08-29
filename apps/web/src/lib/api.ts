const API_BASE = import.meta.env.VITE_API_URL ?? ''

const TOKEN_KEY = 'whatsappai.token'

export function getStoredToken(): string | null {
  return localStorage.getItem(TOKEN_KEY)
}

export function setStoredToken(token: string): void {
  localStorage.setItem(TOKEN_KEY, token)
}

export function clearStoredToken(): void {
  localStorage.removeItem(TOKEN_KEY)
}

// fetchWithCsrf kept for backward compat (used by SignalR hub connection, etc.)
export async function fetchWithCsrf(input: RequestInfo | URL, options: RequestInit = {}) {
  const requestUrl = typeof input === 'string' && input.startsWith('/')
    ? `${API_BASE}${input}`
    : input
  const headers = new Headers(options.headers)
  const token = getStoredToken()
  if (token) headers.set('Authorization', `Bearer ${token}`)
  return fetch(requestUrl, { ...options, headers })
}

async function fetchApi<T>(url: string, options?: RequestInit): Promise<T> {
  const token = getStoredToken()
  const headers: Record<string, string> = {}
  if (!(options?.body instanceof FormData)) headers['Content-Type'] = 'application/json'
  if (token) headers['Authorization'] = `Bearer ${token}`
  const response = await fetch(`${API_BASE}${url}`, {
    ...options,
    cache: options?.method ? undefined : 'no-store',
    headers: {
      ...headers,
      ...options?.headers as Record<string, string>,
    },
  })
  if (!response.ok) {
    if (response.status === 401) {
      // Login attempt → wrong credentials
      // Any other endpoint → session expired / not authenticated
      const isLoginEndpoint = url.includes('/api/auth/login')
      throw new Error(isLoginEndpoint ? 'INVALID_CREDENTIALS' : 'UNAUTHORIZED')
    }
    const error = await response.text()
    throw new Error(error || `HTTP ${response.status}`)
  }
  return response.json()
}

export interface ConversationTag {
  name: string
  color?: string | null
}

export interface Conversation {
  id: string
  contactId: string
  contactName: string
  contactPhone: string
  mode: string
  status: string
  version: number
  lastMessage?: string
  lastMessageAt?: string
  queueId?: string
  queueName?: string
  queueColor?: string
  isQrCode?: boolean
  isWindowOpen: boolean
  assignedToUserId?: string
  assignedToUserName?: string
  tags?: ConversationTag[]
}

export interface ServiceQueue {
  id: string
  name: string
  description?: string | null
  color?: string | null
  sortOrder: number
  isActive: boolean
}

export interface Message {
  id: string
  direction: string
  status: string
  type: string
  content?: string
  mediaId?: string
  caption?: string
  createdAt: string
  senderName?: string
}

export interface CursorPaginationResponse<T> {
  items: T[]
  nextCursor?: string
  hasMore: boolean
}

export interface User {
  id: string
  email: string
  displayName?: string
  tenantId?: string
  role?: 'PlatformAdmin' | 'TenantOwner' | 'Operator'
  isPlatformAdmin: boolean
  mustChangePassword: boolean
  planCode?: string
  aiEnabled?: boolean
  botEnabled?: boolean
  tagsEnabled?: boolean
  automaticDistributionEnabled?: boolean
  officialApiLineCount?: number
  qrCodeLineCount?: number
  operatorLimit?: number
  monthlyAiResponseLimit?: number | null
  monthlyAiResponsesUsed?: number
  dueDate?: string
  tenantStatus?: string
  assignedConnectionType?: string
  assignedLineNumber?: number
  assignedLines?: LineAssignment[]
  assignedQueueId?: string | null
}

export interface LineAssignment {
  connectionType: string
  lineNumber: number
}

export interface Operator {
  id: string
  userId: string
  email: string
  displayName?: string
  status: string
  createdAt: string
  deactivatedAt?: string
  reactivatedAt?: string
  assignedConnectionType?: string
  assignedLineNumber?: number
  assignedLines?: LineAssignment[]
  assignedQueueId?: string | null
}

export interface Tenant {
  id: string
  name: string
  slug: string
  planId: string
  status: string
  version: number
  createdAt: string
  dueDate: string
  lastPaymentAt?: string
  officialApiLineCount: number
  qrCodeLineCount: number
  operatorLimit: number
  monthlyAiResponseLimit?: number | null
  monthlyAiResponsesUsed: number
  ownerEmail?: string
  ownerDisplayName?: string
  suspendedAt?: string
  reactivatedAt?: string
  suspensionReason?: string
}

export interface AiQuotaAlert {
  action: string
  entityId?: string
  details?: string
  occurredAt: string
}

export interface Plan {
  id: string
  name: string
  code: string
  description?: string
  aiEnabled: boolean
  botEnabled: boolean
  tagsEnabled: boolean
  automaticDistributionEnabled: boolean
  isSelectable: boolean
  defaultOfficialApiLineCount: number
  defaultOperatorLimit: number
  defaultMonthlyAiResponseLimit?: number | null
  maxOperators?: number
}

export interface Contact {
  id: string
  phoneNumber: string
  name?: string
  profilePictureUrl?: string
  lastMessageAt?: string
  createdAt: string
  conversationId?: string
  message?: string
}

export interface ContactImportResult {
  total: number
  imported: number
  skipped: number
  invalid: number
  errors: Array<{ row: number; code: string; message: string }>
}

export interface CreateTenantResponse {
  tenantId: string
  tenantName: string
  slug: string
  ownerEmail: string
  ownerDisplayName?: string
  dueDate: string
  officialApiLineCount: number
  qrCodeLineCount: number
  operatorLimit: number
  monthlyAiResponseLimit?: number | null
  temporaryPassword: string
  message: string
}

export interface CapacityIndicator {
  current: number
  limit: number
  utilizationPercentage: number
  status: 'Normal' | 'Warning' | 'MigrationRequired'
}

export interface InfrastructureCapacity {
  customers: CapacityIndicator
  lines: CapacityIndicator
  operators: CapacityIndicator
  migrationRequired: boolean
}

export interface DashboardStats {
  operatorCount: number
  messagesToday: number
  activeConversations: number
}

export interface AiResponseQuota {
  limit: number | null
  used: number
  remaining: number | null
  utilizationPercentage: number | null
}

export interface UsageSummary {
  provider: string
  metric: string
  totalQuantity: number
  totalCostMinorUnits: number
  currency: string | null
  unit: string | null
  count: number
}

export interface UsageResponse {
  from: string
  to: string
  entries: UsageSummary[]
  aiResponseQuota: AiResponseQuota
  quotaAlerts?: AiQuotaAlert[]
  disclaimer: string
}

export interface WebhookEvent {
  id: string
  phoneNumberId: string
  status: string
  createdAt: string
  processedAt?: string
  retryCount: number
  errorMessage?: string
}

export interface WhatsAppLine {
  lineNumber: number
  connectionType: string
  phoneNumberId: string
  isActive: boolean
}

export interface BroadcastList {
  id: string
  name: string
  message: string
  status: string
  linePhoneNumberId?: string
  queueId?: string
  totalCount: number
  sentCount: number
  failedCount: number
  createdAt: string
  startedAt?: string
  finishedAt?: string
}

export interface BroadcastRecipient {
  id: string
  contactId: string
  status: string
  errorMessage?: string
  sentAt?: string
}

export interface BroadcastDetail {
  broadcast: BroadcastList
  recipients: BroadcastRecipient[]
}

export interface ClientTag {
  id: string
  name: string
  color?: string
  description?: string
  isActive: boolean
}

export const api = {
  auth: {
    getMe: () => fetchApi<User>('/api/auth/me'),

    login: async (email: string, password: string) => {
      const response = await fetch(`${API_BASE}/api/auth/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, password }),
      })
      if (!response.ok) {
        if (response.status === 401) throw new Error('INVALID_CREDENTIALS')
        const error = await response.text()
        throw new Error(error || `HTTP ${response.status}`)
      }
      const data = (await response.json()) as { token: string; user: User }
      setStoredToken(data.token)
      return data.user
    },

    logout: async () => {
      return fetchApi<void>('/api/auth/logout', {
        method: 'POST',
      })
    },

    changePassword: async (
      currentPassword: string,
      newPassword: string
    ) => {
      return fetchApi<{ message: string; mustChangePassword: boolean }>(
        '/api/auth/change-password',
        {
          method: 'POST',
          body: JSON.stringify({ currentPassword, newPassword }),
        }
      )
    },

    getCsrf: () =>
      fetchApi<{ token: string; headerName: string }>('/api/auth/csrf'),
  },

  dashboard: {
    getStats: () => fetchApi<DashboardStats>('/api/dashboard/stats'),
  },

  usage: {
    get: (from?: string, to?: string) => {
      const params = new URLSearchParams()
      if (from) params.set('from', from)
      if (to) params.set('to', to)
      const query = params.toString()
      return fetchApi<UsageResponse>(`/api/usage${query ? `?${query}` : ''}`)
    },
  },

  conversations: {
    list: (cursor?: string, limit = 50, operatorUserId?: string, lineFilter?: { connectionType: string; lineNumber: number }) => {
      const params = new URLSearchParams()
      params.set('limit', String(limit))
      if (cursor) params.set('cursor', cursor)
      if (operatorUserId) params.set('operatorUserId', operatorUserId)
      if (lineFilter) {
        params.set('lineConnectionType', lineFilter.connectionType)
        params.set('lineNumber', String(lineFilter.lineNumber))
      }
      return fetchApi<CursorPaginationResponse<Conversation>>(`/api/conversations?${params.toString()}`)
    },

    get: (id: string) =>
      fetchApi<Conversation>(`/api/conversations/${id}`),

    getMessages: (id: string, cursor?: string, limit = 50) =>
      fetchApi<CursorPaginationResponse<Message>>(
        `/api/conversations/${id}/messages?limit=${limit}${cursor ? `&cursor=${cursor}` : ''}`
      ),

    sendMessage: (id: string, content: string) =>
      fetchApi<{ id: string; status: string }>(
        `/api/conversations/${id}/messages`,
        {
          method: 'POST',
          body: JSON.stringify({ content }),
        }
      ),

    switchMode: (id: string, mode: string, version?: number) =>
      fetchApi<{ id: string; mode: string; version: number }>(
        `/api/conversations/${id}/mode`,
        {
          method: 'PUT',
          body: JSON.stringify({ mode }),
          headers: version ? { 'If-Match': version.toString() } : {},
        }
      ),
  },

  serviceQueues: {
    list: () => fetchApi<ServiceQueue[]>('/api/service-queues'),
    assign: (conversationId: string, queueId: string | null) =>
      fetchApi<{ conversationId: string; queueId: string | null }>(
        `/api/service-queues/conversations/${conversationId}/assign`,
        { method: 'POST', body: JSON.stringify({ queueId }) }
      ),
  },

  operators: {
    list: () => fetchApi<Operator[]>('/api/operators'),
    create: (data: { email: string; displayName?: string; password: string }) =>
      fetchApi<{ membershipId: string; email: string; displayName?: string; temporaryPassword: string }>('/api/operators', {
        method: 'POST',
        body: JSON.stringify(data),
      }),
    deactivate: (id: string) =>
      fetchApi<Operator>(`/api/operators/${id}/deactivate`, { method: 'POST' }),
    reactivate: (id: string) =>
      fetchApi<Operator>(`/api/operators/${id}/reactivate`, { method: 'POST' }),
    resetPassword: (id: string, newPassword: string) =>
      fetchApi<{ email: string; temporaryPassword: string }>(`/api/operators/${id}/reset-password`, {
        method: 'POST',
        body: JSON.stringify({ newPassword }),
      }),
    assignLine: (id: string, connectionType: string | null, lineNumber: number | null) =>
      fetchApi<Operator>(`/api/operators/${id}/line`, {
        method: 'PUT',
        body: JSON.stringify({ connectionType, lineNumber }),
      }),
    assignLines: (id: string, lines: { connectionType: string; lineNumber: number }[]) =>
      fetchApi<Operator>(`/api/operators/${id}/line`, {
        method: 'PUT',
        body: JSON.stringify({ lines }),
      }),
    assignQueue: (id: string, queueId: string | null) =>
      fetchApi<Operator>(`/api/operators/${id}/queue`, {
        method: 'PUT',
        body: JSON.stringify({ queueId }),
      }),
    update: (id: string, data: { email?: string; displayName?: string }) =>
      fetchApi<Operator>(`/api/operators/${id}`, {
        method: 'PUT',
        body: JSON.stringify(data),
      }),
  },

  contacts: {
    list: (search?: string, limit = 50, queueId?: string) => {
      const params = new URLSearchParams()
      params.set('limit', String(limit))
      if (search) params.set('search', search)
      if (queueId) params.set('queueId', queueId)
      return fetchApi<Contact[]>(`/api/contacts?${params.toString()}`)
    },

    get: (id: string) =>
      fetchApi<Contact>(`/api/contacts/${id}`),

    create: (data: {
      phoneNumber: string
      name?: string
      startConversation?: boolean
    }) =>
      fetchApi<Contact & { conversationId?: string }>('/api/contacts', {
        method: 'POST',
        body: JSON.stringify(data),
      }),

    update: (
      id: string,
      data: { name?: string; profilePictureUrl?: string }
    ) =>
      fetchApi<Contact>(`/api/contacts/${id}`, {
        method: 'PUT',
        body: JSON.stringify(data),
      }),

    import: (file: File) => {
      const data = new FormData()
      data.append('file', file)
      return fetchApi<ContactImportResult>('/api/contacts/import', {
        method: 'POST',
        body: data,
      })
    },

    startConversation: (id: string) =>
      fetchApi<{ conversationId: string }>(
        `/api/contacts/${id}/start-conversation`,
        {
          method: 'POST',
        }
      ),
  },

  tags: {
    list: () =>
      fetchApi<ClientTag[]>('/api/client-tags'),

    getContactTags: (contactId: string) =>
      fetchApi<ClientTag[]>(
        `/api/client-tags/contacts/${contactId}/tags`
      ),

    assignToContact: (contactId: string, tagId: string) =>
      fetchApi<ClientTag>(
        `/api/client-tags/contacts/${contactId}/tags/${tagId}`,
        { method: 'POST' }
      ),

    removeFromContact: (contactId: string, tagId: string) =>
      fetchApi<void>(
        `/api/client-tags/contacts/${contactId}/tags/${tagId}`,
        { method: 'DELETE' }
      ),
  },

  plans: {
    list: () => fetchApi<Plan[]>('/api/plans'),
  },

  webhookEvents: {
    list: (status?: string) =>
      fetchApi<WebhookEvent[]>(
        `/api/webhook-events${status ? `?status=${status}` : ''}`
      ),

    reprocess: (id: string) =>
      fetchApi<WebhookEvent>(
        `/api/webhook-events/${id}/reprocess`,
        { method: 'POST' }
      ),
  },

  broadcasts: {
    list: () => fetchApi<BroadcastList[]>('/api/broadcasts'),

    get: (id: string) => fetchApi<BroadcastDetail>(`/api/broadcasts/${id}`),

    create: (data: { name: string; message: string; contactIds: string[] }) =>
      fetchApi<BroadcastList>('/api/broadcasts', {
        method: 'POST',
        body: JSON.stringify(data),
      }),

    dispatch: (id: string, linePhoneNumberId: string, queueId?: string) =>
      fetchApi<BroadcastList>(`/api/broadcasts/${id}/dispatch`, {
        method: 'POST',
        body: JSON.stringify({ linePhoneNumberId, queueId: queueId || undefined }),
      }),

    cancel: (id: string) =>
      fetchApi<BroadcastList>(`/api/broadcasts/${id}/cancel`, { method: 'POST' }),

    delete: (id: string) =>
      fetchApi<void>(`/api/broadcasts/${id}`, { method: 'DELETE' }),
  },

  whatsapp: {
    getLines: async (): Promise<WhatsAppLine[]> => {
      const res = await fetchApi<{ isConfigured: boolean; lines: WhatsAppLine[] }>(
        '/api/integrations/whatsapp'
      )
      return res.lines ?? []
    },
  },

  admin: {
    tenants: {
      list: () =>
        fetchApi<Tenant[]>('/api/admin/tenants'),

      capacity: () =>
        fetchApi<InfrastructureCapacity>('/api/admin/tenants/capacity'),

      get: (id: string) =>
        fetchApi<Tenant>(`/api/admin/tenants/${id}`),

      quotaAlerts: (id: string) =>
        fetchApi<AiQuotaAlert[]>(`/api/admin/tenants/${id}/quota-alerts`),

      create: (data: {
        name: string
        ownerEmail: string
        ownerDisplayName?: string
        planCode: string
        officialApiLineCount: number
        qrCodeLineCount: number
        operatorLimit: number
        monthlyAiResponseLimit?: number | null
      }) =>
        fetchApi<CreateTenantResponse>('/api/admin/tenants', {
          method: 'POST',
          body: JSON.stringify(data),
        }),

      update: (
        id: string,
        data: {
          name: string
          ownerEmail: string
          ownerDisplayName?: string
          planCode: string
          officialApiLineCount: number
          qrCodeLineCount: number
          operatorLimit: number
          monthlyAiResponseLimit?: number | null
        },
        version: number
      ) =>
        fetchApi<Tenant>(`/api/admin/tenants/${id}`, {
          method: 'PUT',
          body: JSON.stringify(data),
          headers: { 'If-Match': `"${version}"` },
        }),

      suspend: (id: string, reason: string, version: number) =>
        fetchApi<Tenant>(`/api/admin/tenants/${id}/suspend`, {
          method: 'POST',
          body: JSON.stringify({ reason }),
          headers: { 'If-Match': `"${version}"` },
        }),

      reactivate: (id: string, version: number) =>
        fetchApi<Tenant>(`/api/admin/tenants/${id}/reactivate`, {
          method: 'POST',
          headers: { 'If-Match': `"${version}"` },
        }),

      registerPayment: (id: string, paidAt: string) =>
        fetchApi<{ dueDate: string; status: string }>(
          `/api/admin/tenants/${id}/payments`,
          {
            method: 'POST',
            body: JSON.stringify({ paidAt }),
          }
        ),

      updatePlan: (id: string, planCode: string, version: number) =>
        fetchApi<Tenant>(`/api/admin/tenants/${id}/plan`, {
          method: 'PUT',
          body: JSON.stringify({ planCode }),
          headers: { 'If-Match': `"${version}"` },
        }),

      resetOwnerPassword: (id: string) =>
        fetchApi<{
          email: string
          temporaryPassword: string
          message: string
        }>(
          `/api/admin/tenants/${id}/owner/reset-password`,
          { method: 'POST' }
        ),
    },
  },
}

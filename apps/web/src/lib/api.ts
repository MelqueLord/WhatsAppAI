const API_BASE = import.meta.env.VITE_API_URL ?? ''

let csrfToken: string | undefined

async function ensureCsrfToken(): Promise<string> {
  if (csrfToken) {
    return csrfToken
  }

  const response = await fetch(`${API_BASE}/api/auth/csrf`, {
    cache: 'no-store',
    credentials: 'include',
  })

  if (!response.ok) {
    throw new Error(`HTTP ${response.status}`)
  }

  const csrf = (await response.json()) as { token: string }
  csrfToken = csrf.token
  return csrfToken
}

async function fetchApi<T>(url: string, options?: RequestInit): Promise<T> {
  const method = options?.method?.toUpperCase()
  const isMutation =
    method === 'POST' ||
    method === 'PUT' ||
    method === 'PATCH' ||
    method === 'DELETE'

  let mutationHeaders: HeadersInit | undefined

  if (isMutation) {
    const token = await ensureCsrfToken()
    mutationHeaders = { 'X-CSRF-TOKEN': token }
  }

  const response = await fetch(`${API_BASE}${url}`, {
    ...options,
    cache: options?.method ? undefined : 'no-store',
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      ...mutationHeaders,
      ...options?.headers,
    },
  })

  if (!response.ok) {
    const error = await response.text()
    throw new Error(error || `HTTP ${response.status}`)
  }

  return response.json()
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
  isWindowOpen: boolean
  assignedToUserId?: string
  assignedToUserName?: string
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
  officialApiLineCount?: number
  qrCodeLineCount?: number
  operatorLimit?: number
  dueDate?: string
  tenantStatus?: string
  assignedConnectionType?: string
  assignedLineNumber?: number
}

export interface Operator {
  id: string
  userId: string
  email: string
  displayName?: string
  status: string
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
  ownerEmail?: string
  ownerDisplayName?: string
  suspendedAt?: string
  reactivatedAt?: string
  suspensionReason?: string
}

export interface Plan {
  id: string
  name: string
  code: string
  description?: string
  aiEnabled: boolean
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
  temporaryPassword: string
  message: string
}

export interface DashboardStats {
  operatorCount: number
  messagesToday: number
  activeConversations: number
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
      return fetchApi<User>('/api/auth/login', {
        method: 'POST',
        body: JSON.stringify({ email, password }),
      })
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

  conversations: {
    list: (cursor?: string, limit = 50, operatorUserId?: string) =>
      fetchApi<CursorPaginationResponse<Conversation>>(
        `/api/conversations?limit=${limit}${cursor ? `&cursor=${cursor}` : ''}${operatorUserId ? `&operatorUserId=${encodeURIComponent(operatorUserId)}` : ''}`
      ),

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

  operators: {
    list: () => fetchApi<Operator[]>('/api/operators'),
    create: (data: { email: string; displayName?: string; password: string }) =>
      fetchApi<{ email: string; temporaryPassword: string }>('/api/operators', {
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
  },

  contacts: {
    list: (search?: string, limit = 50) =>
      fetchApi<Contact[]>(
        `/api/contacts?limit=${limit}${search ? `&search=${search}` : ''}`
      ),

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

  admin: {
    tenants: {
      list: () =>
        fetchApi<Tenant[]>('/api/admin/tenants'),

      get: (id: string) =>
        fetchApi<Tenant>(`/api/admin/tenants/${id}`),

      create: (data: {
        name: string
        ownerEmail: string
        ownerDisplayName?: string
        planCode: string
        officialApiLineCount: number
        qrCodeLineCount: number
        operatorLimit: number
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
          planCode: string
          officialApiLineCount: number
          qrCodeLineCount: number
          operatorLimit: number
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

      updatePlan: (id: string, planCode: string) =>
        fetchApi<Tenant>(`/api/admin/tenants/${id}/plan`, {
          method: 'PUT',
          body: JSON.stringify({ planCode }),
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
const API_BASE = ''

async function fetchApi<T>(url: string, options?: RequestInit): Promise<T> {
  const response = await fetch(`${API_BASE}${url}`, {
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      ...options?.headers,
    },
    ...options,
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
}

export interface Tenant {
  id: string
  name: string
  slug: string
  planId: string
  status: string
  version: number
  createdAt: string
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
  activationLink: string
  message: string
}

export const api = {
  auth: {
    getMe: () => fetchApi<User>('/api/auth/me'),
    login: (email: string, password: string) =>
      fetchApi<User>('/api/auth/login', {
        method: 'POST',
        body: JSON.stringify({ email, password }),
      }),
    logout: () => fetchApi<void>('/api/auth/logout', { method: 'POST' }),
    changePassword: (currentPassword: string, newPassword: string) =>
      fetchApi<{ message: string; mustChangePassword: boolean }>('/api/auth/change-password', {
        method: 'POST',
        body: JSON.stringify({ currentPassword, newPassword }),
      }),
    getCsrf: () => fetchApi<{ token: string; headerName: string }>('/api/auth/csrf'),
  },
  conversations: {
    list: (cursor?: string, limit = 50) =>
      fetchApi<CursorPaginationResponse<Conversation>>(
        `/api/conversations?limit=${limit}${cursor ? `&cursor=${cursor}` : ''}`
      ),
    get: (id: string) => fetchApi<Conversation>(`/api/conversations/${id}`),
    getMessages: (id: string, cursor?: string, limit = 50) =>
      fetchApi<CursorPaginationResponse<Message>>(
        `/api/conversations/${id}/messages?limit=${limit}${cursor ? `&cursor=${cursor}` : ''}`
      ),
    sendMessage: (id: string, content: string) =>
      fetchApi<{ id: string; status: string }>(`/api/conversations/${id}/messages`, {
        method: 'POST',
        body: JSON.stringify({ content }),
      }),
    switchMode: (id: string, mode: string, version?: number) =>
      fetchApi<{ id: string; mode: string; version: number }>(`/api/conversations/${id}/mode`, {
        method: 'PUT',
        body: JSON.stringify({ mode }),
        headers: version ? { 'If-Match': version.toString() } : {},
      }),
  },
  contacts: {
    list: (search?: string, limit = 50) =>
      fetchApi<Contact[]>(`/api/contacts?limit=${limit}${search ? `&search=${search}` : ''}`),
    get: (id: string) => fetchApi<Contact>(`/api/contacts/${id}`),
    create: (data: { phoneNumber: string; name?: string; startConversation?: boolean }) =>
      fetchApi<Contact & { conversationId?: string }>('/api/contacts', {
        method: 'POST',
        body: JSON.stringify(data),
      }),
    update: (id: string, data: { name?: string; profilePictureUrl?: string }) =>
      fetchApi<Contact>(`/api/contacts/${id}`, {
        method: 'PUT',
        body: JSON.stringify(data),
      }),
    startConversation: (id: string) =>
      fetchApi<{ conversationId: string }>(`/api/contacts/${id}/start-conversation`, {
        method: 'POST',
      }),
  },
  tags: {
    list: () => fetchApi<any[]>('/api/client-tags'),
    getContactTags: (contactId: string) => fetchApi<any[]>(`/api/client-tags/contacts/${contactId}/tags`),
    assignToContact: (contactId: string, tagId: string) =>
      fetchApi<any>(`/api/client-tags/contacts/${contactId}/tags/${tagId}`, { method: 'POST' }),
    removeFromContact: (contactId: string, tagId: string) =>
      fetchApi<any>(`/api/client-tags/contacts/${contactId}/tags/${tagId}`, { method: 'DELETE' }),
  },
  plans: {
    list: () => fetchApi<Plan[]>('/api/plans'),
  },
  admin: {
    tenants: {
      list: () => fetchApi<Tenant[]>('/api/admin/tenants'),
      get: (id: string) => fetchApi<Tenant>(`/api/admin/tenants/${id}`),
      create: (data: { name: string; ownerEmail: string; ownerDisplayName?: string; planCode: string }) =>
        fetchApi<CreateTenantResponse>('/api/admin/tenants', {
          method: 'POST',
          body: JSON.stringify(data),
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
      updatePlan: (id: string, planCode: string) =>
        fetchApi<Tenant>(`/api/admin/tenants/${id}/plan`, {
          method: 'PUT',
          body: JSON.stringify({ planCode }),
        }),
    },
  },
}

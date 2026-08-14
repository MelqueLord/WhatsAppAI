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
  role?: string
  isPlatformAdmin: boolean
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
  tags: {
    list: () => fetchApi<any[]>('/api/client-tags'),
    getContactTags: (contactId: string) => fetchApi<any[]>(`/api/client-tags/contacts/${contactId}/tags`),
    assignToContact: (contactId: string, tagId: string) =>
      fetchApi<any>(`/api/client-tags/contacts/${contactId}/tags/${tagId}`, { method: 'POST' }),
    removeFromContact: (contactId: string, tagId: string) =>
      fetchApi<any>(`/api/client-tags/contacts/${contactId}/tags/${tagId}`, { method: 'DELETE' }),
  },
}

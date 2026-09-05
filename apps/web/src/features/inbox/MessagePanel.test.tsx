import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { MessagePanel } from './MessagePanel'
import type { Conversation } from '../../lib/api'

const apiMock = vi.hoisted(() => ({
  conversations: {
    get: vi.fn(),
    getMessages: vi.fn(),
    close: vi.fn(),
    submitAiFeedback: vi.fn(),
  },
  serviceQueues: {
    list: vi.fn(),
  },
  contacts: {
    create: vi.fn(),
  },
}))

vi.mock('../../lib/api', () => ({ api: apiMock }))

vi.mock('../../lib/auth', () => ({
  useAuth: () => ({
    user: {
      automaticDistributionEnabled: false,
      tagsEnabled: false,
    },
  }),
}))

vi.mock('../../lib/signalr', () => ({
  useSignalR: () => ({
    isConnected: true,
    start: vi.fn(),
  }),
}))

function createConversation(): Conversation {
  return {
    id: 'conversation-1',
    contactId: 'contact-1',
    contactName: 'Cliente',
    contactPhone: '5511999999999',
    mode: 'Human',
    status: 'Open',
    version: 1,
    isQrCode: true,
    isWindowOpen: true,
  }
}

function renderPanel(conversation = createConversation(), onConversationClosed = vi.fn()) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })

  render(
    <QueryClientProvider client={queryClient}>
      <MessagePanel
        conversation={conversation}
        onConversationClosed={onConversationClosed}
      />
    </QueryClientProvider>,
  )

  return { onConversationClosed }
}

describe('MessagePanel conversation closing', () => {
  beforeEach(() => {
    apiMock.conversations.get.mockReset()
    apiMock.conversations.getMessages.mockReset()
    apiMock.conversations.close.mockReset()
    apiMock.conversations.submitAiFeedback.mockReset()
    apiMock.serviceQueues.list.mockReset()
    apiMock.contacts.create.mockReset()

    apiMock.conversations.get.mockResolvedValue({
      ...createConversation(),
      version: 2,
    })
    apiMock.conversations.getMessages.mockResolvedValue({
      items: [],
      hasMore: false,
    })
    apiMock.conversations.close.mockResolvedValue({
      id: 'conversation-1',
      status: 'Closed',
      version: 3,
    })
    apiMock.serviceQueues.list.mockResolvedValue([])
  })

  it('closes using the latest conversation version instead of the stale list version', async () => {
    const { onConversationClosed } = renderPanel()

    fireEvent.click(await screen.findByRole('button', { name: /Encerrar/ }))

    await waitFor(() => {
      expect(apiMock.conversations.get).toHaveBeenCalledWith('conversation-1')
      expect(apiMock.conversations.close).toHaveBeenCalledWith('conversation-1', 2)
      expect(onConversationClosed).toHaveBeenCalledOnce()
    })
  })
})

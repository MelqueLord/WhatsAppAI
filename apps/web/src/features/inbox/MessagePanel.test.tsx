import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { MessagePanel } from './MessagePanel'
import type { Conversation, CursorPaginationResponse } from '../../lib/api'
import { formatDate, formatTime } from '../../lib/utils'

const apiMock = vi.hoisted(() => ({
  conversations: {
    get: vi.fn(),
    getMessages: vi.fn(),
    close: vi.fn(),
    submitAiFeedback: vi.fn(),
  },
  serviceQueues: {
    list: vi.fn(),
    assign: vi.fn(),
  },
  contacts: {
    create: vi.fn(),
  },
}))

vi.mock('../../lib/api', () => ({ api: apiMock }))

const authMock = vi.hoisted(() => ({
  user: {
    automaticDistributionEnabled: false,
    tagsEnabled: false,
  },
}))

vi.mock('../../lib/auth', () => ({
  useAuth: () => ({ user: authMock.user }),
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

  return { onConversationClosed, queryClient }
}

describe('MessagePanel conversation closing', () => {
  beforeEach(() => {
    Object.defineProperty(HTMLElement.prototype, 'scrollIntoView', {
      configurable: true,
      value: vi.fn(),
      writable: true,
    })
    apiMock.conversations.get.mockReset()
    apiMock.conversations.getMessages.mockReset()
    apiMock.conversations.close.mockReset()
    apiMock.conversations.submitAiFeedback.mockReset()
    apiMock.serviceQueues.list.mockReset()
    apiMock.serviceQueues.assign.mockReset()
    apiMock.contacts.create.mockReset()
    authMock.user.automaticDistributionEnabled = false
    authMock.user.tagsEnabled = false

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

  it('updates the queue inbox cache immediately after changing a conversation queue', async () => {
    authMock.user.automaticDistributionEnabled = true
    apiMock.serviceQueues.list.mockResolvedValue([{
      id: 'queue-1',
      name: 'Vendas',
      sortOrder: 1,
      isActive: true,
    }, {
      id: 'queue-2',
      name: 'Suporte',
      sortOrder: 2,
      isActive: true,
    }])
    apiMock.serviceQueues.assign.mockResolvedValue({
      conversationId: 'conversation-1',
      queueId: 'queue-2',
    })

    const { queryClient } = renderPanel({ ...createConversation(), queueId: 'queue-1' })
    queryClient.setQueryData<CursorPaginationResponse<Conversation>>(
      ['queue-inbox-conversations'],
      {
        items: [{ ...createConversation(), queueId: 'queue-1' }],
        hasMore: false,
      },
    )

    fireEvent.change(await screen.findByLabelText('Fila da conversa'), {
      target: { value: 'queue-2' },
    })

    await waitFor(() => {
      expect(apiMock.serviceQueues.assign).toHaveBeenCalledWith('conversation-1', 'queue-2')
      const cached = queryClient.getQueryData<CursorPaginationResponse<Conversation>>(
        ['queue-inbox-conversations'],
      )
      expect(cached?.items[0].queueId).toBe('queue-2')
    })
  })

  it('groups messages by date and shows the time in each message', async () => {
    const previousDayAt = '2026-09-07T15:29:00.000Z'
    const receivedAt = '2026-09-08T15:30:00.000Z'
    const sentAt = '2026-09-08T15:31:00.000Z'
    apiMock.conversations.getMessages.mockResolvedValue({
      items: [
        {
          id: 'previous-day-message',
          direction: 'Inbound',
          status: 'Delivered',
          type: 'Text',
          content: 'Mensagem de ontem',
          createdAt: previousDayAt,
        },
        {
          id: 'sent-message',
          direction: 'Outbound',
          status: 'Sent',
          type: 'Text',
          content: 'Mensagem enviada',
          createdAt: sentAt,
        },
        {
          id: 'received-message',
          direction: 'Inbound',
          status: 'Delivered',
          type: 'Text',
          content: 'Mensagem recebida',
          createdAt: receivedAt,
        },
      ],
      hasMore: false,
    })

    renderPanel()

    expect(await screen.findByText(formatDate(receivedAt))).toBeInTheDocument()
    expect(screen.getAllByText(formatDate(receivedAt))).toHaveLength(1)
    expect(screen.getAllByText(formatDate(previousDayAt))).toHaveLength(1)
    expect(screen.getAllByText(formatTime(previousDayAt))).toHaveLength(1)
    expect(screen.getAllByText(formatTime(receivedAt))).toHaveLength(1)
    expect(screen.getAllByText(formatTime(sentAt))).toHaveLength(1)
  })
})

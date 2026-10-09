import { render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { api } from '../../lib/api'
import { QueueInboxPage } from './QueueInboxPage'

const signalRMock = vi.hoisted(() => ({
  onConversationUpdate: undefined as (() => void) | undefined,
  start: vi.fn(),
}))

vi.mock('../../lib/api', () => ({
  api: {
    serviceQueues: { list: vi.fn() },
    conversations: { list: vi.fn() },
    contacts: { list: vi.fn() },
  },
}))

vi.mock('../../lib/signalr', () => ({
  useSignalR: (options: { onConversationUpdate?: () => void }) => {
    signalRMock.onConversationUpdate = options.onConversationUpdate
    return { start: signalRMock.start }
  },
}))

vi.mock('../inbox/MessagePanel', () => ({
  MessagePanel: () => <div>Painel da conversa</div>,
}))

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 } },
  })

  return render(
    <QueryClientProvider client={queryClient}>
      <QueueInboxPage />
    </QueryClientProvider>,
  )
}

describe('QueueInboxPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    signalRMock.onConversationUpdate = undefined
    vi.mocked(api.serviceQueues.list).mockResolvedValue([{
      id: 'queue-1',
      name: 'Vendas',
      sortOrder: 1,
      isActive: true,
    }])
    vi.mocked(api.conversations.list).mockResolvedValue({
      items: [{
        id: 'conversation-1',
        contactId: 'contact-1',
        contactName: 'Cliente',
        contactPhone: '5511999999999',
        status: 'Open',
        mode: 'Human',
        queueId: 'queue-1',
        version: 1,
        isWindowOpen: true,
        lastMessage: 'Olá',
        lastMessageAt: '2026-09-11T00:00:00Z',
      }],
      hasMore: false,
    })
    vi.mocked(api.contacts.list).mockResolvedValue([{
      id: 'imported-contact-1',
      name: 'Contato da planilha',
      phoneNumber: '5511888888888',
      createdAt: '2026-09-11T00:00:00Z',
    }, {
      id: 'contact-1',
      name: 'Cliente importado com conversa',
      phoneNumber: '5511999999999',
      createdAt: '2026-09-11T00:00:00Z',
    }])
  })

  it('refreshes the queue inbox when a conversation update arrives', async () => {
    renderPage()

    await screen.findByText('Cliente')
    expect(await screen.findByText('Contato da planilha')).toBeInTheDocument()
    expect(screen.queryByText('5511999999999')).not.toBeInTheDocument()
    expect(api.contacts.list).toHaveBeenCalledWith(undefined, 5000, 'queue-1', true)
    expect(signalRMock.onConversationUpdate).toBeDefined()
    expect(api.conversations.list).toHaveBeenCalledTimes(1)

    signalRMock.onConversationUpdate?.()

    await waitFor(() => expect(api.conversations.list).toHaveBeenCalledTimes(2))
  })
})

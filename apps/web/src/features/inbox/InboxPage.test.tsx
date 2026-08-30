import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { InboxPage } from './InboxPage'

const signalRMock = vi.hoisted(() => ({
  state: {
    isConnected: true as boolean | null,
    isReconnecting: false,
    start: vi.fn(),
  },
}))

vi.mock('../../lib/auth', () => ({
  useAuth: () => ({ user: { tenantStatus: 'Active' } }),
}))

vi.mock('../../lib/signalr', () => ({
  useSignalR: () => signalRMock.state,
}))

vi.mock('./ConversationList', () => ({
  ConversationList: () => <div>Lista de conversas</div>,
}))

vi.mock('./MessagePanel', () => ({
  MessagePanel: () => <div>Painel da conversa</div>,
}))

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <InboxPage />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('InboxPage', () => {
  beforeEach(() => {
    signalRMock.state.isConnected = true
    signalRMock.state.isReconnecting = false
    signalRMock.state.start.mockReset()
  })

  it('does not show a reconnection notice while the hub is connected', () => {
    renderPage()

    expect(screen.queryByText('Reconectando ao servidor...')).not.toBeInTheDocument()
  })

  it('shows the reconnection notice only during an active retry', () => {
    signalRMock.state.isConnected = false
    signalRMock.state.isReconnecting = true

    renderPage()

    expect(screen.getByText('Reconectando ao servidor...')).toBeInTheDocument()
  })
})

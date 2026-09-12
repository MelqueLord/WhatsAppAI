import { fireEvent, render, screen } from '@testing-library/react'
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
  ConversationList: ({ onSelect, statusFilter, onStatusFilterChange }: {
    onSelect: (conversation: { id: string }) => void
    statusFilter: 'Open' | 'Closed'
    onStatusFilterChange: (status: 'Open' | 'Closed') => void
  }) => (
    <div>
      <div data-testid="conversation-status">{statusFilter}</div>
      <button onClick={() => onStatusFilterChange('Closed')}>Filtrar encerradas</button>
      <button onClick={() => onSelect({ id: 'conversation-1' })}>Abrir conversa</button>
    </div>
  ),
}))

vi.mock('./MessagePanel', () => ({
  MessagePanel: ({ onConversationClosed }: { onConversationClosed?: () => void }) => (
    <div>
      <div>Painel da conversa</div>
      <button onClick={() => onConversationClosed?.()}>Encerrar conversa</button>
    </div>
  ),
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

  it('switches to closed conversations after closing the selected conversation', () => {
    renderPage()

    fireEvent.click(screen.getByRole('button', { name: 'Abrir conversa' }))
    fireEvent.click(screen.getByRole('button', { name: 'Encerrar conversa' }))

    expect(screen.getByTestId('conversation-status')).toHaveTextContent('Closed')
  })
})

import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'
import { DashboardPage } from './DashboardPage'
import { api } from '../../lib/api'

vi.mock('../../lib/auth', () => ({
  useAuth: () => ({
    user: {
      tenantId: 'tenant-1',
      displayName: 'Empresa',
      role: 'TenantOwner',
      aiEnabled: true,
      monthlyAiResponseLimit: 1500,
      monthlyAiResponsesUsed: 1200,
    },
    isPlatformAdmin: false,
    isTenantOwner: true,
  }),
}))

vi.mock('../../lib/api', () => ({
  api: {
    conversations: { list: vi.fn().mockResolvedValue({ items: [] }) },
    dashboard: { getStats: vi.fn().mockResolvedValue({ operatorCount: 0, messagesToday: 0, activeConversations: 0 }) },
  },
}))

describe('DashboardPage AI package', () => {
  it('shows an attention warning at 80% consumption', async () => {
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    render(
      <QueryClientProvider client={queryClient}>
        <MemoryRouter><DashboardPage /></MemoryRouter>
      </QueryClientProvider>,
    )

    expect(await screen.findByText('Pacote de respostas da IA')).toBeInTheDocument()
    expect(screen.getByText('Atenção: o pacote está próximo do fim. Solicite uma recarga.')).toBeInTheDocument()
    expect(screen.getByText('80%')).toBeInTheDocument()
  })

  it('localizes conversation modes in recent conversations', async () => {
    vi.mocked(api.conversations.list).mockResolvedValueOnce({
      items: [{
        id: 'conversation-1',
        contactId: 'contact-1',
        contactName: 'Ana',
        contactPhone: '5511999999999',
        mode: 'Human',
        status: 'Open',
        version: 1,
        lastMessage: 'Olá',
        isWindowOpen: true,
      }],
      hasMore: false,
    })
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    render(
      <QueryClientProvider client={queryClient}>
        <MemoryRouter><DashboardPage /></MemoryRouter>
      </QueryClientProvider>,
    )

    expect(await screen.findByText('Humano')).toBeInTheDocument()
    expect(screen.queryByText('Human')).not.toBeInTheDocument()
  })
})

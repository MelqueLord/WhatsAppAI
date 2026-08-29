import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { UsagePage } from './UsagePage'
import { api } from '../../lib/api'

vi.mock('../../lib/api', () => ({
  api: { usage: { get: vi.fn() } },
}))

vi.mock('../../lib/auth', () => ({
  useAuth: () => ({ user: { aiEnabled: true }, isLoading: false }),
}))

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <UsagePage />
    </QueryClientProvider>,
  )
}

describe('UsagePage AI quota', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(api.usage.get).mockResolvedValue({
      from: new Date().toISOString(),
      to: new Date().toISOString(),
      entries: [],
      aiResponseQuota: { limit: 1500, used: 0, remaining: 1500, utilizationPercentage: 0 },
      disclaimer: 'Estimativas de uso. Não é uma fatura.',
    })
  })

  it('shows a warning when the tenant reaches 80 percent', async () => {
    vi.mocked(api.usage.get).mockResolvedValueOnce({
      from: '', to: '', entries: [],
      aiResponseQuota: { limit: 1500, used: 1200, remaining: 300, utilizationPercentage: 80 },
      quotaAlerts: [{ action: 'AiQuota.WarningReached', entityId: '2026-08:Warning', occurredAt: new Date().toISOString() }],
      disclaimer: 'Uso',
    })

    renderPage()

    expect(await screen.findByText('Restam 300 respostas.')).toBeInTheDocument()
    expect(screen.getByText('80%')).toBeInTheDocument()
    expect(screen.getByText('Histórico recente da franquia')).toBeInTheDocument()
  })

  it('shows the safe fallback message when the quota is exhausted', async () => {
    vi.mocked(api.usage.get).mockResolvedValueOnce({
      from: '', to: '', entries: [],
      aiResponseQuota: { limit: 1500, used: 1500, remaining: 0, utilizationPercentage: 100 },
      disclaimer: 'Uso',
    })

    renderPage()

    expect(await screen.findByText(/Franquia esgotada/)).toBeInTheDocument()
  })

  it('does not show a progress bar for an unlimited tenant', async () => {
    vi.mocked(api.usage.get).mockResolvedValueOnce({
      from: '', to: '', entries: [],
      aiResponseQuota: { limit: null, used: 10, remaining: null, utilizationPercentage: null },
      disclaimer: 'Uso',
    })

    renderPage()

    expect(await screen.findByText('Sem limite mensal configurado.')).toBeInTheDocument()
    expect(screen.queryByText('%')).not.toBeInTheDocument()
  })
})

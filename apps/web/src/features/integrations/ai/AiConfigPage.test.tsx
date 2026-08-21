import { render, screen, waitFor } from '@testing-library/react'
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { AiConfigPage } from './AiConfigPage'

vi.mock('../../../lib/auth', () => ({
  useAuth: () => ({
    user: { aiEnabled: true, displayName: 'Owner', email: 'owner@test.com', role: 'TenantOwner' },
    isAuthenticated: true,
    isTenantOwner: true,
  }),
}))

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 } },
  })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter><AiConfigPage /></MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('AiConfigPage', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn((url: string) => {
      const isProviders = typeof url === 'string' && url.includes('/providers')
      return Promise.resolve({
        ok: true,
        json: () => Promise.resolve(
          isProviders
            ? [{ id: 'openai', name: 'OpenAI', models: [{ id: 'gpt-4o', name: 'GPT-4o' }] }]
            : { configured: false, botConfig: { mode: 'Manual', maxTokensPerResponse: 500 } }
        ),
      })
    }))
  })

  it('renders all main sections after loading', async () => {
    renderPage()
    await waitFor(() => {
      expect(screen.getByText('Atendimento com IA')).toBeInTheDocument()
    }, { timeout: 5000 })

    expect(screen.getByText('Provedor de IA')).toBeInTheDocument()
    expect(screen.getByText('OpenAI')).toBeInTheDocument()
    expect(screen.getByText('Modelo')).toBeInTheDocument()
    expect(screen.getByText('API Key')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Salvar' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Testar conexão' })).toBeInTheDocument()
  })

  it('calls fetch for providers and config', async () => {
    renderPage()
    await waitFor(() => {
      expect(fetch).toHaveBeenCalledWith('/api/integrations/ai/providers', expect.anything())
      expect(fetch).toHaveBeenCalledWith('/api/integrations/ai', expect.anything())
    })
  })
})

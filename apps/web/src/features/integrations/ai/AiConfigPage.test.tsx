import { fireEvent, render, screen, waitFor } from '@testing-library/react'
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
      const isQueues = typeof url === 'string' && url.includes('/api/service-queues')
      const isTags = typeof url === 'string' && url.includes('/api/client-tags')
      const isSimulation = typeof url === 'string' && url.includes('/api/integrations/ai/simulate')
      return Promise.resolve({
        ok: true,
        json: () => Promise.resolve(
          isProviders
            ? [{ id: 'openai', name: 'OpenAI', models: [{ id: 'gpt-4o', name: 'GPT-4o' }] }]
            : isSimulation
              ? { decision: 'Handoff', confidence: 0.2, handoffReason: 'low_confidence', fallbackReason: 'A confiança ficou abaixo do limiar configurado.' }
            : isQueues || isTags
              ? []
            : {
                configured: true,
                provider: 'openai',
                modelId: 'gpt-4o',
                version: 1,
                guidelines: { behavior: [], security: [], handoff: [] },
              }
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
    expect(screen.getByText('Regras estruturadas')).toBeInTheDocument()
    expect(screen.getByText('Limiar de confiança')).toBeInTheDocument()
    expect(screen.getByText('Descrição do negócio')).toBeInTheDocument()
    expect(screen.getByText('Público-alvo')).toBeInTheDocument()
    expect(screen.getByText('Produtos e serviços')).toBeInTheDocument()
    expect(screen.getByText('Tom de voz')).toBeInTheDocument()
    expect(screen.queryByText('Mensagens automáticas')).not.toBeInTheDocument()
  })

  it('calls fetch for providers and config', async () => {
    renderPage()
    await waitFor(() => {
      expect(fetch).toHaveBeenCalledWith('/api/integrations/ai/providers', expect.anything())
      expect(fetch).toHaveBeenCalledWith('/api/integrations/ai', expect.anything())
      expect(fetch).not.toHaveBeenCalledWith('/api/bot-config', expect.anything())
    })
  })

  it('simulates a decision without calling bot endpoints', async () => {
    renderPage()
    await waitFor(() => expect(screen.getByText('Simular antes de ativar')).toBeInTheDocument())
    const input = screen.getByPlaceholderText('Digite uma mensagem de exemplo')
    fireEvent.change(input, { target: { value: 'Preciso de ajuda' } })
    fireEvent.click(screen.getByRole('button', { name: 'Simular decisão' }))
    await waitFor(() => expect(screen.getByText('Motivo do handoff:')).toBeInTheDocument())
    expect(fetch).toHaveBeenCalledWith('/api/integrations/ai/simulate', expect.objectContaining({ method: 'POST' }))
    expect(fetch).not.toHaveBeenCalledWith('/api/bot-config', expect.anything())
  })
})

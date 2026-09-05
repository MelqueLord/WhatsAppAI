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
              ? { decision: 'Handoff', confidence: 0.2, handoffReason: 'low_confidence', fallbackReason: 'A confiança ficou abaixo do limiar configurado.', sources: [{ type: 'conhecimento', name: 'Preço da consulta', detail: 'Categoria: Pricing' }] }
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
      expect(screen.getByText('Preparar agente')).toBeInTheDocument()
    }, { timeout: 5000 })

    expect(screen.getByText('1. Identidade')).toBeInTheDocument()
    expect(screen.getByText('Descrição do negócio')).toBeInTheDocument()
    expect(screen.getByRole('option', { name: 'Tecnologia e software' })).toBeInTheDocument()
    expect(screen.getByRole('option', { name: 'Pet shop e veterinária' })).toBeInTheDocument()
    expect(screen.getByRole('option', { name: 'Didático e paciente' })).toBeInTheDocument()
    expect(screen.getByRole('option', { name: 'Calmo e tranquilizador' })).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: /2\. Comportamento/ }))
    expect(screen.getByText('Regras da plataforma')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: /3\. Conhecimento/ }))
    expect(screen.getByText('Base de conhecimento')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: /4\. Testar e ativar/ }))
    expect(screen.getByText('Avaliação do modelo')).toBeInTheDocument()
    expect(screen.queryByText('API Key')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Testar conexão' })).not.toBeInTheDocument()
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
    await waitFor(() => expect(screen.getByText('Preparar agente')).toBeInTheDocument())
    fireEvent.click(screen.getByRole('button', { name: /4\. Testar e ativar/ }))
    const input = screen.getByPlaceholderText('Ex.: Quero saber os preços dos planos')
    fireEvent.change(input, { target: { value: 'Preciso de ajuda' } })
    fireEvent.click(screen.getByRole('button', { name: 'Simular decisão' }))
    await waitFor(() => expect(screen.getByText('Motivo do handoff:')).toBeInTheDocument())
    expect(screen.getByText('Encaminhar para atendimento humano')).toBeInTheDocument()
    expect(screen.getByText('20%')).toBeInTheDocument()
    expect(screen.getByText('Confiança abaixo do limiar')).toBeInTheDocument()
    expect(screen.getByText('Dados usados nesta simulação')).toBeInTheDocument()
    expect(screen.getByText('Preço da consulta')).toBeInTheDocument()
    expect(fetch).toHaveBeenCalledWith('/api/integrations/ai/simulate', expect.objectContaining({ method: 'POST' }))
    expect(fetch).not.toHaveBeenCalledWith('/api/bot-config', expect.anything())
  })
})

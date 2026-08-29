import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { BotConfigPage } from './BotConfigPage'

vi.mock('../../lib/auth', () => ({ useAuth: () => ({ user: { aiEnabled: true }, isTenantOwner: true }) }))

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } })
  return render(<QueryClientProvider client={queryClient}><MemoryRouter><BotConfigPage /></MemoryRouter></QueryClientProvider>)
}

describe('BotConfigPage', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn((url: string, options?: RequestInit) => {
      if (url.includes('/api/bot-config') && options?.method === 'POST') {
        return Promise.resolve({ ok: true, json: async () => ({ saved: true, version: 4, enabled: true, mode: 'AiPowered' }) })
      }
      return Promise.resolve({ ok: true, json: async () => ({ configured: true, mode: 'AiPowered', enabled: true, version: 3, welcomeMessage: 'Olá', flowSteps: [{ id: '1', title: 'Boleto', keywords: 'boleto', response: 'Envio o boleto.' }] }) })
    }))
  })

  it('preserves the current mode when saving BOT messages', async () => {
    renderPage()
    await waitFor(() => expect(screen.getByText('Fluxo do Bot')).toBeInTheDocument())
    expect(screen.getByText('Horário de atendimento')).toBeInTheDocument()
    expect(screen.getByText('Ativar horário de atendimento')).toBeInTheDocument()
    expect(screen.getByText('Fuso horário')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Salvar configuração' }))
    await waitFor(() => expect(fetch).toHaveBeenCalledWith('/api/bot-config', expect.objectContaining({ method: 'POST' })))
    const call = (fetch as ReturnType<typeof vi.fn>).mock.calls.find(([url, opts]) => url === '/api/bot-config' && opts?.method === 'POST')
    expect(call).toBeDefined()
    const options = call?.[1] as RequestInit
    expect(new Headers(options.headers).get('If-Match')).toBe('3')
    expect(JSON.parse(options.body as string).mode).toBe('AiPowered')
  })

  it('previews a flow locally without calling an external endpoint', async () => {
    renderPage()
    await waitFor(() => expect(screen.getByText('Pré-visualização segura')).toBeInTheDocument())
    fireEvent.change(screen.getByPlaceholderText('Digite uma mensagem de exemplo'), { target: { value: 'segunda via boleto' } })
    expect(screen.getByText('Envio o boleto.')).toBeInTheDocument()
    expect(fetch).not.toHaveBeenCalledWith('/api/integrations/ai/simulate', expect.anything())
  })
})

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

  it('shows the BOT as active while the AI strategy is active', async () => {
    renderPage()

    expect(await screen.findByRole('button', { name: 'Bot ativo' })).toBeInTheDocument()
    expect(screen.getByText('O BOT está ativo com IA. A IA responde dentro do fluxo do BOT, sem duplicar mensagens.')).toBeInTheDocument()
    expect(screen.queryByText('inativo', { selector: 'strong' })).not.toBeInTheDocument()
  })

  it('reactivates the BOT without replacing the active AI strategy', async () => {
    vi.stubGlobal('fetch', vi.fn((url: string, options?: RequestInit) => {
      if (url.includes('/api/bot-config') && options?.method === 'POST') {
        return Promise.resolve({ ok: true, json: async () => ({ enabled: true, mode: 'AiPowered', version: 4 }) })
      }
      return Promise.resolve({ ok: true, json: async () => ({ configured: true, mode: 'AiPowered', enabled: false, version: 3, welcomeMessage: 'Olá', flowSteps: [] }) })
    }))

    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: 'Bot inativo' }))
    await waitFor(() => expect(fetch).toHaveBeenCalledWith('/api/bot-config/toggle', expect.objectContaining({ method: 'POST' })))

    const call = (fetch as ReturnType<typeof vi.fn>).mock.calls.find(([url]) => url === '/api/bot-config/toggle')
    expect(JSON.parse((call?.[1] as RequestInit).body as string)).toEqual({ enabled: true, mode: 'AiPowered' })
  })

  it('shows a clear error when activation is rejected by the server', async () => {
    vi.stubGlobal('fetch', vi.fn((url: string) => {
      if (url.includes('/api/bot-config/toggle')) {
        return Promise.resolve({ ok: false, status: 400, json: async () => ({ error: 'O modelo precisa de uma avaliação aprovada antes da ativação.' }) })
      }
      return Promise.resolve({ ok: true, json: async () => ({ configured: true, mode: 'AiPowered', enabled: false, version: 3, flowSteps: [] }) })
    }))

    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: 'Bot inativo' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('avaliação aprovada')
  })

  it('exposes the 160-character limit for bot messages', async () => {
    renderPage()
    await waitFor(() => expect(screen.getByText('Saudações')).toBeInTheDocument())

    const messageFields = screen.getAllByRole('textbox').filter((field) => field.tagName === 'TEXTAREA')
    expect(messageFields.length).toBeGreaterThan(0)
    messageFields.forEach((field) => expect(field).toHaveAttribute('maxlength', '160'))
  })
})

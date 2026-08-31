import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AiExamplesPage } from './AiExamplesPage'

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } })
  return render(<QueryClientProvider client={client}><AiExamplesPage /></QueryClientProvider>)
}

describe('AiExamplesPage', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn((_url: string, options?: RequestInit) => Promise.resolve({
      ok: true,
      status: options?.method === 'POST' ? 201 : 200,
      json: () => Promise.resolve(options?.method === 'POST' ? { id: 'example-id', version: 1 } : []),
    })))
  })

  it('creates a company-specific response example', async () => {
    renderPage()
    await waitFor(() => expect(screen.getByText('Exemplos de atendimento')).toBeInTheDocument())
    fireEvent.click(screen.getByRole('button', { name: 'Novo exemplo' }))
    fireEvent.change(screen.getByLabelText('Mensagem do cliente'), { target: { value: 'Quero agendar' } })
    fireEvent.change(screen.getByLabelText('Resposta ideal'), { target: { value: 'Claro! Vou ajudar.' } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar exemplo' }))

    await waitFor(() => expect(fetch).toHaveBeenCalledWith('/api/ai-response-examples', expect.objectContaining({ method: 'POST' })))
    const postCall = vi.mocked(fetch).mock.calls.find(([, options]) => (options as RequestInit | undefined)?.method === 'POST')
    expect(JSON.parse((postCall?.[1] as RequestInit).body as string)).toEqual({ customerMessage: 'Quero agendar', idealResponse: 'Claro! Vou ajudar.' })
  })
})

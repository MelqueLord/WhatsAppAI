import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AiScenarioTestsPage } from './AiScenarioTestsPage'

function renderPage() {
  const client = new QueryClient({ defaultOptions: { mutations: { retry: false } } })
  return render(<QueryClientProvider client={client}><AiScenarioTestsPage /></QueryClientProvider>)
}

describe('AiScenarioTestsPage', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(() => Promise.resolve({
      ok: true,
      status: 200,
      json: () => Promise.resolve({
        decision: 'Reply',
        text: 'A consulta custa R$ 150.',
        confidence: 0.87,
      }),
    })))
  })

  it('executes a preset scenario and presents the simulated decision', async () => {
    renderPage()

    fireEvent.click(screen.getByRole('button', { name: /Preço/ }))
    fireEvent.click(screen.getByRole('button', { name: 'Executar teste' }))

    await waitFor(() => expect(fetch).toHaveBeenCalledWith(
      '/api/integrations/ai/simulate',
      expect.objectContaining({ method: 'POST' }),
    ))
    const request = vi.mocked(fetch).mock.calls[0][1] as RequestInit
    expect(JSON.parse(request.body as string)).toEqual({
      message: 'Qual é o preço do principal serviço da empresa?',
    })
    expect(await screen.findByText('A consulta custa R$ 150.')).toBeInTheDocument()
    expect(screen.getByText('Responder')).toBeInTheDocument()
    expect(screen.getByText('87%')).toBeInTheDocument()
  })
})

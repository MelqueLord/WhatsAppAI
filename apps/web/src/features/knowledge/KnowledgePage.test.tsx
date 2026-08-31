import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { KnowledgePage } from './KnowledgePage'

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } })
  return render(<QueryClientProvider client={queryClient}><KnowledgePage /></QueryClientProvider>)
}

describe('KnowledgePage', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn((_url: string, options?: RequestInit) => Promise.resolve({
      ok: true,
      json: () => Promise.resolve(options?.method === 'POST' ? { id: 'item-id', version: 1 } : []),
    })))
  })

  it('guides a pricing entry and persists its category', async () => {
    renderPage()
    await waitFor(() => expect(screen.getByText('Base de Conhecimento')).toBeInTheDocument())

    fireEvent.click(screen.getByRole('button', { name: /Novo item guiado/i }))
    fireEvent.change(screen.getByLabelText('Tipo de informação'), { target: { value: 'Pricing' } })

    expect(screen.getByPlaceholderText('Ex.: Valor da avaliação inicial')).toBeInTheDocument()
    fireEvent.change(screen.getByPlaceholderText('Ex.: Valor da avaliação inicial'), { target: { value: 'Avaliação' } })
    fireEvent.change(screen.getByPlaceholderText('Informe valor, moeda, condições, validade e quando deve haver orçamento humano.'), { target: { value: 'A avaliação custa R$ 100.' } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar informação' }))

    await waitFor(() => expect(fetch).toHaveBeenCalledWith('/api/knowledge', expect.objectContaining({ method: 'POST' })))
    const calls = vi.mocked(fetch).mock.calls
    const postCall = calls.find(([, options]) => (options as RequestInit | undefined)?.method === 'POST')
    expect(JSON.parse((postCall?.[1] as RequestInit).body as string)).toMatchObject({ category: 'Pricing', priority: 100 })
  })
})

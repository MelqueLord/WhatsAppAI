import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { QueuesPage } from './QueuesPage'

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <QueuesPage />
    </QueryClientProvider>,
  )
}

describe('QueuesPage interaction reply', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn((url: string) => Promise.resolve({
      ok: true,
      json: async () => url === '/api/service-queues' ? [] : { id: 'queue-1' },
    })))
  })

  it('saves the reply that is sent when a queued customer interacts again', async () => {
    renderPage()

    fireEvent.click(await screen.findByRole('button', { name: /Nova Fila/ }))
    fireEvent.change(screen.getByPlaceholderText('Nome da fila *'), { target: { value: 'Suporte' } })
    fireEvent.change(
      screen.getByPlaceholderText('Resposta quando o cliente interagir nesta fila (opcional)'),
      { target: { value: 'Recebemos sua mensagem. Retornaremos em breve.' } },
    )
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))

    await waitFor(() => expect(fetch).toHaveBeenCalledWith(
      '/api/service-queues',
      expect.objectContaining({
        method: 'POST',
        body: expect.stringContaining('"interactionReply":"Recebemos sua mensagem. Retornaremos em breve."'),
      }),
    ))
  })
})

import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { OperatorsPage } from './OperatorsPage'

vi.mock('../../lib/auth', () => ({
  useAuth: () => ({
    user: { operatorLimit: 0, officialApiLineCount: 0, qrCodeLineCount: 0 },
  }),
}))

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter><OperatorsPage /></MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('OperatorsPage queue assignment', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn((url: string, options?: RequestInit) => {
      if (url === '/api/service-queues') {
        return Promise.resolve({
          ok: true,
          json: async () => [{ id: 'queue-1', name: 'Financeiro', sortOrder: 0, isActive: true }],
        })
      }
      if (url === '/api/operators/operator-1/queue' && options?.method === 'PUT') {
        return Promise.resolve({
          ok: true,
          json: async () => ({
            id: 'operator-1', userId: 'user-1', email: 'operator@test.com', displayName: 'Operator',
            status: 'Active', createdAt: '2026-08-28T00:00:00Z', assignedQueueId: 'queue-1', assignedLines: [],
          }),
        })
      }
      return Promise.resolve({
        ok: true,
        json: async () => [{
          id: 'operator-1', userId: 'user-1', email: 'operator@test.com', displayName: 'Operator',
          status: 'Active', createdAt: '2026-08-28T00:00:00Z', assignedQueueId: null, assignedLines: [],
        }],
      })
    }))
  })

  it('switches an operator from general service to a specific queue', async () => {
    renderPage()

    const select = await screen.findByRole('combobox', { name: 'Fila de Operator' })
    fireEvent.change(select, { target: { value: 'queue-1' } })

    await waitFor(() => expect(fetch).toHaveBeenCalledWith(
      '/api/operators/operator-1/queue',
      expect.objectContaining({
        method: 'PUT',
        body: JSON.stringify({ queueId: 'queue-1' }),
      }),
    ))
  })
})

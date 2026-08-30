import { render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { describe, expect, it, vi } from 'vitest'
import { WebhookEventsPage } from './WebhookEventsPage'

vi.mock('../../../lib/api', () => ({
  api: {
    webhookEvents: {
      list: vi.fn().mockResolvedValue([{
        id: 'event-1',
        phoneNumberId: 'phone-1',
        status: 'UnexpectedStatus',
        createdAt: new Date().toISOString(),
        retryCount: 0,
        errorMessage: null,
      }]),
      reprocess: vi.fn(),
    },
  },
}))

describe('WebhookEventsPage', () => {
  it('uses Portuguese labels for the phone column and unknown statuses', async () => {
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    render(
      <QueryClientProvider client={queryClient}>
        <WebhookEventsPage />
      </QueryClientProvider>,
    )

    await waitFor(() => expect(screen.getByText('Número de telefone')).toBeInTheDocument())
    expect(screen.getByText('Desconhecido')).toBeInTheDocument()
    expect(screen.queryByText('UnexpectedStatus')).not.toBeInTheDocument()
  })
})

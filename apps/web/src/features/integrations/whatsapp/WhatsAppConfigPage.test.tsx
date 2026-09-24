import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { WhatsAppConfigPage } from './WhatsAppConfigPage'

const fetchApiResponse = vi.hoisted(() => vi.fn())

vi.mock('../../../lib/api', () => ({ fetchApiResponse }))

vi.mock('../../../lib/auth', () => ({
  useAuth: () => ({
    isOperator: false,
    user: { tenantStatus: 'Active', officialApiLineCount: 1, qrCodeLineCount: 0 },
  }),
}))

function response(body: unknown, ok = true) {
  return { ok, status: ok ? 200 : 400, json: async () => body }
}

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <WhatsAppConfigPage />
    </QueryClientProvider>,
  )
}

describe('WhatsAppConfigPage', () => {
  beforeEach(() => {
    fetchApiResponse.mockReset()
    fetchApiResponse.mockImplementation((url: string) => {
      if (url.includes('/official/status/')) {
        return Promise.resolve(response({
          configured: true,
          isActive: true,
          isConnected: true,
          phoneNumber: '+55 11 99999-9999',
          message: 'Connection successful.',
        }))
      }

      if (url.includes('/session/status/')) {
        return Promise.resolve(response({ isConnected: false, status: 'disconnected' }))
      }

      return Promise.resolve(response({ isConfigured: false, lines: [] }))
    })
  })

  it('shows that the selected official API line is connected after Meta validates it', async () => {
    renderPage()

    fireEvent.click(await screen.findByRole('button', { name: 'API Oficial' }))

    expect(await screen.findByText('Conectado')).toBeInTheDocument()
    expect(screen.getByText('+55 11 99999-9999')).toBeInTheDocument()
  })

  it('shows disconnected when the selected official API line cannot be validated', async () => {
    fetchApiResponse.mockImplementation((url: string) => {
      if (url.includes('/official/status/')) {
        return Promise.resolve(response({
          configured: true,
          isConnected: false,
          message: 'Connection failed. Please check your credentials.',
        }))
      }

      if (url.includes('/session/status/')) {
        return Promise.resolve(response({ isConnected: false, status: 'disconnected' }))
      }

      return Promise.resolve(response({ isConfigured: false, lines: [] }))
    })

    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: 'API Oficial' }))

    await waitFor(() => {
      expect(screen.getByText('Desconectado')).toBeInTheDocument()
      expect(screen.getByText('Connection failed. Please check your credentials.')).toBeInTheDocument()
    })
  })

  it('disconnects the selected official API line', async () => {
    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: 'API Oficial' }))

    fireEvent.click(await screen.findByRole('button', { name: 'Desconectar API' }))

    await waitFor(() => {
      expect(fetchApiResponse).toHaveBeenCalledWith(
        '/api/integrations/whatsapp/official/disconnect/1',
        expect.objectContaining({ method: 'POST' }),
      )
    })
  })
})

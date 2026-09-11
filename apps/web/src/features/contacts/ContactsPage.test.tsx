import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { api } from '../../lib/api'
import { ContactsPage } from './ContactsPage'

vi.mock('../../lib/auth', () => ({ useAuth: () => ({ isTenantOwner: true }) }))
vi.mock('../../lib/api', () => ({
  api: {
    contacts: {
      list: vi.fn(),
      create: vi.fn(),
      update: vi.fn(),
      delete: vi.fn(),
      startConversation: vi.fn(),
      import: vi.fn(),
    },
    whatsapp: {
      getLines: vi.fn(),
    },
  },
}))

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter><ContactsPage /></MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('ContactsPage import', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(api.contacts.list).mockResolvedValue([])
    vi.mocked(api.whatsapp.getLines).mockResolvedValue([])
  })

  it('uploads the selected spreadsheet and shows the result', async () => {
    vi.mocked(api.contacts.import).mockResolvedValue({
      total: 3,
      imported: 1,
      skipped: 1,
      invalid: 1,
      errors: [{ row: 4, code: 'invalid_contact', message: 'Contato inválido.' }],
    })
    renderPage()

    fireEvent.click(screen.getByRole('button', { name: 'Importar' }))
    const file = new File(['nome,contato\nAna,5511999990000'], 'contatos.csv', { type: 'text/csv' })
    fireEvent.change(screen.getByLabelText('Arquivo *'), { target: { files: [file] } })
    fireEvent.submit(screen.getByRole('button', { name: 'Importar contatos' }).closest('form')!)

    await waitFor(() => expect(api.contacts.import).toHaveBeenCalledWith(file))
    expect(await screen.findByText('1 importados, 1 ignorados e 1 inválidos.')).toBeInTheDocument()
    expect(screen.getByText('Linha 4: Contato inválido.')).toBeInTheDocument()
  })

  it('asks which company line should start a conversation when multiple lines are active', async () => {
    vi.mocked(api.contacts.list).mockResolvedValue([{
      id: 'contact-1',
      phoneNumber: '5571999999999',
      name: 'Cliente',
      createdAt: '2026-09-10T00:00:00Z',
    }])
    vi.mocked(api.whatsapp.getLines).mockResolvedValue([
      { lineNumber: 1, connectionType: 'QrCode', phoneNumberId: 'qr:tenant:1', isActive: true },
      { lineNumber: 2, connectionType: 'QrCode', phoneNumberId: 'qr:tenant:2', isActive: true },
    ])
    vi.mocked(api.contacts.startConversation).mockResolvedValue({ conversationId: 'conversation-1' })

    renderPage()

    fireEvent.click(await screen.findByRole('button', { name: /Conversar/i }))
    expect(screen.getByRole('heading', { name: 'Escolher linha' })).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: /QR Code — linha 2/i }))
    fireEvent.click(screen.getByRole('button', { name: 'Abrir conversa' }))

    await waitFor(() => expect(api.contacts.startConversation).toHaveBeenCalledWith('contact-1', 'qr:tenant:2'))
  })
})

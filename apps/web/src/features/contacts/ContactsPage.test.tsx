import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { api } from '../../lib/api'
import { ContactsPage } from './ContactsPage'

let signalROptions: { onMessage?: (payload: unknown) => void } | undefined

vi.mock('../../lib/auth', () => ({ useAuth: () => ({ isTenantOwner: true }) }))
vi.mock('../../lib/signalr', () => ({
  useSignalR: (options: typeof signalROptions) => {
    signalROptions = options
    return { start: vi.fn() }
  },
}))
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
    serviceQueues: {
      list: vi.fn(),
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
    signalROptions = undefined
    vi.mocked(api.contacts.list).mockResolvedValue([])
    vi.mocked(api.whatsapp.getLines).mockResolvedValue([])
    vi.mocked(api.serviceQueues.list).mockResolvedValue([])
  })

  it('refreshes the list when a new inbound message is announced', async () => {
    const newContact = {
      id: 'contact-new',
      phoneNumber: '5511999990000',
      name: 'Novo contato',
      createdAt: '2026-09-11T00:00:00Z',
    }
    vi.mocked(api.contacts.list)
      .mockResolvedValueOnce([])
      .mockResolvedValueOnce([newContact])

    renderPage()

    await waitFor(() => expect(api.contacts.list).toHaveBeenCalledTimes(1))
    signalROptions?.onMessage?.({ conversationId: 'conversation-new' })

    expect(await screen.findByText('Novo contato')).toBeInTheDocument()
    expect(api.contacts.list).toHaveBeenCalledTimes(2)
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

    await waitFor(() => expect(api.contacts.import).toHaveBeenCalledWith(file, undefined))
    expect(await screen.findByRole('status')).toHaveTextContent('1 adicionados à agenda, 1 já existentes ou duplicados e 1 inválidos.')
  })

  it('refreshes the agenda and shows a clear success message after importing contacts', async () => {
    const importedContact = {
      id: 'contact-imported',
      phoneNumber: '5511999990000',
      name: 'Ana Importada',
      createdAt: '2026-09-11T00:00:00Z',
    }
    vi.mocked(api.contacts.list)
      .mockResolvedValueOnce([])
      .mockResolvedValueOnce([importedContact])
    vi.mocked(api.contacts.import).mockResolvedValue({
      total: 1, imported: 1, skipped: 0, invalid: 0, errors: [],
    })
    renderPage()

    fireEvent.click(screen.getByRole('button', { name: 'Importar' }))
    const file = new File(['nome,contato\nAna Importada,5511999990000'], 'contatos.csv', { type: 'text/csv' })
    fireEvent.change(screen.getByLabelText('Arquivo *'), { target: { files: [file] } })
    fireEvent.submit(screen.getByRole('button', { name: 'Importar contatos' }).closest('form')!)

    expect(await screen.findByRole('status')).toHaveTextContent('1 adicionados à agenda')
    expect(await screen.findByText('Ana Importada')).toBeInTheDocument()
    expect(api.contacts.list).toHaveBeenCalledTimes(2)
  })

  it('shows a load error instead of claiming the contacts list is empty', async () => {
    vi.mocked(api.contacts.list).mockRejectedValue(new Error('Falha de conexão'))
    renderPage()

    expect(await screen.findByRole('alert')).toHaveTextContent('Não foi possível carregar a lista de contatos: Falha de conexão')
    expect(screen.queryByText('Nenhum contato cadastrado.')).not.toBeInTheDocument()
  })

  it('sends the selected queue with the imported spreadsheet', async () => {
    vi.mocked(api.serviceQueues.list).mockResolvedValue([{
      id: 'queue-1', name: 'Clientes ativos', isActive: true, sortOrder: 0,
    }])
    vi.mocked(api.contacts.import).mockResolvedValue({
      total: 1, imported: 1, skipped: 0, invalid: 0, errors: [],
    })
    renderPage()

    fireEvent.click(screen.getByRole('button', { name: 'Importar' }))
    await screen.findByRole('option', { name: 'Clientes ativos' })
    const queueSelect = await screen.findByLabelText('Fila para o disparo')
    fireEvent.change(queueSelect, { target: { value: 'queue-1' } })
    const file = new File(['nome,contato\nAna,5511999990000'], 'contatos.csv', { type: 'text/csv' })
    fireEvent.change(screen.getByLabelText('Arquivo *'), { target: { files: [file] } })
    fireEvent.submit(screen.getByRole('button', { name: 'Importar contatos' }).closest('form')!)

    await waitFor(() => expect(api.contacts.import).toHaveBeenCalledWith(file, 'queue-1'))
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

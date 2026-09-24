import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { api } from '../../lib/api'
import { BroadcastPage } from './BroadcastPage'

vi.mock('../../lib/auth', () => ({
  useAuth: () => ({ isOperator: false, user: undefined }),
}))

vi.mock('../../lib/api', () => ({
  api: {
    broadcasts: {
      list: vi.fn(),
      get: vi.fn(),
      listOfficialTemplates: vi.fn(),
      create: vi.fn(),
      dispatch: vi.fn(),
      update: vi.fn(),
      retryFailed: vi.fn(),
      cancel: vi.fn(),
      delete: vi.fn(),
    },
    contacts: {
      list: vi.fn(),
    },
    serviceQueues: {
      list: vi.fn(),
    },
    whatsapp: {
      getLines: vi.fn(),
    },
  },
}))

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 } },
  })

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter><BroadcastPage /></MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('BroadcastPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(api.broadcasts.list).mockResolvedValue([])
    vi.mocked(api.contacts.list).mockResolvedValue([{
      id: 'contact-1',
      phoneNumber: '5511999999999',
      name: 'Cliente',
      createdAt: '2026-09-11T00:00:00Z',
    }])
    vi.mocked(api.serviceQueues.list).mockResolvedValue([{
      id: 'queue-1',
      name: 'Vendas',
      sortOrder: 1,
      isActive: true,
    }])
    vi.mocked(api.whatsapp.getLines).mockResolvedValue([])
    vi.mocked(api.broadcasts.listOfficialTemplates).mockResolvedValue({ templates: [] })
  })

  it('saves the selected queue when creating a broadcast', async () => {
    vi.mocked(api.broadcasts.create).mockResolvedValue({
      id: 'broadcast-1',
      name: 'Aviso',
      message: 'Mensagem',
      status: 'Draft',
      queueId: 'queue-1',
      totalCount: 1,
      sentCount: 0,
      failedCount: 0,
      createdAt: '2026-09-11T00:00:00Z',
    })

    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: 'Novo Disparo' }))
    await screen.findByRole('option', { name: 'Vendas' })
    fireEvent.change(screen.getByLabelText('Fila de atendimento'), { target: { value: 'queue-1' } })
    fireEvent.click(screen.getByLabelText('Todos os contatos desta fila'))
    fireEvent.change(screen.getByLabelText('Nome da lista *'), { target: { value: 'Aviso' } })
    fireEvent.change(screen.getByLabelText('Mensagem *'), { target: { value: 'Mensagem' } })
    fireEvent.click(screen.getByRole('button', { name: 'Criar Lista' }))

    await waitFor(() => expect(api.broadcasts.create).toHaveBeenCalledWith({
      name: 'Aviso',
      message: 'Mensagem',
      deliveryMode: 0,
      linePhoneNumberId: undefined,
      templateName: undefined,
      templateLanguage: undefined,
      templateBodyParameters: [],
      contactIds: [],
      queueId: 'queue-1',
    }))
  })

  it('uses the entire selected queue instead of the first 500 loaded contacts', async () => {
    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: 'Novo Disparo' }))
    await screen.findByRole('option', { name: 'Vendas' })
    fireEvent.change(screen.getByLabelText('Fila de atendimento'), { target: { value: 'queue-1' } })
    fireEvent.click(screen.getByLabelText('Todos os contatos desta fila'))

    expect(await screen.findByText(/Todos os contatos desta fila serão incluídos/)).toBeInTheDocument()
    expect(api.contacts.list).toHaveBeenCalledWith(undefined, 500, 'queue-1')
  })

  it('allows selecting individual contacts from the chosen queue', async () => {
    vi.mocked(api.contacts.list).mockImplementation(async (_search, _limit, queueId) =>
      queueId === 'queue-1'
        ? [{ id: 'contact-2', phoneNumber: '5511888888888', name: 'Cliente da fila', createdAt: '2026-09-11T00:00:00Z' }]
        : [{ id: 'contact-1', phoneNumber: '5511999999999', name: 'Cliente geral', createdAt: '2026-09-11T00:00:00Z' }])
    vi.mocked(api.broadcasts.create).mockResolvedValue({
      id: 'broadcast-2', name: 'Aviso', message: 'Mensagem', status: 'Draft', queueId: 'queue-1',
      totalCount: 1, sentCount: 0, failedCount: 0, createdAt: '2026-09-11T00:00:00Z',
    })

    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: 'Novo Disparo' }))
    await screen.findByRole('option', { name: 'Vendas' })
    fireEvent.change(screen.getByLabelText('Fila de atendimento'), { target: { value: 'queue-1' } })
    await waitFor(() => expect(api.contacts.list).toHaveBeenCalledWith(undefined, 500, 'queue-1'))
    await screen.findByText('Cliente da fila')
    expect(screen.queryByText('Cliente geral')).not.toBeInTheDocument()
    fireEvent.click(screen.getByText('Cliente da fila').closest('label')!.querySelector('input')!)
    fireEvent.change(screen.getByLabelText('Nome da lista *'), { target: { value: 'Aviso' } })
    fireEvent.change(screen.getByLabelText('Mensagem *'), { target: { value: 'Mensagem' } })
    fireEvent.click(screen.getByRole('button', { name: 'Criar Lista' }))

    await waitFor(() => expect(api.broadcasts.create).toHaveBeenCalledWith(expect.objectContaining({
      contactIds: ['contact-2'], queueId: 'queue-1',
    })))
  })

  it('shows a retry option when queue contacts fail to load', async () => {
    vi.mocked(api.contacts.list).mockImplementation(async (_search, _limit, queueId) => {
      if (queueId === 'queue-1') throw new Error('Contact lookup failed')
      return []
    })

    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: 'Novo Disparo' }))
    await screen.findByRole('option', { name: 'Vendas' })
    fireEvent.change(screen.getByLabelText('Fila de atendimento'), { target: { value: 'queue-1' } })

    expect(await screen.findByText('Não foi possível carregar os contatos desta fila.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Tentar novamente' })).toBeInTheDocument()
    expect(screen.queryByText('Nenhum contato encontrado.')).not.toBeInTheDocument()
  })

  it('allows selecting individual contacts from the chosen queue', async () => {
    vi.mocked(api.contacts.list).mockImplementation(async (_search, _limit, queueId) =>
      queueId === 'queue-1'
        ? [{ id: 'contact-2', phoneNumber: '5511888888888', name: 'Cliente da fila', createdAt: '2026-09-11T00:00:00Z' }]
        : [{ id: 'contact-1', phoneNumber: '5511999999999', name: 'Cliente geral', createdAt: '2026-09-11T00:00:00Z' }])
    vi.mocked(api.broadcasts.create).mockResolvedValue({
      id: 'broadcast-2', name: 'Aviso', message: 'Mensagem', status: 'Draft', queueId: 'queue-1',
      totalCount: 1, sentCount: 0, failedCount: 0, createdAt: '2026-09-11T00:00:00Z',
    })

    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: 'Novo Disparo' }))
    await screen.findByRole('option', { name: 'Vendas' })
    fireEvent.change(screen.getByLabelText('Fila de atendimento'), { target: { value: 'queue-1' } })
    fireEvent.click(screen.getByLabelText('Selecionar contatos desta fila'))

    await waitFor(() => expect(api.contacts.list).toHaveBeenCalledWith(undefined, 500, 'queue-1'))
    await screen.findByText('Cliente da fila')
    expect(screen.queryByText('Cliente geral')).not.toBeInTheDocument()
    fireEvent.click(screen.getByText('Cliente da fila').closest('label')!.querySelector('input')!)
    fireEvent.change(screen.getByLabelText('Nome da lista *'), { target: { value: 'Aviso' } })
    fireEvent.change(screen.getByLabelText('Mensagem *'), { target: { value: 'Mensagem' } })
    fireEvent.click(screen.getByRole('button', { name: 'Criar Lista' }))

    await waitFor(() => expect(api.broadcasts.create).toHaveBeenCalledWith(expect.objectContaining({
      contactIds: ['contact-2'], queueId: 'queue-1',
    })))
  })

  it('creates an official broadcast with the selected approved template and body parameters', async () => {
    vi.mocked(api.whatsapp.getLines).mockResolvedValue([{
      lineNumber: 2,
      connectionType: 'OfficialApi',
      phoneNumberId: 'official-2',
      isActive: true,
    }])
    vi.mocked(api.broadcasts.listOfficialTemplates).mockResolvedValue({
      templates: [{ name: 'status_update', language: 'pt_BR', bodyParameterCount: 1, category: 'UTILITY', status: 'APPROVED', isCompatible: true, canSendInInbox: true, canSendInBroadcast: true }],
    })
    vi.mocked(api.broadcasts.create).mockResolvedValue({
      id: 'broadcast-official', name: 'Atualização', message: '', deliveryMode: 'OfficialApiTemplate',
      templateName: 'status_update', templateLanguage: 'pt_BR', status: 'Draft', totalCount: 1,
      sentCount: 0, failedCount: 0, createdAt: '2026-09-11T00:00:00Z',
    })

    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: 'Novo Disparo' }))
    fireEvent.change(screen.getByLabelText('Canal de envio'), { target: { value: 'OfficialApiTemplate' } })
    await screen.findByRole('option', { name: '2' })
    fireEvent.change(screen.getByLabelText('Linha oficial'), { target: { value: 'official-2' } })
    await waitFor(() => expect(screen.getByLabelText('Linha oficial')).toHaveValue('official-2'))
    await waitFor(() => expect(api.broadcasts.listOfficialTemplates).toHaveBeenCalledWith('official-2'))
    fireEvent.change(screen.getByLabelText('Template aprovado'), { target: { value: 'status_update:pt_BR' } })
    fireEvent.change(screen.getByLabelText('Parâmetro 1'), { target: { value: 'Pedido 10' } })
    fireEvent.change(screen.getByLabelText('Nome da lista *'), { target: { value: 'Atualização' } })
    fireEvent.click(screen.getByText('Cliente').closest('label')!.querySelector('input')!)
    fireEvent.click(screen.getByRole('button', { name: 'Criar Lista' }))

    await waitFor(() => expect(api.broadcasts.create).toHaveBeenCalledWith(expect.objectContaining({
      deliveryMode: 1, linePhoneNumberId: 'official-2', templateName: 'status_update',
      templateLanguage: 'pt_BR', templateBodyParameters: ['Pedido 10'],
    })))
  })

  it('does not ask for a queue again when dispatching a saved broadcast', async () => {
    vi.mocked(api.broadcasts.list).mockResolvedValue([{
      id: 'broadcast-1',
      name: 'Aviso',
      message: 'Mensagem',
      status: 'Draft',
      queueId: 'queue-1',
      totalCount: 1,
      sentCount: 0,
      failedCount: 0,
      createdAt: '2026-09-11T00:00:00Z',
    }])
    vi.mocked(api.whatsapp.getLines).mockResolvedValue([{
      lineNumber: 1,
      connectionType: 'QrCode',
      phoneNumberId: 'qr:tenant:1:1',
      isActive: true,
    }])
    vi.mocked(api.broadcasts.dispatch).mockResolvedValue({
      id: 'broadcast-1',
      name: 'Aviso',
      message: 'Mensagem',
      status: 'Sending',
      queueId: 'queue-1',
      totalCount: 1,
      sentCount: 0,
      failedCount: 0,
      createdAt: '2026-09-11T00:00:00Z',
    })

    renderPage()
    fireEvent.click(await screen.findByTitle('Disparar'))

    await screen.findByLabelText('Linha QR Code')
    expect(screen.getAllByRole('combobox')).toHaveLength(1)
    expect(screen.getByText('Vendas')).toBeInTheDocument()
    fireEvent.change(screen.getByLabelText('Linha QR Code'), { target: { value: 'qr:tenant:1:1' } })
    fireEvent.click(screen.getAllByRole('button', { name: 'Disparar' }).at(-1)!)

    await waitFor(() => expect(api.broadcasts.dispatch).toHaveBeenCalledWith(
      'broadcast-1',
      'qr:tenant:1:1',
    ))
  })

  it('allows editing the message while the broadcast is a draft', async () => {
    vi.mocked(api.broadcasts.list).mockResolvedValue([{
      id: 'broadcast-1',
      name: 'Aviso',
      message: 'Mensagem antiga',
      status: 'Draft',
      queueId: 'queue-1',
      totalCount: 1,
      sentCount: 0,
      failedCount: 0,
      createdAt: '2026-09-11T00:00:00Z',
    }])
    vi.mocked(api.broadcasts.update).mockResolvedValue({
      id: 'broadcast-1',
      name: 'Aviso',
      message: 'Mensagem nova',
      status: 'Draft',
      queueId: 'queue-1',
      totalCount: 1,
      sentCount: 0,
      failedCount: 0,
      createdAt: '2026-09-11T00:00:00Z',
    })

    renderPage()
    fireEvent.click(await screen.findByTitle('Editar mensagem'))
    fireEvent.change(screen.getByLabelText('Mensagem'), { target: { value: 'Mensagem nova' } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar mensagem' }))

    await waitFor(() => expect(api.broadcasts.update).toHaveBeenCalledWith(
      'broadcast-1',
      'Mensagem nova',
    ))
  })

  it('allows resending only failed recipients', async () => {
    vi.mocked(api.broadcasts.list).mockResolvedValue([{
      id: 'broadcast-1',
      name: 'Aviso',
      message: 'Mensagem',
      status: 'Completed',
      queueId: 'queue-1',
      totalCount: 2,
      sentCount: 1,
      failedCount: 1,
      createdAt: '2026-09-11T00:00:00Z',
    }])
    vi.mocked(api.broadcasts.retryFailed).mockResolvedValue({
      id: 'broadcast-1',
      name: 'Aviso',
      message: 'Mensagem',
      status: 'Sending',
      queueId: 'queue-1',
      totalCount: 2,
      sentCount: 1,
      failedCount: 0,
      createdAt: '2026-09-11T00:00:00Z',
    })

    renderPage()
    fireEvent.click(await screen.findByTitle('Reenviar falhas'))

    await waitFor(() => expect(api.broadcasts.retryFailed).toHaveBeenCalledWith('broadcast-1'))
  })
})

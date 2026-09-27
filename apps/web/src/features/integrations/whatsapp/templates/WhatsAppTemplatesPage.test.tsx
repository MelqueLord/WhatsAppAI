import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { WhatsAppTemplatesPage } from './WhatsAppTemplatesPage'

const api = vi.hoisted(() => ({
  whatsapp: { getLines: vi.fn(), listTemplates: vi.fn(), createTemplate: vi.fn(), syncTemplates: vi.fn() },
}))
vi.mock('../../../../lib/api', () => ({ api }))

function renderPage() {
  return render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><WhatsAppTemplatesPage /></QueryClientProvider>)
}

describe('WhatsAppTemplatesPage', () => {
  beforeEach(() => {
    api.whatsapp.getLines.mockResolvedValue([{ lineNumber: 1, connectionType: 'OfficialApi', phoneNumberId: 'phone-1', isActive: true }])
    api.whatsapp.listTemplates.mockResolvedValue({ templates: [] })
    api.whatsapp.createTemplate.mockResolvedValue({ templateId: 'template-1', submissionId: 'submission-1', submissionStatus: 'QUEUED', reviewStatus: 'PENDING' })
    api.whatsapp.syncTemplates.mockResolvedValue({ status: 'QUEUED', count: 1 })
  })

  it('collects one example for each positional parameter before submission', async () => {
    renderPage()
    const nameInput = await screen.findByLabelText('Nome')
    fireEvent.change(nameInput, { target: { value: 'atualizacao' } })
    fireEvent.change(screen.getByLabelText(/Corpo/), { target: { value: 'Olá {{1}}' } })
    fireEvent.change(await screen.findByLabelText('Exemplo para {{1}}'), { target: { value: 'Maria' } })
    fireEvent.click(screen.getByRole('button', { name: 'Enviar para análise' }))
    await waitFor(() => expect(api.whatsapp.createTemplate).toHaveBeenCalledWith(1, expect.any(String), expect.objectContaining({ bodyExamples: ['Maria'] })))
  })
})

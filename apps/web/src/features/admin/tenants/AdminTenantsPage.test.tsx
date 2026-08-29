import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { api, type Tenant } from '../../../lib/api'
import { AdminTenantsPage } from './AdminTenantsPage'

vi.mock('../../../lib/api', () => ({
  api: {
    plans: { list: vi.fn() },
    admin: {
      tenants: {
        list: vi.fn(),
        capacity: vi.fn(),
        create: vi.fn(),
        update: vi.fn(),
        suspend: vi.fn(),
        reactivate: vi.fn(),
        registerPayment: vi.fn(),
        updatePlan: vi.fn(),
        resetOwnerPassword: vi.fn(),
      },
    },
  },
}))

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <AdminTenantsPage />
    </QueryClientProvider>,
  )
}

describe('AdminTenantsPage capacity', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(api.admin.tenants.list).mockResolvedValue([])
    vi.mocked(api.plans.list).mockResolvedValue([])
    vi.mocked(api.admin.tenants.capacity).mockResolvedValue({
      customers: { current: 0, limit: 25, utilizationPercentage: 0, status: 'Normal' },
      lines: { current: 0, limit: 40, utilizationPercentage: 0, status: 'Normal' },
      operators: { current: 0, limit: 90, utilizationPercentage: 0, status: 'Normal' },
      migrationRequired: false,
    })
  })

  it('shows the infrastructure totals and migration alert at the limit', async () => {
    vi.mocked(api.admin.tenants.capacity).mockResolvedValue({
      customers: { current: 25, limit: 25, utilizationPercentage: 100, status: 'MigrationRequired' },
      lines: { current: 32, limit: 40, utilizationPercentage: 80, status: 'Warning' },
      operators: { current: 45, limit: 90, utilizationPercentage: 50, status: 'Normal' },
      migrationRequired: true,
    })

    renderPage()

    expect(await screen.findByText('Capacidade atingida: migre para outro KVM')).toBeInTheDocument()
    expect(screen.getByText('25 / 25')).toBeInTheDocument()
    expect(screen.getByText('32 / 40')).toBeInTheDocument()
    expect(screen.getByText('45 / 90')).toBeInTheDocument()
  })

  it('applies the selected plan defaults and keeps the AI allowance editable', async () => {
    vi.mocked(api.plans.list).mockResolvedValue([
      {
        id: 'star', name: 'STAR', code: 'STAR', description: 'Essencial', aiEnabled: true,
        botEnabled: false, tagsEnabled: false, automaticDistributionEnabled: false,
        isSelectable: true, defaultOfficialApiLineCount: 1, defaultOperatorLimit: 2,
        defaultMonthlyAiResponseLimit: 1500,
      },
      {
        id: 'flow', name: 'FLOW', code: 'FLOW', description: 'Agilidade', aiEnabled: true,
        botEnabled: true, tagsEnabled: true, automaticDistributionEnabled: true,
        isSelectable: true, defaultOfficialApiLineCount: 2, defaultOperatorLimit: 4,
        defaultMonthlyAiResponseLimit: 5000,
      },
    ])

    renderPage()
    const newCompany = await screen.findByRole('button', { name: 'Nova Empresa' })
    await waitFor(() => expect(newCompany).toBeEnabled())
    fireEvent.click(newCompany)

    const plan = screen.getByLabelText('Plano *')
    const allowance = screen.getByLabelText('Respostas da IA por mês')
    expect(allowance).toHaveValue(1500)

    fireEvent.change(plan, { target: { value: 'FLOW' } })
    expect(allowance).toHaveValue(5000)

    fireEvent.change(allowance, { target: { value: '6500' } })
    expect(allowance).toHaveValue(6500)
  })

  it('filters tenants by AI allowance status', async () => {
    const makeTenant = (id: string, used: number, limit: number | null): Tenant => ({
      id, name: `Empresa ${id}`, slug: id, planId: '', status: 'Active', version: 0,
      createdAt: new Date().toISOString(), dueDate: new Date().toISOString(),
      officialApiLineCount: 1, qrCodeLineCount: 0, operatorLimit: 2,
      monthlyAiResponseLimit: limit, monthlyAiResponsesUsed: used,
    })
    vi.mocked(api.admin.tenants.list).mockResolvedValue([
      makeTenant('normal', 100, 1500),
      makeTenant('warning', 1200, 1500),
      makeTenant('exhausted', 1500, 1500),
      makeTenant('unlimited', 9000, null),
    ])

    renderPage()

    expect(await screen.findByText('Empresa normal')).toBeInTheDocument()
    const filter = screen.getByLabelText('Filtrar franquia de IA')
    fireEvent.change(filter, { target: { value: 'exhausted' } })

    expect(screen.getByText('Empresa exhausted')).toBeInTheDocument()
    expect(screen.queryByText('Empresa normal')).not.toBeInTheDocument()
    expect(screen.getByText('Esgotadas (1)')).toBeInTheDocument()
  })
})

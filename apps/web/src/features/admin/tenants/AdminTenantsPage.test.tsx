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
        quotaAlerts: vi.fn(),
        aiUsage: vi.fn(),
        addAiResponseTopUp: vi.fn(),
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
    vi.mocked(api.admin.tenants.quotaAlerts).mockResolvedValue([])
    vi.mocked(api.admin.tenants.aiUsage).mockResolvedValue({
      periodStart: '2026-08-01T00:00:00Z',
      periodEnd: '2026-09-01T00:00:00Z',
      contractedModel: { provider: 'openai', modelId: 'gpt-4.1-mini' },
      responsePackage: {
        baseLimit: 1500, topUps: 0, limit: 1500, used: 0, remaining: 1500,
        status: 'normal', aiSuspended: false,
      },
      tokens: { input: 0, output: 0, total: 0, estimatedCostMinorUnits: 0 },
      byProvider: [],
      byModel: [],
    })
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
        isSelectable: true, defaultLineCount: 1, defaultOperatorLimit: 2,
        defaultMonthlyAiResponseLimit: 1500,
      },
      {
        id: 'flow', name: 'FLOW', code: 'FLOW', description: 'Agilidade', aiEnabled: true,
        botEnabled: true, tagsEnabled: true, automaticDistributionEnabled: true,
        isSelectable: true, defaultLineCount: 2, defaultOperatorLimit: 4,
        defaultMonthlyAiResponseLimit: 5000,
      },
    ])

    renderPage()
    const newCompany = await screen.findByRole('button', { name: 'Nova Empresa' })
    await waitFor(() => expect(newCompany).toBeEnabled())
    fireEvent.click(newCompany)

    const plan = screen.getByLabelText('Plano *')
    const allowance = screen.getByLabelText('Respostas da IA por mês')
    const lineDistribution = screen.getByLabelText('Distribuição das linhas')
    expect(allowance).toHaveValue(1500)
    expect(lineDistribution).toHaveValue('1')

    fireEvent.change(lineDistribution, { target: { value: '0' } })
    expect(lineDistribution).toHaveValue('0')
    expect(screen.getByRole('option', { name: '0 API Oficial + 1 QR Code' })).toBeInTheDocument()

    fireEvent.change(plan, { target: { value: 'FLOW' } })
    expect(allowance).toHaveValue(5000)
    expect(lineDistribution).toHaveValue('2')

    fireEvent.change(lineDistribution, { target: { value: '1' } })
    expect(screen.getByRole('option', { name: '1 API Oficial + 1 QR Code' })).toBeInTheDocument()

    fireEvent.change(allowance, { target: { value: '6500' } })
    expect(allowance).toHaveValue(6500)
  })

  it('filters tenants by AI allowance status', async () => {
    const makeTenant = (id: string, used: number, limit: number | null): Tenant => ({
      id, name: `Empresa ${id}`, slug: id, planId: '', status: 'Active', version: 0,
      createdAt: new Date().toISOString(), dueDate: new Date().toISOString(),
      officialApiLineCount: 1, qrCodeLineCount: 0, operatorLimit: 2,
      monthlyAiResponseLimit: limit, monthlyAiResponsesUsed: used,
      monthlyAiResponseStatus: id as 'normal' | 'warning' | 'exhausted' | 'unlimited',
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
    expect(screen.getByText('4.500')).toBeInTheDocument()
    expect(screen.getByText('11.800')).toBeInTheDocument()
  })

  it('opens the tenant quota alert history', async () => {
    const tenant: Tenant = {
      id: 'tenant-alerts', name: 'Empresa alertas', slug: 'empresa-alertas', planId: '', status: 'Active', version: 0,
      createdAt: new Date().toISOString(), dueDate: new Date().toISOString(), officialApiLineCount: 1,
      qrCodeLineCount: 0, operatorLimit: 2, monthlyAiResponseLimit: 1500,
      monthlyAiResponsesUsed: 1200,
      monthlyAiResponseStatus: 'warning',
    }
    vi.mocked(api.admin.tenants.list).mockResolvedValue([tenant])
    vi.mocked(api.admin.tenants.quotaAlerts).mockResolvedValue([{
      action: 'AiQuota.WarningReached', entityId: '2026-08:Warning',
      details: 'period=2026-08;used=1200;limit=1500', occurredAt: new Date().toISOString(),
    }])

    renderPage()
    const alertButton = await screen.findByRole('button', { name: 'Ver alertas de franquia de Empresa alertas' })
    fireEvent.click(alertButton)

    expect(await screen.findByText('Alertas de franquia')).toBeInTheDocument()
    expect(await screen.findByText('Alerta de 80%')).toBeInTheDocument()
    expect(screen.getByText('period=2026-08;used=1200;limit=1500')).toBeInTheDocument()
  })

  it('adds exactly 500 responses to an exhausted tenant package', async () => {
    const tenant: Tenant = {
      id: 'tenant-topup', name: 'Empresa esgotada', slug: 'empresa-esgotada', planId: '', status: 'Active', version: 0,
      createdAt: new Date().toISOString(), dueDate: new Date().toISOString(), officialApiLineCount: 1,
      qrCodeLineCount: 0, operatorLimit: 2, monthlyAiBaseResponseLimit: 1500,
      monthlyAiResponseTopUps: 0, monthlyAiResponseLimit: 1500, monthlyAiResponsesUsed: 1500,
      monthlyAiResponseStatus: 'exhausted', isAiSuspendedByQuota: true,
    }
    vi.mocked(api.admin.tenants.list).mockResolvedValue([tenant])
    vi.mocked(api.admin.tenants.addAiResponseTopUp).mockResolvedValue({
      added: true, quantity: 500, baseLimit: 1500, topUps: 500, limit: 2000,
      used: 1500, remaining: 500, status: 'normal', aiSuspended: false,
    })

    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: '+500 respostas' }))

    await waitFor(() => expect(api.admin.tenants.addAiResponseTopUp).toHaveBeenCalledWith(
      tenant.id,
      expect.any(String),
    ))
  })

  it('shows real token usage and provider distribution for a tenant', async () => {
    const tenant: Tenant = {
      id: 'tenant-usage', name: 'Empresa consumo', slug: 'empresa-consumo', planId: '', status: 'Active', version: 0,
      createdAt: new Date().toISOString(), dueDate: new Date().toISOString(), officialApiLineCount: 1,
      qrCodeLineCount: 0, operatorLimit: 2, monthlyAiResponseLimit: 1500,
      monthlyAiResponsesUsed: 900, monthlyAiResponseStatus: 'warning',
      monthlyAiTokensUsed: 4200,
    }
    vi.mocked(api.admin.tenants.list).mockResolvedValue([tenant])
    vi.mocked(api.admin.tenants.aiUsage).mockResolvedValue({
      periodStart: '2026-08-01T00:00:00Z',
      periodEnd: '2026-09-01T00:00:00Z',
      contractedModel: { provider: 'openai', modelId: 'gpt-4.1-mini' },
      responsePackage: {
        baseLimit: 1500, topUps: 0, limit: 1500, used: 900, remaining: 600,
        status: 'warning', aiSuspended: false,
      },
      tokens: { input: 3000, output: 1200, total: 4200, estimatedCostMinorUnits: 12 },
      byProvider: [{ provider: 'openai', metric: 'input_tokens', tokens: 3000, estimatedCostMinorUnits: 8 }],
      byModel: [{ modelId: 'gpt-4.1-mini', inputTokens: 3000, outputTokens: 1200, interactions: 30 }],
    })

    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: 'Ver consumo de IA de Empresa consumo' }))

    expect(await screen.findByText('Consumo real de IA')).toBeInTheDocument()
    expect(await screen.findByText('4.200')).toBeInTheDocument()
    expect(await screen.findByText('openai')).toBeInTheDocument()
    expect(await screen.findByText('gpt-4.1-mini')).toBeInTheDocument()
  })
})

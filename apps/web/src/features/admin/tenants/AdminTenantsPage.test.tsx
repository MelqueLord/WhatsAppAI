import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { api } from '../../../lib/api'
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
})

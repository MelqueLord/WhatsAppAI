import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'
import { Sidebar } from './Sidebar'

vi.mock('../lib/auth', () => ({
  useAuth: () => ({
    user: {
      displayName: 'Empresa',
      email: 'empresa@example.com',
      role: 'TenantOwner',
      aiEnabled: true,
      tagsEnabled: false,
      automaticDistributionEnabled: false,
    },
    isPlatformAdmin: false,
    isTenantOwner: true,
    isOperator: false,
    logout: vi.fn(),
  }),
}))

describe('Sidebar', () => {
  it('keeps queue and tag management visible when operational features are unavailable', () => {
    render(
      <MemoryRouter>
        <Sidebar collapsed={false} onToggle={vi.fn()} />
      </MemoryRouter>,
    )

    expect(screen.getByRole('link', { name: 'Filas' })).toHaveAttribute('href', '/queues')
    expect(screen.getByRole('link', { name: 'Tags' })).toHaveAttribute('href', '/tags')
    expect(screen.getByRole('link', { name: 'Teste IA' })).toHaveAttribute('href', '/integrations/ai/scenarios')
    expect(screen.getByText('Administrador da empresa')).toBeInTheDocument()
    expect(screen.queryByText('TenantOwner')).not.toBeInTheDocument()
  })
})

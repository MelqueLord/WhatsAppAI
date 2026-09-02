import { fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { Sidebar } from './Sidebar'

const authState = vi.hoisted(() => ({ isOperator: false }))

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
    isOperator: authState.isOperator,
    logout: vi.fn(),
  }),
}))

describe('Sidebar', () => {
  beforeEach(() => {
    authState.isOperator = false
  })

  it('keeps queue and tag management visible when operational features are unavailable', () => {
    const { container } = render(
      <MemoryRouter>
        <Sidebar collapsed={false} onToggle={vi.fn()} />
      </MemoryRouter>,
    )

    expect(screen.getByRole('link', { name: 'Filas' })).toHaveAttribute('href', '/queues')
    expect(screen.getByRole('link', { name: 'Tags' })).toHaveAttribute('href', '/tags')
    expect(screen.getByRole('link', { name: 'Teste IA' })).toHaveAttribute('href', '/integrations/ai/scenarios')
    expect(screen.getByRole('button', { name: 'Recolher menu' })).toBeVisible()
    expect(screen.getByText('Administrador da empresa')).toBeInTheDocument()
    expect(screen.queryByText('TenantOwner')).not.toBeInTheDocument()
    expect(container.querySelector('aside')).toHaveClass('h-dvh')
    expect(container.querySelector('nav')).toHaveClass('min-h-0', 'overflow-y-auto')
    expect(container.querySelector('nav')?.nextElementSibling).toHaveClass('shrink-0')
  })

  it('keeps the collapse control available after the menu is collapsed', () => {
    const onToggle = vi.fn()
    const { rerender } = render(
      <MemoryRouter>
        <Sidebar collapsed={false} onToggle={onToggle} />
      </MemoryRouter>,
    )

    fireEvent.click(screen.getByRole('button', { name: 'Recolher menu' }))
    expect(onToggle).toHaveBeenCalledOnce()

    rerender(
      <MemoryRouter>
        <Sidebar collapsed onToggle={onToggle} />
      </MemoryRouter>,
    )

    expect(screen.getByRole('button', { name: 'Expandir menu' })).toBeVisible()
  })

  it('provides the same visible collapse control for operators', () => {
    authState.isOperator = true

    render(
      <MemoryRouter>
        <Sidebar collapsed={false} onToggle={vi.fn()} />
      </MemoryRouter>,
    )

    expect(screen.getByRole('button', { name: 'Recolher menu' })).toBeVisible()
  })
})

import { fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'
import { Layout } from './Layout'

vi.mock('../lib/auth', () => ({
  useAuth: () => ({
    mustChangePassword: false,
    user: {
      displayName: 'Empresa',
      email: 'empresa@example.com',
      role: 'TenantOwner',
      aiEnabled: true,
      automaticDistributionEnabled: false,
    },
    isPlatformAdmin: false,
    isTenantOwner: true,
    isOperator: false,
    logout: vi.fn(),
  }),
}))

describe('Layout', () => {
  it('opens the mobile drawer at full width after the desktop sidebar was collapsed', () => {
    render(
      <MemoryRouter initialEntries={['/dashboard']}>
        <Routes>
          <Route element={<Layout />}>
            <Route path="/dashboard" element={<div>Dashboard</div>} />
          </Route>
        </Routes>
      </MemoryRouter>,
    )

    fireEvent.click(screen.getByRole('button', { name: 'Recolher menu' }))
    expect(screen.getByRole('button', { name: 'Expandir menu' })).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Abrir menu' }))
    expect(screen.getByRole('button', { name: 'Recolher menu' })).toBeInTheDocument()
  })
})

import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import App from './App'

const auth = vi.hoisted(() => ({ isTenantOwner: false }))

vi.mock('./lib/auth', () => ({
  AuthProvider: ({ children }: { children: React.ReactNode }) => children,
  useAuth: () => ({
    user: auth.isTenantOwner ? { tenantId: 'tenant-1', role: 'TenantOwner' } : null,
    isLoading: false,
    isAuthenticated: auth.isTenantOwner,
    isPlatformAdmin: false,
    isTenantOwner: auth.isTenantOwner,
    isOperator: false,
  }),
}))

vi.mock('./components/Layout', async () => {
  const { Outlet } = await import('react-router-dom')
  return { Layout: () => <Outlet /> }
})

vi.mock('./features/integrations/whatsapp/templates/WhatsAppTemplatesPage', () => ({
  WhatsAppTemplatesPage: () => <div>Catálogo de templates</div>,
}))

describe('App', () => {
  beforeEach(() => {
    auth.isTenantOwner = false
    window.history.pushState({}, '', '/')
  })

  it('renders the public landing page when not authenticated', () => {
    render(<App />)

    expect(screen.getByRole('link', { name: 'Entrar' })).toHaveAttribute('href', '/login')
  })

  it('renders the templates route for a tenant owner', () => {
    auth.isTenantOwner = true
    window.history.pushState({}, '', '/integrations/whatsapp/templates')

    render(<App />)

    expect(screen.getByText('Catálogo de templates')).toBeInTheDocument()
  })
})

import { Outlet } from 'react-router-dom'
import { Sidebar } from './Sidebar'
import { ChangePasswordDialog } from './ChangePasswordDialog'
import { useAuth } from '../lib/auth'
import { useState } from 'react'

export function Layout() {
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false)
  const { mustChangePassword } = useAuth()

  return (
    <div className="flex h-screen bg-slate-50">
      <Sidebar collapsed={sidebarCollapsed} onToggle={() => setSidebarCollapsed(!sidebarCollapsed)} />
      <main className="flex-1 overflow-hidden">
        <Outlet />
      </main>
      {mustChangePassword && <ChangePasswordDialog />}
    </div>
  )
}

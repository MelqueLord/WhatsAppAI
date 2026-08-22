import { Outlet } from 'react-router-dom'
import { Sidebar } from './Sidebar'
import { ChangePasswordDialog } from './ChangePasswordDialog'
import { useAuth } from '../lib/auth'
import { useState } from 'react'

export function Layout() {
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false)
  const { mustChangePassword } = useAuth()

  return (
    <div className="flex h-screen bg-[#070b16] text-white">
      <Sidebar collapsed={sidebarCollapsed} onToggle={() => setSidebarCollapsed(!sidebarCollapsed)} />
      <main className="relative flex-1 overflow-hidden">
        <div className="pointer-events-none absolute inset-0">
          <div className="absolute -top-24 -right-24 h-80 w-80 rounded-full bg-cyan-500/15 blur-3xl" />
          <div className="absolute bottom-0 left-1/3 h-72 w-72 rounded-full bg-blue-500/10 blur-3xl" />
        </div>
        <div className="app-shell-theme relative z-10 h-full">
          <Outlet />
        </div>
      </main>
      {mustChangePassword && <ChangePasswordDialog />}
    </div>
  )
}

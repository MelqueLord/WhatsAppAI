import { Outlet } from 'react-router-dom'
import { Sidebar } from './Sidebar'
import { ChangePasswordDialog } from './ChangePasswordDialog'
import { useAuth } from '../lib/auth'
import { useState } from 'react'
import { Menu } from 'lucide-react'

export function Layout() {
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false)
  const [mobileOpen, setMobileOpen] = useState(false)
  const { mustChangePassword } = useAuth()

  return (
    <div className="flex h-screen bg-[#070b16] text-white">
      {/* Mobile overlay */}
      {mobileOpen && (
        <div
          className="fixed inset-0 z-30 bg-black/60 lg:hidden"
          onClick={() => setMobileOpen(false)}
        />
      )}

      {/* Sidebar — hidden on mobile unless mobileOpen */}
      <div
        className={`
          fixed inset-y-0 left-0 z-40 transition-transform duration-300 lg:relative lg:translate-x-0
          ${mobileOpen ? 'translate-x-0' : '-translate-x-full lg:translate-x-0'}
        `}
      >
        <Sidebar
          collapsed={sidebarCollapsed}
          onToggle={() => setSidebarCollapsed(!sidebarCollapsed)}
          onMobileClose={() => setMobileOpen(false)}
        />
      </div>

      <main className="relative flex-1 overflow-hidden min-w-0">
        {/* Mobile top bar */}
        <div className="flex items-center gap-3 px-4 h-14 border-b border-white/10 bg-[#0b1222] lg:hidden">
          <button
            onClick={() => {
              setSidebarCollapsed(false)
              setMobileOpen(true)
            }}
            aria-label="Abrir menu"
            title="Abrir menu"
            className="p-2 rounded-lg text-slate-300 hover:text-white hover:bg-white/10 transition-colors"
          >
            <Menu className="w-5 h-5" />
          </button>
          <span className="font-bold text-base tracking-tight">ATENZ</span>
        </div>

        <div className="pointer-events-none absolute inset-0">
          <div className="absolute -top-24 -right-24 h-80 w-80 rounded-full bg-cyan-500/15 blur-3xl" />
          <div className="absolute bottom-0 left-1/3 h-72 w-72 rounded-full bg-blue-500/10 blur-3xl" />
        </div>
        <div className="app-shell-theme relative z-10 h-[calc(100%-3.5rem)] lg:h-full overflow-auto">
          <Outlet />
        </div>
      </main>

      {mustChangePassword && <ChangePasswordDialog />}
    </div>
  )
}

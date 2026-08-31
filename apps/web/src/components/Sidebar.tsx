import { NavLink } from 'react-router-dom'
import { cn } from '../lib/utils'
import { useAuth } from '../lib/auth'
import {
  MessageSquare,
  Users,
  Building2,
  ChevronLeft,
  ChevronRight,
  Zap,
  Shield,
  Bot,
  BookOpen,
  BarChart3,
  LayoutDashboard,
  LogOut,
  Tags,
  ListOrdered,
  Radio,
  Settings,
} from 'lucide-react'
import atenzLogo from '../assets/atenz-logo-a.png'
import { formatUserRole } from '../lib/utils'

interface SidebarProps {
  collapsed: boolean
  onToggle: () => void
  onMobileClose?: () => void
}

export function Sidebar({ collapsed, onToggle, onMobileClose }: SidebarProps) {
  const { user, isPlatformAdmin, isTenantOwner, isOperator, logout } = useAuth()

  const aiEnabled = user?.aiEnabled === true
  const botEnabled = user?.botEnabled === true
  const automaticDistributionEnabled = user?.automaticDistributionEnabled === true

  if (isOperator) {
    return (
      <aside
        className={cn(
          'h-screen flex flex-col border-r border-white/10 bg-gradient-to-b from-[#0b1222] via-[#0b162d] to-[#0a1d2f] text-white transition-all duration-300 ease-in-out relative',
          collapsed ? 'w-[72px]' : 'w-[260px]'
        )}
      >
        <div className="flex items-center gap-3 px-5 h-16 border-b border-white/10">
          <img src={atenzLogo} alt="ATENZ" className="w-9 h-9 object-contain flex-shrink-0" />
          {!collapsed && (
            <div className="overflow-hidden">
              <h1 className="font-bold text-base tracking-tight">ATENZ</h1>
              <p className="text-[10px] text-cyan-200/80 uppercase tracking-widest">Atendimento</p>
            </div>
          )}
        </div>
        <nav className="flex-1 py-4 px-3 space-y-1 overflow-y-auto">
          <NavLink
            to="/inbox"
            className={({ isActive }) =>
              cn(
                'flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-all duration-200',
                isActive
                  ? 'bg-emerald-500/20 text-emerald-400'
                  : 'text-slate-300 hover:bg-white/5 hover:text-white'
              )
            }
          >
            <MessageSquare className="w-5 h-5 flex-shrink-0" />
            {!collapsed && <span>Inbox</span>}
          </NavLink>
          <NavLink
            to="/contacts"
            className={({ isActive }) =>
              cn(
                'flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-all duration-200',
                isActive
                  ? 'bg-emerald-500/20 text-emerald-400'
                  : 'text-slate-300 hover:bg-white/5 hover:text-white'
              )
            }
          >
            <Users className="w-5 h-5 flex-shrink-0" />
            {!collapsed && <span>Contatos</span>}
          </NavLink>
          {automaticDistributionEnabled && (
            <NavLink
              to="/queue-inbox"
              className={({ isActive }) =>
                cn(
                  'flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-all duration-200',
                  isActive ? 'bg-emerald-500/20 text-emerald-400' : 'text-slate-300 hover:bg-white/5 hover:text-white'
                )
              }
            >
              <ListOrdered className="w-5 h-5 flex-shrink-0" />
              {!collapsed && <span>Filas Inbox</span>}
            </NavLink>
          )}
          <NavLink
            to="/broadcast"
            className={({ isActive }) =>
              cn(
                'flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-all duration-200',
                isActive ? 'bg-emerald-500/20 text-emerald-400' : 'text-slate-300 hover:bg-white/5 hover:text-white'
              )
            }
          >
            <Radio className="w-5 h-5 flex-shrink-0" />
            {!collapsed && <span>Disparo em massa</span>}
          </NavLink>
        </nav>
        <div className="border-t border-white/10 p-3">
          <div className={cn('flex items-center', collapsed ? 'justify-center' : 'gap-3')}>
            <div className="w-9 h-9 rounded-full bg-emerald-600 flex items-center justify-center text-white font-semibold text-xs flex-shrink-0">
              {user?.displayName?.split(' ').map((n: string) => n[0]).join('').slice(0, 2).toUpperCase() ?? user?.email?.slice(0, 2).toUpperCase() ?? '??'}
            </div>
            {!collapsed && (
              <div className="flex-1 min-w-0">
                <p className="text-sm font-medium truncate">{user?.displayName || user?.email}</p>
                <p className="text-[10px] text-slate-400 uppercase tracking-wider">Operador</p>
              </div>
            )}
            {!collapsed && (
              <button onClick={logout} className="p-1.5 rounded-lg text-slate-400 hover:text-white hover:bg-white/10 transition-colors" title="Sair">
                <LogOut className="w-4 h-4" />
              </button>
            )}
          </div>
        </div>
      </aside>
    )
  }

  const navItems = [
    ...(isPlatformAdmin ? [] : [{ to: '/dashboard', icon: LayoutDashboard, label: 'Dashboard' }]),
    ...(isPlatformAdmin ? [] : [{ to: '/inbox', icon: MessageSquare, label: 'Inbox' }]),
    ...(isPlatformAdmin ? [] : [{ to: '/contacts', icon: Users, label: 'Contatos' }]),
    ...(isPlatformAdmin || !automaticDistributionEnabled ? [] : [{ to: '/queue-inbox', icon: ListOrdered, label: 'Filas Inbox' }]),
    ...(isTenantOwner
      ? [
          { to: '/operators', icon: Users, label: 'Operadores' },
          { to: '/integrations/whatsapp', icon: Zap, label: 'WhatsApp' },
          ...(botEnabled ? [{ to: '/bot-config', icon: Settings, label: 'Fluxo do Bot' }] : []),
          ...(aiEnabled ? [{ to: '/integrations/ai', icon: Bot, label: 'Diretrizes IA' }] : []),
          { to: '/knowledge', icon: BookOpen, label: 'Conhecimento' },
          // Queue and tag management remains available to the tenant owner.
          // Plan flags only gate their operational use (routing/assignment).
          { to: '/queues', icon: ListOrdered, label: 'Filas' },
          { to: '/tags', icon: Tags, label: 'Tags' },
          { to: '/broadcast', icon: Radio, label: 'Disparo em massa' },
        ]
      : []),
    { to: '/usage', icon: BarChart3, label: 'Uso' },
    ...(isPlatformAdmin
      ? [
          { to: '/admin/tenants', icon: Building2, label: 'Empresas' },
          { to: '/admin/ai-usage', icon: BarChart3, label: 'Uso de IA' },
          { to: '/admin/webhooks', icon: Shield, label: 'Webhooks' },
        ]
      : []),
  ]

  const initials = user?.displayName
    ? user.displayName.split(' ').map((n: string) => n[0]).join('').slice(0, 2).toUpperCase()
    : user?.email?.slice(0, 2).toUpperCase() ?? '??'

  return (
    <aside
      className={cn(
        'h-screen flex flex-col border-r border-white/10 bg-gradient-to-b from-[#0b1222] via-[#0b162d] to-[#0a1d2f] text-white transition-all duration-300 ease-in-out relative',
        collapsed ? 'w-[72px]' : 'w-[260px]'
      )}
    >
      {/* Logo */}
      <div className="flex items-center gap-3 px-5 h-16 border-b border-white/10">
        <img src={atenzLogo} alt="ATENZ" className="w-9 h-9 object-contain flex-shrink-0" />
        {!collapsed && (
          <div className="overflow-hidden">
            <h1 className="font-bold text-base tracking-tight">ATENZ</h1>
            <p className="text-[10px] text-cyan-200/80 uppercase tracking-widest">Platform</p>
          </div>
        )}
      </div>

      {/* Navigation */}
      <nav className="flex-1 py-4 px-3 space-y-1 overflow-y-auto">
        {navItems.map((item) => (
          <NavLink
            key={item.to}
            to={item.to}
            end={item.to === '/dashboard'}
            onClick={onMobileClose}
            className={({ isActive }) =>
              cn(
                'flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-all duration-200',
                isActive
                  ? 'bg-emerald-500/20 text-emerald-400'
                  : 'text-slate-300 hover:bg-white/5 hover:text-white'
              )
            }
          >
            <item.icon className="w-5 h-5 flex-shrink-0" />
            {!collapsed && <span>{item.label}</span>}
          </NavLink>
        ))}
      </nav>

      {/* User Section */}
      <div className="border-t border-white/10 p-3">
        <div className={cn('flex items-center', collapsed ? 'justify-center' : 'gap-3')}>
          <div className="w-9 h-9 rounded-full bg-emerald-600 flex items-center justify-center text-white font-semibold text-xs flex-shrink-0">
            {initials}
          </div>
          {!collapsed && (
            <div className="flex-1 min-w-0">
              <p className="text-sm font-medium truncate">
                {user?.displayName || user?.email}
              </p>
              <p className="text-[10px] text-slate-400 uppercase tracking-wider">
                {formatUserRole(user?.role)}
              </p>
            </div>
          )}
          {!collapsed && (
            <button
              onClick={logout}
              className="p-1.5 rounded-lg text-slate-400 hover:text-white hover:bg-white/10 transition-colors"
              title="Sair"
            >
              <LogOut className="w-4 h-4" />
            </button>
          )}
        </div>
      </div>

      {/* Toggle Button */}
      <button
        onClick={onToggle}
        className="absolute -right-3 top-20 w-6 h-6 bg-[#0d1a30] border border-white/20 rounded-full flex items-center justify-center text-slate-300 hover:text-white hover:bg-[#10223f] transition-colors z-10"
      >
        {collapsed ? <ChevronRight className="w-3 h-3" /> : <ChevronLeft className="w-3 h-3" />}
      </button>
    </aside>
  )
}

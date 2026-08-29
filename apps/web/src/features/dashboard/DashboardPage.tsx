import { useQuery } from '@tanstack/react-query'
import { useAuth } from '../../lib/auth'
import { api } from '../../lib/api'
import {
  MessageSquare,
  Users,
  Zap,
  Bot,
  TrendingUp,
  ArrowRight,
  CheckCircle2,
  Shield,
  Phone,
  QrCode,
  type LucideIcon,
} from 'lucide-react'
import { Link } from 'react-router-dom'
import type { Conversation } from '../../lib/api'

export function DashboardPage() {
  const { user, isPlatformAdmin, isTenantOwner } = useAuth()
  const planNames: Record<string, string> = {
    STAR: 'STAR', FLOW: 'FLOW', SCALA: 'SCALA', IA_BOT: 'IA + BOT', BOT: 'BOT',
  }
  const planName = user?.planCode ? planNames[user.planCode] ?? user.planCode : '—'
  const aiEnabled = user?.aiEnabled === true
  const aiPackageLimit = user?.monthlyAiResponseLimit
  const aiPackageUsed = user?.monthlyAiResponsesUsed ?? 0
  const aiPackagePercent = aiPackageLimit && aiPackageLimit > 0
    ? Math.min(100, Math.round((aiPackageUsed * 100) / aiPackageLimit))
    : null
  const renewalDate = user?.dueDate
    ? new Date(user.dueDate).toLocaleDateString('pt-BR')
    : null
  const assignedLine = user?.assignedLineNumber
    ? `${user.assignedConnectionType === 'QrCode' ? 'QR Code' : 'API oficial'} ${user.assignedLineNumber}`
    : null

  const { data: conversations } = useQuery({
    queryKey: ['conversations'],
    queryFn: () => api.conversations.list(undefined, 5),
  })

  const { data: statsData } = useQuery({
    queryKey: ['dashboard-stats'],
    queryFn: () => api.dashboard.getStats(),
  })

  const stats = [
    {
      label: 'Conversas Ativas',
      value: statsData?.activeConversations ?? 0,
      icon: MessageSquare,
      color: 'emerald',
    },
    {
      label: 'Operadores',
      value: statsData?.operatorCount ?? 0,
      icon: Users,
      color: 'blue',
    },
    {
      label: 'Mensagens Hoje',
      value: statsData?.messagesToday ?? 0,
      icon: Zap,
      color: 'amber',
    },
    {
      label: 'IA Respondendo',
      value: aiEnabled ? 'Ativo' : 'Indisponível',
      icon: Bot,
      color: aiEnabled ? 'violet' : 'slate',
    },
  ]

  const colorMap: Record<string, string> = {
    emerald: 'bg-emerald-50 text-emerald-600',
    blue: 'bg-blue-50 text-blue-600',
    amber: 'bg-amber-50 text-amber-600',
    violet: 'bg-violet-50 text-violet-600',
    slate: 'bg-slate-100 text-slate-500',
  }

  return (
    <div className="dashboard-page h-full overflow-y-auto">
      <div className="max-w-7xl mx-auto px-6 py-8">
        {/* Header */}
        <div className="mb-8 flex items-center justify-between">
          <div>
            <h1 className="text-2xl font-bold text-slate-900">
              Olá, {user?.displayName || user?.email?.split('@')[0]}
            </h1>
            <p className="text-slate-500 mt-1">
              {new Date().toLocaleDateString('pt-BR', {
                weekday: 'long',
                year: 'numeric',
                month: 'long',
                day: 'numeric',
              })}
            </p>
          </div>
          <div className="flex items-center gap-2 px-3 py-1.5 bg-slate-100 rounded-lg">
            <Shield className="w-4 h-4 text-slate-500" />
            <span className="text-sm font-medium text-slate-700">Plano: {planName}{renewalDate ? ` · Renova em ${renewalDate}` : ''}</span>
          </div>
          {assignedLine && (
            <div className="flex items-center gap-2 px-3 py-1.5 bg-emerald-50 rounded-lg">
              <MessageSquare className="w-4 h-4 text-emerald-600" />
              <span className="text-sm font-medium text-emerald-700">Linha: {assignedLine}</span>
            </div>
          )}
        </div>

        {/* Stats Grid */}
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
          {stats.map((stat) => (
            <div
              key={stat.label}
              className="bg-white rounded-xl border border-slate-200 p-5 hover:shadow-md transition-shadow"
            >
              <div className="flex items-center justify-between mb-3">
                <div className={`w-10 h-10 rounded-lg ${colorMap[stat.color]} flex items-center justify-center`}>
                  <stat.icon className="w-5 h-5" />
                </div>
                <TrendingUp className="w-4 h-4 text-slate-300" />
              </div>
              <p className="text-2xl font-bold text-slate-900">{stat.value}</p>
              <p className="text-sm text-slate-500 mt-1">{stat.label}</p>
            </div>
          ))}
        </div>

        {user?.tenantId && (
          <div className="mb-8 bg-white rounded-xl border border-slate-200 p-5">
            <div className="flex items-center justify-between mb-4">
              <div>
                <h2 className="font-semibold text-slate-900">Linhas contratadas</h2>
                <p className="text-sm text-slate-500 mt-1">Capacidade cadastrada para esta empresa</p>
              </div>
              <Phone className="w-5 h-5 text-slate-400" />
            </div>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div className="flex items-center gap-3 rounded-lg border border-emerald-400/20 bg-emerald-500/15 p-4">
                <Phone className="w-5 h-5 text-emerald-300" />
                <div>
                  <p className="text-2xl font-bold text-slate-900">{user.officialApiLineCount ?? 0}</p>
                  <p className="text-sm text-slate-600">API oficial</p>
                </div>
              </div>
              <div className="flex items-center gap-3 rounded-lg border border-blue-400/20 bg-blue-500/15 p-4">
                <QrCode className="w-5 h-5 text-blue-300" />
                <div>
                  <p className="text-2xl font-bold text-slate-900">{user.qrCodeLineCount ?? 0}</p>
                  <p className="text-sm text-slate-600">QR Code</p>
                </div>
              </div>
            </div>
          </div>
        )}

        {user?.tenantId && aiEnabled && (
          <div className={`mb-8 rounded-xl border p-5 ${aiPackagePercent !== null && aiPackagePercent >= 80 ? 'border-amber-200 bg-amber-50' : 'border-slate-200 bg-white'}`}>
            <div className="flex items-start justify-between gap-4">
              <div>
                <h2 className="font-semibold text-slate-900">Pacote de respostas da IA</h2>
                <p className="mt-1 text-sm text-slate-500">
                  {aiPackageLimit === null || aiPackageLimit === undefined
                    ? `${aiPackageUsed.toLocaleString('pt-BR')} respostas consumidas neste mês.`
                    : `${aiPackageUsed.toLocaleString('pt-BR')} de ${aiPackageLimit.toLocaleString('pt-BR')} respostas consumidas neste mês.`}
                </p>
              </div>
              {aiPackagePercent !== null && (
                <span className={`text-sm font-semibold ${aiPackagePercent >= 80 ? 'text-amber-700' : 'text-slate-700'}`}>
                  {aiPackagePercent}%
                </span>
              )}
            </div>
            {aiPackagePercent !== null && (
              <>
                <div className="mt-3 h-2 overflow-hidden rounded-full bg-slate-200">
                  <div className={`h-full rounded-full ${aiPackagePercent >= 80 ? 'bg-amber-500' : 'bg-emerald-500'}`} style={{ width: `${aiPackagePercent}%` }} />
                </div>
                <p className={`mt-2 text-xs ${aiPackagePercent >= 80 ? 'text-amber-700' : 'text-slate-500'}`}>
                  {aiPackagePercent >= 100
                    ? 'IA suspensa automaticamente. Solicite uma recarga de 500 respostas ao administrador; o atendimento humano e o BOT continuam disponíveis.'
                    : aiPackagePercent >= 80
                      ? 'Atenção: o pacote está próximo do fim. Solicite uma recarga.'
                      : `Restam ${Math.max(0, (aiPackageLimit ?? 0) - aiPackageUsed).toLocaleString('pt-BR')} respostas.`}
                </p>
              </>
            )}
          </div>
        )}

        {/* Quick Actions */}
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          {/* Recent Conversations */}
          <div className="bg-white rounded-xl border border-slate-200">
            <div className="flex items-center justify-between px-5 py-4 border-b border-slate-100">
              <h2 className="font-semibold text-slate-900">Conversas Recentes</h2>
              <Link
                to="/inbox"
                className="text-sm text-emerald-600 hover:text-emerald-700 flex items-center gap-1"
              >
                Ver todas <ArrowRight className="w-3 h-3" />
              </Link>
            </div>
            <div className="divide-y divide-slate-100">
              {conversations?.items?.slice(0, 5).map((conv: Conversation) => (
                <Link
                  key={conv.id}
                  to="/inbox"
                  className="flex items-center gap-3 px-5 py-3 hover:bg-slate-50 transition-colors"
                >
                  <div className="w-9 h-9 rounded-full bg-slate-100 flex items-center justify-center">
                    <MessageSquare className="w-4 h-4 text-slate-500" />
                  </div>
                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-medium text-slate-900 truncate">
                      {conv.contactName || conv.contactPhone}
                    </p>
                    <p className="text-xs text-slate-500 truncate">
                      {conv.lastMessage || 'Sem mensagens'}
                    </p>
                  </div>
                  <div className="text-right">
                    <span className="text-xs text-slate-400">
                      {conv.mode}
                    </span>
                    {conv.isWindowOpen && (
                      <div className="flex items-center gap-1 mt-0.5">
                        <div className="w-1.5 h-1.5 rounded-full bg-emerald-500" />
                        <span className="text-[10px] text-emerald-600">Aberta</span>
                      </div>
                    )}
                  </div>
                </Link>
              ))}
              {(!conversations?.items || conversations.items.length === 0) && (
                <div className="px-5 py-8 text-center">
                  <MessageSquare className="w-8 h-8 text-slate-300 mx-auto mb-2" />
                  <p className="text-sm text-slate-500">Nenhuma conversa ainda</p>
                </div>
              )}
            </div>
          </div>

          {/* Quick Links */}
          <div className="bg-white rounded-xl border border-slate-200">
            <div className="px-5 py-4 border-b border-slate-100">
              <h2 className="font-semibold text-slate-900">Ações Rápidas</h2>
            </div>
            <div className="p-4 space-y-2">
              {isTenantOwner && (
                <>
                  <QuickLink
                    to="/integrations/whatsapp"
                    icon={Zap}
                    title="Configurar WhatsApp"
                    description="Conecte seu número WhatsApp Business"
                  />
                  <QuickLink
                    to="/integrations/ai"
                    icon={Bot}
                    title="Configurar IA"
                    description="Configure o provedor de IA e modelo"
                  />
                  <QuickLink
                    to="/knowledge"
                    icon={CheckCircle2}
                    title="Base de Conhecimento"
                    description="Gerencie respostas e informações"
                  />
                  <QuickLink
                    to="/operators"
                    icon={Users}
                    title="Gerenciar Operadores"
                    description="Convide e gerencie sua equipe"
                  />
                </>
              )}
              {isPlatformAdmin && (
                <QuickLink
                  to="/admin/tenants"
                  icon={Users}
                  title="Gerenciar Tenants"
                  description="Administre as empresas da plataforma"
                />
              )}
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}

function QuickLink({
  to,
  icon: Icon,
  title,
  description,
}: {
  to: string
  icon: LucideIcon
  title: string
  description: string
}) {
  return (
    <Link
      to={to}
      className="flex items-center gap-4 p-3 rounded-lg hover:bg-slate-50 transition-colors group"
    >
      <div className="w-10 h-10 rounded-lg bg-emerald-50 text-emerald-600 flex items-center justify-center">
        <Icon className="w-5 h-5" />
      </div>
      <div className="flex-1">
        <p className="text-sm font-medium text-slate-900">{title}</p>
        <p className="text-xs text-slate-500">{description}</p>
      </div>
      <ArrowRight className="w-4 h-4 text-slate-300 group-hover:text-emerald-500 transition-colors" />
    </Link>
  )
}

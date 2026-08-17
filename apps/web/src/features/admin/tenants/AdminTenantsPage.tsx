import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  Building2,
  Plus,
  Search,
  CheckCircle2,
  XCircle,
  Copy,
  X,
  Loader2,
  AlertCircle,
} from 'lucide-react'
import { api, type Tenant, type CreateTenantResponse } from '../../../lib/api'

export function AdminTenantsPage() {
  const queryClient = useQueryClient()
  const [showCreateForm, setShowCreateForm] = useState(false)
  const [activationLink, setActivationLink] = useState<CreateTenantResponse | null>(null)
  const [copied, setCopied] = useState(false)
  const [search, setSearch] = useState('')
  const [suspendTarget, setSuspendTarget] = useState<Tenant | null>(null)
  const [suspendReason, setSuspendReason] = useState('')

  const { data: tenants, isLoading, error } = useQuery({
    queryKey: ['admin', 'tenants'],
    queryFn: () => api.admin.tenants.list(),
  })

  const { data: plans } = useQuery({
    queryKey: ['plans'],
    queryFn: () => api.plans.list(),
  })

  const createMutation = useMutation({
    mutationFn: (data: { name: string; ownerEmail: string; ownerDisplayName?: string; planCode: string }) =>
      api.admin.tenants.create(data),
    onSuccess: (data) => {
      setActivationLink(data)
      setShowCreateForm(false)
      queryClient.invalidateQueries({ queryKey: ['admin', 'tenants'] })
    },
  })

  const suspendMutation = useMutation({
    mutationFn: ({ tenant, reason }: { tenant: Tenant; reason: string }) =>
      api.admin.tenants.suspend(tenant.id, reason, tenant.version),
    onSuccess: () => {
      setSuspendTarget(null)
      setSuspendReason('')
      queryClient.invalidateQueries({ queryKey: ['admin', 'tenants'] })
    },
  })

  const reactivateMutation = useMutation({
    mutationFn: (tenant: Tenant) =>
      api.admin.tenants.reactivate(tenant.id, tenant.version),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'tenants'] })
    },
  })

  const updatePlanMutation = useMutation({
    mutationFn: ({ tenantId, planCode }: { tenantId: string; planCode: string }) =>
      api.admin.tenants.updatePlan(tenantId, planCode),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'tenants'] })
    },
  })

  const copyLink = () => {
    if (activationLink) {
      const fullLink = `${window.location.origin}${activationLink.activationLink}`
      navigator.clipboard.writeText(fullLink)
      setCopied(true)
      setTimeout(() => setCopied(false), 2000)
    }
  }

  const handleCreate = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    const formData = new FormData(e.currentTarget)
    createMutation.mutate({
      name: formData.get('name') as string,
      ownerEmail: formData.get('ownerEmail') as string,
      ownerDisplayName: (formData.get('ownerDisplayName') as string) || undefined,
      planCode: formData.get('planCode') as string,
    })
  }

  const handleSuspend = () => {
    if (suspendTarget && suspendReason.trim()) {
      suspendMutation.mutate({ tenant: suspendTarget, reason: suspendReason })
    }
  }

  const filteredTenants = (tenants ?? []).filter((t) =>
    t.name.toLowerCase().includes(search.toLowerCase())
  )

  const getStatusBadge = (status: string) => {
    switch (status) {
      case 'Active':
        return (
          <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-emerald-100 text-emerald-700">
            <CheckCircle2 className="w-3 h-3" /> Ativo
          </span>
        )
      case 'Suspended':
        return (
          <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-red-100 text-red-700">
            <XCircle className="w-3 h-3" /> Suspenso
          </span>
        )
      case 'Pending':
        return (
          <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-amber-100 text-amber-700">
            Pendente
          </span>
        )
      default:
        return (
          <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-slate-100 text-slate-700">
            {status}
          </span>
        )
    }
  }

  const isOverdue = (dueDate?: string) => dueDate ? new Date(dueDate) < new Date() : false

  return (
    <div className="h-full flex flex-col bg-slate-50">
      <div className="bg-white border-b border-slate-200 px-6 py-4">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-xl font-semibold text-slate-800">Empresas</h1>
            <p className="text-sm text-slate-500 mt-0.5">Gerencie as empresas da plataforma</p>
          </div>
          <button
            onClick={() => {
              createMutation.reset()
              setShowCreateForm(true)
            }}
            className="flex items-center gap-2 px-4 py-2.5 bg-emerald-500 text-white rounded-xl hover:bg-emerald-600 transition-colors shadow-sm"
          >
            <Plus className="w-4 h-4" /> Nova Empresa
          </button>
        </div>
      </div>

      <div className="flex-1 overflow-auto p-6">
        {activationLink && (
          <div className="mb-4 p-4 bg-emerald-50 border border-emerald-200 rounded-xl">
            <div className="flex items-start gap-3">
              <CheckCircle2 className="w-5 h-5 text-emerald-500 flex-shrink-0 mt-0.5" />
              <div className="flex-1">
                <p className="font-medium text-emerald-800">Tenant criado com sucesso!</p>
                <p className="text-sm text-emerald-600 mt-1">
                  {activationLink.message}
                </p>
                <p className="text-sm text-slate-600 mt-2">
                  <strong>TenantOwner:</strong> {activationLink.ownerEmail}
                </p>
                <div className="flex items-center gap-2 mt-2">
                  <code className="flex-1 p-2.5 bg-white border border-emerald-200 rounded-lg text-xs text-slate-700 break-all">
                    {window.location.origin}{activationLink.activationLink}
                  </code>
                  <button
                    onClick={copyLink}
                    className="flex items-center gap-1.5 px-3 py-2 bg-emerald-500 text-white rounded-lg hover:bg-emerald-600 transition-colors text-sm"
                  >
                    <Copy className="w-4 h-4" /> {copied ? 'Copiado!' : 'Copiar'}
                  </button>
                </div>
              </div>
              <button
                onClick={() => setActivationLink(null)}
                className="p-1 hover:bg-emerald-100 rounded-lg"
              >
                <X className="w-4 h-4 text-emerald-500" />
              </button>
            </div>
          </div>
        )}

        {error && (
          <div className="mb-4 p-4 bg-red-50 border border-red-200 rounded-xl flex items-center gap-3">
            <AlertCircle className="w-5 h-5 text-red-500 flex-shrink-0" />
            <div>
              <p className="font-medium text-red-800">Erro ao carregar tenants</p>
              <p className="text-sm text-red-600">{(error as Error).message}</p>
            </div>
          </div>
        )}

        <div className="mb-4">
          <div className="relative">
            <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 w-4 h-4 text-slate-400" />
            <input
              type="text"
              placeholder="Buscar tenants..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="w-full pl-10 pr-4 py-2.5 bg-white border border-slate-200 rounded-xl text-sm focus:ring-2 focus:ring-emerald-500 focus:border-transparent"
            />
          </div>
        </div>

        {isLoading ? (
          <div className="flex items-center justify-center py-12">
            <Loader2 className="w-6 h-6 text-emerald-500 animate-spin" />
            <span className="ml-2 text-slate-500">Carregando tenants...</span>
          </div>
        ) : (
          <div className="bg-white rounded-xl border border-slate-200 overflow-hidden shadow-sm">
            <table className="min-w-full">
              <thead>
                <tr className="bg-slate-50 border-b border-slate-200">
                  <th className="px-6 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">
                    Nome
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">
                    Gerência
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">
                    Plano
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">
                    Vencimento
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">
                    Status
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">
                    Criado em
                  </th>
                  <th className="px-6 py-3 text-right text-xs font-semibold text-slate-500 uppercase tracking-wider">
                    Ações
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {filteredTenants.length === 0 ? (
                  <tr>
                    <td colSpan={7} className="px-6 py-12 text-center text-slate-500">
                      {search ? 'Nenhum tenant encontrado com esse filtro.' : 'Nenhum tenant cadastrado.'}
                    </td>
                  </tr>
                ) : (
                  filteredTenants.map((tenant) => (
                    <tr key={tenant.id} className="hover:bg-slate-50 transition-colors">
                      <td className="px-6 py-4">
                        <div className="flex items-center gap-3">
                          <div className="w-9 h-9 rounded-lg bg-slate-100 flex items-center justify-center">
                            <Building2 className="w-4 h-4 text-slate-500" />
                          </div>
                          <span className="font-medium text-slate-800">{tenant.name}</span>
                        </div>
                      </td>
                      <td className="px-6 py-4 text-sm text-slate-500">TenantOwner</td>
                      <td className="px-6 py-4">
                        {plans?.find(p => p.id === tenant.planId)?.name || '-'}
                      </td>
                      <td className="px-6 py-4 text-sm">
                        {tenant.dueDate ? (
                          <span className={isOverdue(tenant.dueDate) ? 'font-medium text-red-600' : 'text-slate-500'}>
                            {new Date(tenant.dueDate).toLocaleDateString('pt-BR')}
                            {isOverdue(tenant.dueDate) ? ' (em atraso)' : ''}
                          </span>
                        ) : '-'}
                      </td>
                      <td className="px-6 py-4">{getStatusBadge(tenant.status)}</td>
                      <td className="px-6 py-4 text-sm text-slate-500">
                        {new Date(tenant.createdAt).toLocaleDateString('pt-BR')}
                      </td>
                      <td className="px-6 py-4 text-right">
                        <div className="flex items-center justify-end gap-2">
                          <select
                            value={plans?.find(p => p.id === tenant.planId)?.code || ''}
                            onChange={(e) => updatePlanMutation.mutate({ tenantId: tenant.id, planCode: e.target.value })}
                            className="text-xs px-2 py-1 border border-slate-200 rounded-lg"
                          >
                            {plans?.map(p => (
                              <option key={p.id} value={p.code}>{p.name}</option>
                            ))}
                          </select>
                          {tenant.status === 'Active' ? (
                            <button
                              onClick={() => {
                                setSuspendTarget(tenant)
                                setSuspendReason('')
                              }}
                              className="text-sm text-red-600 hover:text-red-700 font-medium"
                            >
                              Suspender
                            </button>
                          ) : tenant.status === 'Suspended' ? (
                            <button
                              onClick={() => reactivateMutation.mutate(tenant)}
                              disabled={reactivateMutation.isPending}
                              className="text-sm text-emerald-600 hover:text-emerald-700 font-medium disabled:opacity-50"
                            >
                              {reactivateMutation.isPending ? 'Reativando...' : 'Reativar'}
                            </button>
                          ) : null}
                        </div>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Create tenant modal */}
      {showCreateForm && (
        <div className="fixed inset-0 bg-black/50 backdrop-blur-sm flex items-center justify-center z-50">
          <div className="bg-white rounded-2xl p-6 w-full max-w-md shadow-2xl">
            <div className="flex items-center justify-between mb-6">
              <h2 className="text-lg font-semibold text-slate-800">Nova Empresa</h2>
              <button
                onClick={() => setShowCreateForm(false)}
                className="p-2 hover:bg-slate-100 rounded-lg"
              >
                <X className="w-5 h-5 text-slate-400" />
              </button>
            </div>

            {createMutation.isError && (
              <div className="mb-4 p-3 bg-red-50 border border-red-200 rounded-xl flex items-center gap-2">
                <AlertCircle className="w-4 h-4 text-red-500 flex-shrink-0" />
                <p className="text-sm text-red-700">{(createMutation.error as Error).message}</p>
              </div>
            )}

            <form onSubmit={handleCreate} className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1.5">
                  Nome da Empresa *
                </label>
                <input
                  name="name"
                  type="text"
                  required
                  className="w-full px-4 py-2.5 border border-slate-200 rounded-xl text-sm focus:ring-2 focus:ring-emerald-500 focus:border-transparent"
                  placeholder="Nome da empresa"
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1.5">
                  Email do responsável *
                </label>
                <input
                  name="ownerEmail"
                  type="email"
                  required
                  className="w-full px-4 py-2.5 border border-slate-200 rounded-xl text-sm focus:ring-2 focus:ring-emerald-500 focus:border-transparent"
                  placeholder="owner@empresa.com"
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1.5">
                  Nome do responsável
                </label>
                <input
                  name="ownerDisplayName"
                  type="text"
                  className="w-full px-4 py-2.5 border border-slate-200 rounded-xl text-sm focus:ring-2 focus:ring-emerald-500 focus:border-transparent"
                  placeholder="Nome do responsável (opcional)"
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1.5">
                  Plano *
                </label>
                <select
                  name="planCode"
                  required
                  className="w-full px-4 py-2.5 border border-slate-200 rounded-xl text-sm focus:ring-2 focus:ring-emerald-500 focus:border-transparent"
                >
                  {plans?.map((plan) => (
                    <option key={plan.id} value={plan.code}>
                      {plan.name} — {plan.description}
                    </option>
                  ))}
                </select>
                <p className="text-xs text-slate-500 mt-1">
                  BOT: todos os recursos exceto IA | IA+BOT: completo com IA
                </p>
              </div>
              <div className="flex justify-end gap-3 pt-2">
                <button
                  type="button"
                  onClick={() => setShowCreateForm(false)}
                  className="px-4 py-2.5 border border-slate-200 rounded-xl text-sm hover:bg-slate-50 transition-colors"
                >
                  Cancelar
                </button>
                <button
                  type="submit"
                  disabled={createMutation.isPending}
                  className="flex items-center gap-2 px-4 py-2.5 bg-emerald-500 text-white rounded-xl text-sm hover:bg-emerald-600 transition-colors disabled:opacity-50"
                >
                  {createMutation.isPending ? (
                    <>
                      <Loader2 className="w-4 h-4 animate-spin" /> Criando...
                    </>
                  ) : (
                    'Criar Tenant'
                  )}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Suspend tenant modal */}
      {suspendTarget && (
        <div className="fixed inset-0 bg-black/50 backdrop-blur-sm flex items-center justify-center z-50">
          <div className="bg-white rounded-2xl p-6 w-full max-w-md shadow-2xl">
            <div className="flex items-center justify-between mb-4">
              <h2 className="text-lg font-semibold text-slate-800">Suspender Tenant</h2>
              <button
                onClick={() => setSuspendTarget(null)}
                className="p-2 hover:bg-slate-100 rounded-lg"
              >
                <X className="w-5 h-5 text-slate-400" />
              </button>
            </div>

            <p className="text-sm text-slate-600 mb-4">
              Tem certeza que deseja suspender <strong>{suspendTarget.name}</strong>?
              O histórico será preservado, mas novas operações serão bloqueadas.
            </p>

            {suspendMutation.isError && (
              <div className="mb-4 p-3 bg-red-50 border border-red-200 rounded-xl flex items-center gap-2">
                <AlertCircle className="w-4 h-4 text-red-500 flex-shrink-0" />
                <p className="text-sm text-red-700">{(suspendMutation.error as Error).message}</p>
              </div>
            )}

            <div className="mb-4">
              <label className="block text-sm font-medium text-slate-700 mb-1.5">
                Motivo da suspensão *
              </label>
              <textarea
                value={suspendReason}
                onChange={(e) => setSuspendReason(e.target.value)}
                required
                rows={3}
                className="w-full px-4 py-2.5 border border-slate-200 rounded-xl text-sm focus:ring-2 focus:ring-emerald-500 focus:border-transparent resize-none"
                placeholder="Descreva o motivo da suspensão..."
              />
            </div>

            <div className="flex justify-end gap-3">
              <button
                onClick={() => setSuspendTarget(null)}
                className="px-4 py-2.5 border border-slate-200 rounded-xl text-sm hover:bg-slate-50 transition-colors"
              >
                Cancelar
              </button>
              <button
                onClick={handleSuspend}
                disabled={suspendMutation.isPending || !suspendReason.trim()}
                className="flex items-center gap-2 px-4 py-2.5 bg-red-500 text-white rounded-xl text-sm hover:bg-red-600 transition-colors disabled:opacity-50"
              >
                {suspendMutation.isPending ? (
                  <>
                    <Loader2 className="w-4 h-4 animate-spin" /> Suspender...
                  </>
                ) : (
                  'Suspender'
                )}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}

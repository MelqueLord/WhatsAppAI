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
  KeyRound,
  Pencil,
  DollarSign,
  RadioTower,
  Server,
  TriangleAlert,
  Users,
} from 'lucide-react'
import { api, type Tenant, type CreateTenantResponse } from '../../../lib/api'

export function AdminTenantsPage() {
  const queryClient = useQueryClient()
  const [showCreateForm, setShowCreateForm] = useState(false)
  const [createResult, setCreateResult] = useState<CreateTenantResponse | null>(null)
  const [copiedPassword, setCopiedPassword] = useState(false)
  const [copiedEmail, setCopiedEmail] = useState(false)
  const [resetResult, setResetResult] = useState<{ tenantName: string; email: string; temporaryPassword: string } | null>(null)
  const [search, setSearch] = useState('')
  const [suspendTarget, setSuspendTarget] = useState<Tenant | null>(null)
  const [suspendReason, setSuspendReason] = useState('')
  const [editTarget, setEditTarget] = useState<Tenant | null>(null)
  const [paymentTarget, setPaymentTarget] = useState<Tenant | null>(null)
  const [paymentDate, setPaymentDate] = useState('')
  const [createPlanCode, setCreatePlanCode] = useState('STAR')
  const [createAiLimit, setCreateAiLimit] = useState(1500)
  const [editPlanCode, setEditPlanCode] = useState('')
  const [editAiLimit, setEditAiLimit] = useState(0)
  const [quotaFilter, setQuotaFilter] = useState<'all' | 'normal' | 'warning' | 'exhausted' | 'unlimited'>('all')

  const { data: tenants, isLoading, error } = useQuery({
    queryKey: ['admin', 'tenants'],
    queryFn: () => api.admin.tenants.list(),
  })

  const { data: capacity, isLoading: isCapacityLoading, error: capacityError } = useQuery({
    queryKey: ['admin', 'capacity'],
    queryFn: () => api.admin.tenants.capacity(),
    refetchInterval: 60_000,
  })

  const { data: plans } = useQuery({
    queryKey: ['plans'],
    queryFn: () => api.plans.list(),
  })

  const createMutation = useMutation({
    mutationFn: (data: { name: string; ownerEmail: string; ownerDisplayName?: string; planCode: string; officialApiLineCount: number; qrCodeLineCount: number; operatorLimit: number; monthlyAiResponseLimit: number }) =>
      api.admin.tenants.create(data),
    onSuccess: (data) => {
      setCreateResult(data)
      setShowCreateForm(false)
      queryClient.invalidateQueries({ queryKey: ['admin', 'tenants'] })
      queryClient.invalidateQueries({ queryKey: ['admin', 'capacity'] })
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

  const paymentMutation = useMutation({
    mutationFn: ({ tenant, paidAt }: { tenant: Tenant; paidAt: string }) => api.admin.tenants.registerPayment(tenant.id, paidAt),
    onSuccess: () => {
      setPaymentTarget(null)
      queryClient.invalidateQueries({ queryKey: ['admin', 'tenants'] })
    },
  })

  const updatePlanMutation = useMutation({
    mutationFn: ({ tenant, planCode }: { tenant: Tenant; planCode: string }) =>
      api.admin.tenants.updatePlan(tenant.id, planCode, tenant.version),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'tenants'] })
    },
  })

  const resetOwnerPasswordMutation = useMutation({
    mutationFn: (tenantId: string) => api.admin.tenants.resetOwnerPassword(tenantId),
    onSuccess: (data, tenantId) => {
      const tenant = tenants?.find((item) => item.id === tenantId)
      setResetResult({
        tenantName: tenant?.name ?? 'Empresa',
        email: data.email,
        temporaryPassword: data.temporaryPassword,
      })
    },
  })

  const updateMutation = useMutation({
    mutationFn: ({ tenant, data }: { tenant: Tenant; data: { name: string; ownerEmail: string; ownerDisplayName?: string; planCode: string; officialApiLineCount: number; qrCodeLineCount: number; operatorLimit: number; monthlyAiResponseLimit: number } }) =>
      api.admin.tenants.update(tenant.id, data, tenant.version),
    onSuccess: () => {
      setEditTarget(null)
      queryClient.invalidateQueries({ queryKey: ['admin', 'tenants'] })
    },
  })

  const copyPassword = () => {
    if (createResult) {
      navigator.clipboard.writeText(createResult.temporaryPassword)
      setCopiedPassword(true)
      setTimeout(() => setCopiedPassword(false), 2000)
    }
  }

  const copyEmail = () => {
    if (createResult) {
      navigator.clipboard.writeText(createResult.ownerEmail)
      setCopiedEmail(true)
      setTimeout(() => setCopiedEmail(false), 2000)
    }
  }

  const handleCreate = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    const formData = new FormData(e.currentTarget)
    createMutation.mutate({
      name: formData.get('name') as string,
      ownerEmail: formData.get('ownerEmail') as string,
      ownerDisplayName: (formData.get('ownerDisplayName') as string) || undefined,
      planCode: createPlanCode,
      officialApiLineCount: plans?.find((plan) => plan.code === createPlanCode)?.defaultOfficialApiLineCount ?? 0,
      qrCodeLineCount: 0,
      operatorLimit: plans?.find((plan) => plan.code === createPlanCode)?.defaultOperatorLimit ?? 0,
      monthlyAiResponseLimit: createAiLimit,
    })
  }

  const handleSuspend = () => {
    if (suspendTarget && suspendReason.trim()) {
      suspendMutation.mutate({ tenant: suspendTarget, reason: suspendReason })
    }
  }

  const handleEdit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    if (!editTarget) return

    const formData = new FormData(e.currentTarget)
    updateMutation.mutate({
      tenant: editTarget,
      data: {
        name: String(formData.get('name') ?? '').trim(),
        ownerEmail: String(formData.get('ownerEmail') ?? '').trim(),
        ownerDisplayName: String(formData.get('ownerDisplayName') ?? '').trim() || undefined,
        planCode: editPlanCode,
        officialApiLineCount: plans?.find((plan) => plan.code === editPlanCode)?.defaultOfficialApiLineCount ?? editTarget.officialApiLineCount,
        qrCodeLineCount: 0,
        operatorLimit: plans?.find((plan) => plan.code === editPlanCode)?.defaultOperatorLimit ?? editTarget.operatorLimit,
        monthlyAiResponseLimit: editAiLimit,
      },
    })
  }

  const getQuotaStatus = (tenant: Tenant) => {
    if (tenant.monthlyAiResponseLimit === null || tenant.monthlyAiResponseLimit === undefined)
      return 'unlimited' as const
    if (tenant.monthlyAiResponseLimit <= 0 || (tenant.monthlyAiResponsesUsed ?? 0) >= tenant.monthlyAiResponseLimit)
      return 'exhausted' as const
    return (tenant.monthlyAiResponsesUsed ?? 0) / tenant.monthlyAiResponseLimit >= 0.8
      ? 'warning' as const
      : 'normal' as const
  }

  const filteredTenants = (tenants ?? []).filter((t) =>
    t.name.toLowerCase().includes(search.toLowerCase()) &&
    (quotaFilter === 'all' || getQuotaStatus(t) === quotaFilter)
  )
  const quotaCounts = (tenants ?? []).reduce((counts, tenant) => {
    counts[getQuotaStatus(tenant)]++
    return counts
  }, { normal: 0, warning: 0, exhausted: 0, unlimited: 0 })
  const selectablePlans = (plans ?? []).filter((plan) => plan.isSelectable)
  const selectedCreatePlan = plans?.find((plan) => plan.code === createPlanCode)
  const selectedEditPlan = plans?.find((plan) => plan.code === editPlanCode)

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
    <div className="companies-page h-full flex flex-col bg-[#070b16] text-white">
      <div className="border-b border-white/10 bg-[#0b1222] px-6 py-4">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-xl font-semibold text-white">Empresas</h1>
            <p className="text-sm text-slate-400 mt-0.5">Gerencie as empresas da plataforma</p>
          </div>
          <button
            disabled={selectablePlans.length === 0}
            onClick={() => {
              createMutation.reset()
              const defaultPlan = plans?.find((plan) => plan.code === 'STAR') ??
                plans?.find((plan) => plan.isSelectable)
              setCreatePlanCode(defaultPlan?.code ?? 'STAR')
              setCreateAiLimit(defaultPlan?.defaultMonthlyAiResponseLimit ?? 0)
              setShowCreateForm(true)
            }}
            className="flex items-center gap-2 px-4 py-2.5 bg-emerald-500 text-white rounded-xl hover:bg-emerald-600 transition-colors shadow-sm disabled:cursor-not-allowed disabled:opacity-50"
          >
            <Plus className="w-4 h-4" /> Nova Empresa
          </button>
        </div>
      </div>

      <div className="flex-1 overflow-auto p-6">
        {capacity?.migrationRequired && (
          <div className="mb-4 flex items-start gap-3 rounded-xl border border-red-400/40 bg-red-500/10 p-4 text-red-100" role="alert">
            <TriangleAlert className="mt-0.5 h-5 w-5 flex-shrink-0 text-red-400" />
            <div>
              <p className="font-semibold">Capacidade atingida: migre para outro KVM</p>
              <p className="mt-1 text-sm text-red-200">Um ou mais limites operacionais da instalação foram alcançados.</p>
            </div>
          </div>
        )}

        <section className="mb-6" aria-label="Capacidade da infraestrutura">
          <div className="mb-3 flex items-center gap-2">
            <Server className="h-4 w-4 text-blue-400" />
            <h2 className="text-sm font-semibold text-slate-200">Capacidade deste KVM</h2>
          </div>
          {capacityError ? (
            <div className="rounded-xl border border-amber-400/30 bg-amber-500/10 p-3 text-sm text-amber-200">
              Não foi possível carregar os indicadores de capacidade.
            </div>
          ) : (
            <div className="grid gap-3 md:grid-cols-3">
              {([
                { label: 'Clientes', detail: 'Não encerrados', icon: Building2, indicator: capacity?.customers },
                { label: 'Linhas', detail: 'WhatsApp ativas', icon: RadioTower, indicator: capacity?.lines },
                { label: 'Operadores', detail: 'Vínculos ativos', icon: Users, indicator: capacity?.operators },
              ]).map(({ label, detail, icon: Icon, indicator }) => {
                const migrationRequired = indicator?.status === 'MigrationRequired'
                const warning = indicator?.status === 'Warning'
                const barColor = migrationRequired ? 'bg-red-500' : warning ? 'bg-amber-400' : 'bg-emerald-500'
                return (
                  <article key={label} className={`rounded-xl border p-4 ${migrationRequired ? 'border-red-400/40 bg-red-500/10' : warning ? 'border-amber-400/30 bg-amber-500/10' : 'border-white/10 bg-[#0b1222]'}`}>
                    <div className="flex items-center justify-between">
                      <div className="flex items-center gap-2 text-sm font-medium text-slate-200">
                        <Icon className="h-4 w-4 text-slate-400" /> {label}
                      </div>
                      {migrationRequired && <span className="text-xs font-semibold text-red-300">Migrar KVM</span>}
                      {warning && <span className="text-xs font-semibold text-amber-300">Atenção</span>}
                    </div>
                    <div className="mt-3 flex items-end justify-between">
                      <p className="text-2xl font-bold text-white">
                        {isCapacityLoading || !indicator ? '—' : `${indicator.current} / ${indicator.limit}`}
                      </p>
                      <p className="text-xs text-slate-400">{detail}</p>
                    </div>
                    <div className="mt-3 h-2 overflow-hidden rounded-full bg-white/10">
                      <div
                        className={`h-full rounded-full transition-all ${barColor}`}
                        style={{ width: `${indicator?.utilizationPercentage ?? 0}%` }}
                      />
                    </div>
                  </article>
                )
              })}
            </div>
          )}
        </section>

        {createResult && (
          <div className="mb-4 p-4 bg-emerald-50 border border-emerald-200 rounded-xl">
            <div className="flex items-start gap-3">
              <CheckCircle2 className="w-5 h-5 text-emerald-500 flex-shrink-0 mt-0.5" />
              <div className="flex-1">
                <p className="font-medium text-emerald-800">Empresa criada com sucesso!</p>
                <p className="text-sm text-emerald-600 mt-1">
                  {createResult.message}
                </p>

                <div className="mt-3 space-y-2">
                  <div className="flex items-center gap-2">
                    <span className="text-xs font-medium text-slate-500 w-16">Email:</span>
                    <code className="flex-1 p-2 bg-white border border-emerald-200 rounded-lg text-xs text-slate-700">
                      {createResult.ownerEmail}
                    </code>
                    <button
                      onClick={copyEmail}
                      className="p-1.5 text-slate-400 hover:text-emerald-600 transition-colors"
                      title="Copiar email"
                    >
                      {copiedEmail ? <CheckCircle2 className="w-3.5 h-3.5 text-emerald-500" /> : <Copy className="w-3.5 h-3.5" />}
                    </button>
                  </div>

                  <div className="flex items-center gap-2">
                    <span className="text-xs font-medium text-slate-500 w-16">Senha:</span>
                    <code className="flex-1 p-2 bg-white border border-emerald-200 rounded-lg text-sm font-mono font-bold text-emerald-700">
                      {createResult.temporaryPassword}
                    </code>
                    <button
                      onClick={copyPassword}
                      className="p-1.5 text-slate-400 hover:text-emerald-600 transition-colors"
                      title="Copiar senha"
                    >
                      {copiedPassword ? <CheckCircle2 className="w-3.5 h-3.5 text-emerald-500" /> : <Copy className="w-3.5 h-3.5" />}
                    </button>
                  </div>
                </div>

                <p className="text-xs text-amber-600 mt-3 flex items-center gap-1">
                  <AlertCircle className="w-3 h-3" />
                  O proprietário será obrigado a alterar a senha no primeiro login.
                </p>
              </div>
              <button
                onClick={() => setCreateResult(null)}
                className="p-1 hover:bg-emerald-100 rounded-lg"
              >
                <X className="w-4 h-4 text-emerald-500" />
              </button>
            </div>
          </div>
        )}

        {resetResult && (
          <div className="mb-4 p-4 bg-amber-50 border border-amber-200 rounded-xl">
            <div className="flex items-start gap-3">
              <KeyRound className="w-5 h-5 text-amber-600 flex-shrink-0 mt-0.5" />
              <div className="flex-1">
                <p className="font-medium text-amber-800">Senha redefinida para {resetResult.tenantName}</p>
                <p className="text-sm text-amber-700 mt-1">Responsável: {resetResult.email}</p>
                <code className="inline-block mt-2 px-3 py-2 bg-white border border-amber-200 rounded-lg text-sm font-mono font-bold text-amber-800">
                  {resetResult.temporaryPassword}
                </code>
                <p className="text-xs text-amber-700 mt-2">A senha será exibida somente nesta tela e deverá ser alterada no próximo login.</p>
              </div>
              <button onClick={() => setResetResult(null)} className="p-1 hover:bg-amber-100 rounded-lg" title="Fechar">
                <X className="w-4 h-4 text-amber-600" />
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
          <div className="flex flex-col gap-3 md:flex-row">
            <div className="relative flex-1">
              <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 w-4 h-4 text-slate-400" />
              <input
                type="text"
                placeholder="Buscar tenants..."
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                className="w-full pl-10 pr-4 py-2.5 bg-[#0b1222] border border-white/10 rounded-xl text-sm text-white placeholder-slate-500 focus:ring-2 focus:ring-blue-500 focus:border-transparent"
              />
            </div>
            <select
              aria-label="Filtrar franquia de IA"
              value={quotaFilter}
              onChange={(e) => setQuotaFilter(e.target.value as typeof quotaFilter)}
              className="rounded-xl border border-white/10 bg-[#0b1222] px-3 py-2.5 text-sm text-white"
            >
              <option value="all">Todas as franquias</option>
              <option value="normal">Franquia normal ({quotaCounts.normal})</option>
              <option value="warning">Em atenção ({quotaCounts.warning})</option>
              <option value="exhausted">Esgotadas ({quotaCounts.exhausted})</option>
              <option value="unlimited">Sem limite ({quotaCounts.unlimited})</option>
            </select>
          </div>
        </div>

        {isLoading ? (
          <div className="flex items-center justify-center py-12">
            <Loader2 className="w-6 h-6 text-emerald-500 animate-spin" />
            <span className="ml-2 text-slate-500">Carregando tenants...</span>
          </div>
        ) : (
          <div className="overflow-x-auto rounded-xl border border-white/10 bg-[#0b1222] shadow-xl shadow-black/20">
            <table className="min-w-[1250px] w-full">
              <thead>
                <tr className="border-b border-white/10 bg-[#10223f]">
                  <th className="px-6 py-3 text-left text-xs font-semibold text-slate-300 uppercase tracking-wider">
                    Nome
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-semibold text-slate-300 uppercase tracking-wider">
                    Gerência
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-semibold text-slate-300 uppercase tracking-wider">
                    Plano
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-semibold text-slate-300 uppercase tracking-wider">
                    Linhas
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-semibold text-slate-300 uppercase tracking-wider">
                    Operadores
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-semibold text-slate-300 uppercase tracking-wider">
                    Respostas IA/mês
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-semibold text-slate-300 uppercase tracking-wider">
                    Vencimento
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-semibold text-slate-300 uppercase tracking-wider">
                    Status
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-semibold text-slate-300 uppercase tracking-wider">
                    Criado em
                  </th>
                  <th className="sticky right-0 bg-[#10223f] px-6 py-3 text-right text-xs font-semibold text-slate-300 uppercase tracking-wider">
                    Ações
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {filteredTenants.length === 0 ? (
                  <tr>
                    <td colSpan={10} className="px-6 py-12 text-center text-slate-500">
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
                      <td className="px-6 py-4 text-sm">
                        <div className="text-slate-700">{tenant.ownerDisplayName || 'TenantOwner'}</div>
                        <div className="text-xs text-slate-500">{tenant.ownerEmail || 'Responsável não encontrado'}</div>
                      </td>
                      <td className="px-6 py-4">
                        {plans?.find(p => p.id === tenant.planId)?.name || '-'}
                      </td>
                      <td className="px-6 py-4 text-sm text-slate-600">
                        <div>API: <span className="font-medium">{tenant.officialApiLineCount}</span></div>
                        <div>QR: <span className="font-medium">{tenant.qrCodeLineCount}</span></div>
                      </td>
                      <td className="px-6 py-4 text-sm text-slate-600">{tenant.operatorLimit || 'Ilimitado'}</td>
                      <td className="px-6 py-4 text-sm text-slate-600">
                        <span className="font-medium">{tenant.monthlyAiResponsesUsed ?? 0}</span>
                        {' / '}{tenant.monthlyAiResponseLimit ?? 'Ilimitado'}
                        {getQuotaStatus(tenant) === 'warning' && (
                          <span className="ml-2 rounded-full bg-amber-100 px-2 py-0.5 text-[11px] font-medium text-amber-700">Atenção</span>
                        )}
                        {getQuotaStatus(tenant) === 'exhausted' && (
                          <span className="ml-2 rounded-full bg-red-100 px-2 py-0.5 text-[11px] font-medium text-red-700">Esgotada</span>
                        )}
                      </td>
                      <td className="px-6 py-4 text-sm">
                        {tenant.dueDate ? (
                          <div>
                            <span className={isOverdue(tenant.dueDate) ? 'font-medium text-red-600' : 'text-slate-500'}>
                              {new Date(tenant.dueDate).toLocaleDateString('pt-BR')}
                              {isOverdue(tenant.dueDate) ? ' (em atraso)' : ''}
                            </span>
                            {tenant.lastPaymentAt && <div className="text-xs text-slate-400">Pago em {new Date(tenant.lastPaymentAt).toLocaleDateString('pt-BR')}</div>}
                          </div>
                        ) : '-'}
                      </td>
                      <td className="px-6 py-4">{getStatusBadge(tenant.status)}</td>
                      <td className="px-6 py-4 text-sm text-slate-500">
                        {new Date(tenant.createdAt).toLocaleDateString('pt-BR')}
                      </td>
                      <td className="sticky right-0 bg-[#0b1222] px-6 py-4 text-right">
                        <div className="flex min-w-[290px] flex-nowrap items-center justify-end gap-3">
                          <button
                            onClick={() => {
                              updateMutation.reset()
                              const currentPlan = plans?.find((plan) => plan.id === tenant.planId)
                              setEditPlanCode(currentPlan?.code ?? '')
                              setEditAiLimit(tenant.monthlyAiResponseLimit ??
                                currentPlan?.defaultMonthlyAiResponseLimit ?? 0)
                              setEditTarget(tenant)
                            }}
                            className="inline-flex shrink-0 items-center text-xs text-slate-600 hover:text-emerald-700 font-medium"
                            aria-label="Editar empresa"
                            title="Editar empresa"
                          >
                            <Pencil className="w-4 h-4" />
                          </button>
                          <select
                            value={plans?.find(p => p.id === tenant.planId)?.code || ''}
                            onChange={(e) => updatePlanMutation.mutate({ tenant, planCode: e.target.value })}
                            className="text-xs px-2 py-1 border border-slate-200 rounded-lg"
                          >
                            {plans?.filter((plan) => plan.isSelectable || plan.id === tenant.planId).map(p => (
                              <option key={p.id} value={p.code}>{p.name}</option>
                            ))}
                          </select>
                          <button
                            onClick={() => resetOwnerPasswordMutation.mutate(tenant.id)}
                            disabled={!tenant.ownerEmail || resetOwnerPasswordMutation.isPending}
                            className="inline-flex shrink-0 items-center text-xs text-amber-700 hover:text-amber-800 font-medium disabled:opacity-50"
                            aria-label="Redefinir senha"
                            title="Redefinir senha do responsável"
                          >
                            <KeyRound className="w-3.5 h-3.5" />
                          </button>
                          <button
                            onClick={() => {
                              setPaymentTarget(tenant)
                              setPaymentDate(new Date().toISOString().slice(0, 10))
                            }}
                            disabled={paymentMutation.isPending}
                            className="inline-flex shrink-0 items-center text-xs text-emerald-700 hover:text-emerald-800 font-medium disabled:opacity-50"
                            aria-label="Registrar pagamento"
                            title="Registrar pagamento manual"
                          >
                            <DollarSign className="w-4 h-4" />
                          </button>
                          {tenant.status === 'Active' ? (
                            <button
                              onClick={() => {
                                setSuspendTarget(tenant)
                                setSuspendReason('')
                              }}
                              className="inline-flex shrink-0 items-center text-sm text-red-600 hover:text-red-700 font-medium"
                              aria-label="Suspender empresa"
                              title="Suspender empresa"
                            >
                              <XCircle className="w-4 h-4" />
                            </button>
                          ) : tenant.status === 'Suspended' ? (
                            <button
                              onClick={() => reactivateMutation.mutate(tenant)}
                              disabled={reactivateMutation.isPending}
                              className="inline-flex shrink-0 items-center text-sm text-emerald-600 hover:text-emerald-700 font-medium disabled:opacity-50"
                              aria-label="Reativar empresa"
                              title="Reativar empresa"
                            >
                              <CheckCircle2 className="w-4 h-4" />
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
      {paymentTarget && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
          <div className="w-full max-w-sm rounded-lg bg-white p-6 shadow-xl">
            <h2 className="text-lg font-semibold text-slate-800">Registrar pagamento</h2>
            <p className="mt-1 text-sm text-slate-500">{paymentTarget.name}</p>
            <label className="mt-5 block text-sm font-medium text-slate-700">Data do pagamento</label>
            <input type="date" value={paymentDate} onChange={(event) => setPaymentDate(event.target.value)} max={new Date().toISOString().slice(0, 10)} className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2" />
            <p className="mt-2 text-xs text-slate-500">O próximo vencimento será 30 dias após esta data.</p>
            <div className="mt-6 flex justify-end gap-3"><button onClick={() => setPaymentTarget(null)} className="text-sm text-slate-600">Cancelar</button><button onClick={() => paymentMutation.mutate({ tenant: paymentTarget, paidAt: `${paymentDate}T12:00:00Z` })} disabled={paymentMutation.isPending || !paymentDate} className="rounded-lg bg-emerald-500 px-4 py-2 text-sm font-medium text-white disabled:opacity-50">{paymentMutation.isPending ? 'Registrando...' : 'Confirmar pagamento'}</button></div>
          </div>
        </div>
      )}

      {showCreateForm && (
        <div className="fixed inset-0 bg-black/50 backdrop-blur-sm flex items-center justify-center z-50">
          <div className="bg-white text-slate-800 rounded-2xl p-6 w-full max-w-md shadow-2xl">
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
                <label htmlFor="create-plan" className="block text-sm font-medium text-slate-700 mb-1.5">
                  Plano *
                </label>
                <select
                  id="create-plan"
                  name="planCode"
                  required
                  value={createPlanCode}
                  onChange={(event) => {
                    const plan = plans?.find((item) => item.code === event.target.value)
                    setCreatePlanCode(event.target.value)
                    setCreateAiLimit(plan?.defaultMonthlyAiResponseLimit ?? 0)
                  }}
                  className="w-full px-4 py-2.5 border border-slate-200 rounded-xl text-sm focus:ring-2 focus:ring-emerald-500 focus:border-transparent"
                >
                  {selectablePlans.map((plan) => (
                    <option key={plan.id} value={plan.code}>
                      {plan.name} — {plan.description}
                    </option>
                  ))}
                </select>
                <p className="text-xs text-slate-500 mt-1">
                  Os recursos e limites operacionais são aplicados automaticamente.
                </p>
              </div>
              <div className="grid grid-cols-2 gap-3 rounded-xl bg-slate-50 p-3 text-sm text-slate-600">
                <div><span className="block text-xs text-slate-400">Linhas oficiais</span><strong>{selectedCreatePlan?.defaultOfficialApiLineCount ?? 0}</strong></div>
                <div><span className="block text-xs text-slate-400">Operadores</span><strong>{selectedCreatePlan?.defaultOperatorLimit ?? 0}</strong></div>
              </div>
              <div>
                <label htmlFor="create-ai-limit" className="block text-sm font-medium text-slate-700 mb-1.5">Respostas da IA por mês</label>
                <input
                  id="create-ai-limit"
                  type="number"
                  min="0"
                  step="1"
                  value={createAiLimit}
                  onChange={(event) => setCreateAiLimit(Math.max(0, Number(event.target.value)))}
                  required
                  className="w-full px-4 py-2.5 border border-slate-200 rounded-xl text-sm focus:ring-2 focus:ring-emerald-500 focus:border-transparent"
                />
                <p className="text-xs text-slate-500 mt-1">Personalize a franquia desta empresa. Mensagens recebidas não contam.</p>
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

      {editTarget && (
        <div className="fixed inset-0 bg-black/50 backdrop-blur-sm flex items-center justify-center z-50">
          <div className="bg-white text-slate-800 rounded-2xl p-6 w-full max-w-md shadow-2xl">
            <div className="flex items-center justify-between mb-6">
              <h2 className="text-lg font-semibold text-slate-800">Editar Empresa</h2>
              <button onClick={() => setEditTarget(null)} className="p-2 hover:bg-slate-100 rounded-lg" title="Fechar">
                <X className="w-5 h-5 text-slate-400" />
              </button>
            </div>

            {updateMutation.isError && (
              <div className="mb-4 p-3 bg-red-50 border border-red-200 rounded-xl flex items-center gap-2">
                <AlertCircle className="w-4 h-4 text-red-500 flex-shrink-0" />
                <p className="text-sm text-red-700">{(updateMutation.error as Error).message}</p>
              </div>
            )}

            <form key={editTarget.id} onSubmit={handleEdit} className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1.5">Nome da Empresa *</label>
                <input
                  name="name"
                  type="text"
                  required
                  defaultValue={editTarget.name}
                  className="w-full px-4 py-2.5 border border-slate-200 rounded-xl text-sm focus:ring-2 focus:ring-emerald-500 focus:border-transparent"
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1.5">Email do responsável *</label>
                <input
                  name="ownerEmail"
                  type="email"
                  required
                  defaultValue={editTarget.ownerEmail ?? ''}
                  className="w-full px-4 py-2.5 border border-slate-200 rounded-xl text-sm focus:ring-2 focus:ring-emerald-500 focus:border-transparent"
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1.5">Nome do responsável</label>
                <input
                  name="ownerDisplayName"
                  type="text"
                  defaultValue={editTarget.ownerDisplayName ?? ''}
                  className="w-full px-4 py-2.5 border border-slate-200 rounded-xl text-sm focus:ring-2 focus:ring-emerald-500 focus:border-transparent"
                  placeholder="Nome do responsável (opcional)"
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1.5">Plano *</label>
                <select
                  name="planCode"
                  required
                  value={editPlanCode}
                  onChange={(event) => {
                    const plan = plans?.find((item) => item.code === event.target.value)
                    setEditPlanCode(event.target.value)
                    setEditAiLimit(plan?.defaultMonthlyAiResponseLimit ?? 0)
                  }}
                  className="w-full px-4 py-2.5 border border-slate-200 rounded-xl text-sm focus:ring-2 focus:ring-emerald-500 focus:border-transparent"
                >
                  {plans?.filter((plan) => plan.isSelectable || plan.id === editTarget.planId).map((plan) => <option key={plan.id} value={plan.code}>{plan.name} — {plan.description}</option>)}
                </select>
              </div>
              <div className="grid grid-cols-2 gap-3 rounded-xl bg-slate-50 p-3 text-sm text-slate-600">
                <div><span className="block text-xs text-slate-400">Linhas oficiais</span><strong>{selectedEditPlan?.isSelectable ? selectedEditPlan.defaultOfficialApiLineCount : editTarget.officialApiLineCount}</strong></div>
                <div><span className="block text-xs text-slate-400">Operadores</span><strong>{selectedEditPlan?.isSelectable ? selectedEditPlan.defaultOperatorLimit : editTarget.operatorLimit}</strong></div>
              </div>
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1.5">Respostas da IA por mês</label>
                <input
                  type="number"
                  min="0"
                  step="1"
                  required
                  value={editAiLimit}
                  onChange={(event) => setEditAiLimit(Math.max(0, Number(event.target.value)))}
                  className="w-full px-4 py-2.5 border border-slate-200 rounded-xl text-sm focus:ring-2 focus:ring-emerald-500 focus:border-transparent"
                />
                <p className="text-xs text-slate-500 mt-1">Consumo atual: {editTarget.monthlyAiResponsesUsed ?? 0} respostas neste mês.</p>
              </div>
              <div className="flex justify-end gap-3 pt-2">
                <button type="button" onClick={() => setEditTarget(null)} className="px-4 py-2.5 border border-slate-200 rounded-xl text-sm hover:bg-slate-50">
                  Cancelar
                </button>
                <button type="submit" disabled={updateMutation.isPending} className="flex items-center gap-2 px-4 py-2.5 bg-emerald-500 text-white rounded-xl text-sm hover:bg-emerald-600 disabled:opacity-50">
                  {updateMutation.isPending ? <><Loader2 className="w-4 h-4 animate-spin" /> Salvando...</> : 'Salvar alterações'}
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

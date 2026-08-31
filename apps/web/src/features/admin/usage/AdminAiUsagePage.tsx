import { useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { BarChart3, Building2, Loader2, Search, Wallet } from 'lucide-react'
import { api, type Tenant } from '../../../lib/api'

const money = (minorUnits: number) =>
  `R$ ${(minorUnits / 100).toLocaleString('pt-BR', { minimumFractionDigits: 2 })}`

export function AdminAiUsagePage() {
  const [search, setSearch] = useState('')
  const { data: tenants = [], isLoading, error } = useQuery({
    queryKey: ['admin', 'ai-usage-overview'],
    queryFn: () => api.admin.tenants.list(),
    refetchInterval: 30_000,
  })

  const filteredTenants = useMemo(() => {
    const term = search.trim().toLowerCase()
    return tenants.filter((tenant) => !term ||
      tenant.name.toLowerCase().includes(term) ||
      tenant.aiProvider?.toLowerCase().includes(term) ||
      tenant.aiModelId?.toLowerCase().includes(term))
  }, [search, tenants])

  const totals = tenants.reduce((summary, tenant) => ({
    responses: summary.responses + (tenant.monthlyAiResponsesUsed ?? 0),
    tokens: summary.tokens + (tenant.monthlyAiTokensUsed ?? 0),
    cost: summary.cost + (tenant.monthlyAiEstimatedCostMinorUnits ?? 0),
  }), { responses: 0, tokens: 0, cost: 0 })

  return (
    <div className="h-full overflow-y-auto bg-slate-50">
      <div className="mx-auto max-w-7xl space-y-6 px-6 py-8">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-violet-50 text-violet-600"><BarChart3 className="h-5 w-5" /></div>
            <div><h1 className="text-xl font-bold text-slate-900">Uso de IA</h1><p className="text-sm text-slate-500">Controle operacional por empresa · mês UTC atual · atualização automática</p></div>
          </div>
          <div className="relative"><Search className="absolute left-3 top-2.5 h-4 w-4 text-slate-400" /><input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Buscar empresa, provedor ou modelo" className="w-72 rounded-lg border border-slate-300 bg-white py-2 pl-9 pr-3 text-sm" /></div>
        </div>

        <section className="grid gap-4 md:grid-cols-3">
          <SummaryCard label="Mensagens IA" value={totals.responses.toLocaleString('pt-BR')} hint="Respostas geradas no mês" icon={<BarChart3 className="h-4 w-4" />} />
          <SummaryCard label="Tokens consumidos" value={totals.tokens.toLocaleString('pt-BR')} hint="Entrada + saída" icon={<Building2 className="h-4 w-4" />} />
          <SummaryCard label="Custo operacional" value={money(totals.cost)} hint="Soma dos custos registrados" icon={<Wallet className="h-4 w-4" />} />
        </section>

        {error && <div className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">Não foi possível carregar o uso das empresas.</div>}
        {isLoading ? <div className="flex justify-center py-16"><Loader2 className="h-7 w-7 animate-spin text-violet-500" /></div> : (
          <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white shadow-sm">
            <table className="min-w-[1100px] w-full text-sm">
              <thead className="border-b border-slate-200 bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500"><tr>
                <th className="px-5 py-3">Empresa</th><th className="px-5 py-3">Provedor / modelo</th><th className="px-5 py-3">Mensagens</th><th className="px-5 py-3">Tokens</th><th className="px-5 py-3">Custo</th><th className="px-5 py-3">Orçamento</th><th className="px-5 py-3">Status</th>
              </tr></thead>
              <tbody className="divide-y divide-slate-100">
                {filteredTenants.map((tenant) => <UsageRow key={tenant.id} tenant={tenant} />)}
                {filteredTenants.length === 0 && <tr><td colSpan={7} className="px-5 py-12 text-center text-slate-500">Nenhuma empresa encontrada.</td></tr>}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  )
}

function SummaryCard({ label, value, hint, icon }: { label: string; value: string; hint: string; icon: React.ReactNode }) {
  return <article className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm"><div className="flex items-center justify-between text-violet-600"><p className="text-xs uppercase tracking-wide text-slate-500">{label}</p>{icon}</div><p className="mt-2 text-2xl font-bold text-slate-900">{value}</p><p className="mt-1 text-xs text-slate-500">{hint}</p></article>
}

function UsageRow({ tenant }: { tenant: Tenant }) {
  const responseLimit = tenant.monthlyAiResponseLimit
  const responsePercent = responseLimit ? Math.min(100, ((tenant.monthlyAiResponsesUsed ?? 0) / responseLimit) * 100) : 0
  const tokenLimit = tenant.monthlyAiTokenLimit
  const costLimit = tenant.monthlyAiCostLimitMinorUnits
  const budgetExhausted = (tokenLimit !== null && tokenLimit !== undefined && (tenant.monthlyAiTokensUsed ?? 0) >= tokenLimit) ||
    (costLimit !== null && costLimit !== undefined && (tenant.monthlyAiEstimatedCostMinorUnits ?? 0) >= costLimit)
  return <tr className="hover:bg-slate-50"><td className="px-5 py-4"><div className="font-medium text-slate-800">{tenant.name}</div><div className="text-xs text-slate-500">{tenant.status}</div></td><td className="px-5 py-4"><div className="font-medium text-slate-700">{tenant.aiProvider ?? 'Não configurado'}</div><div className="text-xs text-slate-500">{tenant.aiModelId ?? '—'}</div></td><td className="px-5 py-4"><div>{(tenant.monthlyAiResponsesUsed ?? 0).toLocaleString('pt-BR')} / {responseLimit?.toLocaleString('pt-BR') ?? 'Ilimitado'}</div><div className="mt-1 h-1.5 w-32 rounded-full bg-slate-200"><div className="h-full rounded-full bg-violet-500" style={{ width: `${responsePercent}%` }} /></div></td><td className="px-5 py-4"><div>{(tenant.monthlyAiTokensUsed ?? 0).toLocaleString('pt-BR')}</div><div className="text-xs text-slate-500">/ {tokenLimit?.toLocaleString('pt-BR') ?? 'Ilimitado'}</div></td><td className="px-5 py-4"><div>{money(tenant.monthlyAiEstimatedCostMinorUnits ?? 0)}</div><div className="text-xs text-slate-500">/ {costLimit === null || costLimit === undefined ? 'Ilimitado' : money(costLimit)}</div></td><td className="px-5 py-4 text-xs text-slate-600">Tokens: {tokenLimit === null || tokenLimit === undefined ? 'Ilimitado' : Math.max(0, tokenLimit - (tenant.monthlyAiTokensUsed ?? 0)).toLocaleString('pt-BR')}<br />Margem: {costLimit === null || costLimit === undefined ? 'Ilimitada' : money(Math.max(0, costLimit - (tenant.monthlyAiEstimatedCostMinorUnits ?? 0)))}</td><td className="px-5 py-4">{budgetExhausted || tenant.isAiSuspendedByQuota ? <span className="rounded-full bg-red-100 px-2 py-1 text-xs font-semibold text-red-700">Bloqueada</span> : <span className="rounded-full bg-emerald-100 px-2 py-1 text-xs font-semibold text-emerald-700">Operacional</span>}</td></tr>
}

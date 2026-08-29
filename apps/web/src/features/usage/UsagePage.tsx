import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { BarChart3, Loader2, Info } from 'lucide-react'
import { useAuth } from '../../lib/auth'
import { api } from '../../lib/api'

export function UsagePage() {
  const [days, setDays] = useState(30)
  const { user } = useAuth()
  const aiEnabled = user?.aiEnabled === true

  const { data, isLoading } = useQuery({
    queryKey: ['usage', days],
    queryFn: () => {
      const from = new Date(Date.now() - days * 86400000).toISOString()
      return api.usage.get(from)
    },
  })

  if (isLoading) {
    return (
      <div className="h-full flex items-center justify-center">
        <Loader2 className="w-8 h-8 text-emerald-500 animate-spin" />
      </div>
    )
  }

  const formatCost = (minorUnits: number, currency: string | null) => {
    if (!minorUnits || !currency) return '—'
    return new Intl.NumberFormat('pt-BR', {
      style: 'currency',
      currency: currency,
    }).format(minorUnits / 100)
  }

  const quota = data?.aiResponseQuota
  const quotaPercent = quota?.utilizationPercentage ?? 0
  const quotaWarning = quota?.status === 'warning'
  const quotaExhausted = quota?.status === 'exhausted'

  return (
    <div className="h-full overflow-y-auto">
      <div className="max-w-4xl mx-auto px-6 py-8">
        <div className="flex items-center justify-between mb-8">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-lg bg-amber-50 text-amber-600 flex items-center justify-center">
              <BarChart3 className="w-5 h-5" />
            </div>
            <div>
              <h1 className="text-xl font-bold text-slate-900">Uso</h1>
              <p className="text-sm text-slate-500">Consumo de tokens e estimativas de custo</p>
            </div>
          </div>
          <select
            value={days}
            onChange={(e) => setDays(Number(e.target.value))}
            className="px-3 py-2 border border-slate-300 rounded-lg text-sm focus:ring-2 focus:ring-emerald-500"
          >
            <option value={7}>Últimos 7 dias</option>
            <option value={30}>Últimos 30 dias</option>
            <option value={90}>Últimos 90 dias</option>
          </select>
        </div>

        {/* Disclaimer */}
        <div className="mb-6 p-4 bg-blue-50 border border-blue-200 rounded-lg flex items-start gap-3">
          <Info className="w-5 h-5 text-blue-500 flex-shrink-0 mt-0.5" />
          <p className="text-sm text-blue-700">{data?.disclaimer || 'Estimativas de uso. Não é uma fatura.'}</p>
        </div>

        {quota && (
          <div className={`mb-6 rounded-xl border p-5 ${
            quotaExhausted
              ? 'border-red-200 bg-red-50'
              : quotaWarning
                ? 'border-amber-200 bg-amber-50'
                : 'border-slate-200 bg-white'
          }`}>
            <div className="flex items-start justify-between gap-4">
              <div>
                <h2 className="text-sm font-semibold text-slate-900">Franquia de respostas da IA</h2>
                <p className="text-xs text-slate-500 mt-1">
                  {quota.limit === null
                    ? 'Sem limite mensal configurado.'
                    : `${quota.used.toLocaleString('pt-BR')} de ${quota.limit.toLocaleString('pt-BR')} respostas usadas neste mês.`}
                </p>
                {(quota.topUps ?? 0) > 0 && (
                  <p className="mt-1 text-xs text-emerald-700">
                    Limite-base: {quota.baseLimit?.toLocaleString('pt-BR') ?? 0} · Recargas do mês: {quota.topUps?.toLocaleString('pt-BR')}
                  </p>
                )}
              </div>
              {quota.limit !== null && (
                <span className={`text-sm font-semibold ${quotaExhausted ? 'text-red-700' : quotaWarning ? 'text-amber-700' : 'text-slate-700'}`}>
                  {Math.min(100, quotaPercent).toLocaleString('pt-BR')}%
                </span>
              )}
            </div>
            {quota.limit !== null && (
              <>
                <div className="mt-3 h-2 rounded-full bg-slate-200 overflow-hidden">
                  <div
                    className={`h-full rounded-full ${quotaExhausted ? 'bg-red-500' : quotaWarning ? 'bg-amber-500' : 'bg-emerald-500'}`}
                    style={{ width: `${Math.min(100, Math.max(0, quotaPercent))}%` }}
                  />
                </div>
                <p className={`text-xs mt-2 ${quotaExhausted ? 'text-red-700' : quotaWarning ? 'text-amber-700' : 'text-slate-500'}`}>
                  {quotaExhausted
                    ? 'IA suspensa automaticamente por franquia esgotada. Solicite uma recarga de 500 respostas; o atendimento humano e o BOT continuam disponíveis.'
                    : quotaWarning
                      ? `Restam ${quota.remaining?.toLocaleString('pt-BR') ?? 0} respostas.`
                      : `Restam ${quota.remaining?.toLocaleString('pt-BR') ?? 0} respostas.`}
                </p>
              </>
            )}
          </div>
        )}

        {data?.quotaAlerts && data.quotaAlerts.length > 0 && (
          <section className="mb-6 rounded-xl border border-slate-200 bg-white p-5" aria-label="Histórico da franquia de IA">
            <h2 className="text-sm font-semibold text-slate-900">Histórico recente da franquia</h2>
            <div className="mt-3 space-y-2">
              {data.quotaAlerts.map((alert) => (
                <div key={`${alert.action}-${alert.entityId ?? alert.occurredAt}`} className="flex items-center justify-between gap-3 rounded-lg border border-slate-100 px-3 py-2">
                  <span className={`text-sm font-medium ${alert.action.endsWith('Exhausted') ? 'text-red-700' : 'text-amber-700'}`}>
                    {alert.action.endsWith('Exhausted') ? 'Franquia esgotada' : 'Alerta de 80%'}
                  </span>
                  <time className="text-xs text-slate-400">{new Date(alert.occurredAt).toLocaleString('pt-BR')}</time>
                </div>
              ))}
            </div>
          </section>
        )}

        {/* Usage Table */}
        <div className="bg-white rounded-xl border border-slate-200 overflow-hidden">
          <table className="w-full">
            <thead>
              <tr className="border-b border-slate-100 bg-slate-50">
                <th className="text-left px-5 py-3 text-xs font-medium text-slate-500 uppercase tracking-wider">Provedor</th>
                <th className="text-left px-5 py-3 text-xs font-medium text-slate-500 uppercase tracking-wider">Métrica</th>
                <th className="text-right px-5 py-3 text-xs font-medium text-slate-500 uppercase tracking-wider">Quantidade</th>
                <th className="text-right px-5 py-3 text-xs font-medium text-slate-500 uppercase tracking-wider">Custo Est.</th>
                <th className="text-right px-5 py-3 text-xs font-medium text-slate-500 uppercase tracking-wider">Registros</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {data?.entries?.filter(e => aiEnabled || e.provider !== 'OpenAI').map((entry, i) => (
                <tr key={i} className="hover:bg-slate-50">
                  <td className="px-5 py-3 text-sm font-medium text-slate-900">{entry.provider}</td>
                  <td className="px-5 py-3 text-sm text-slate-600">{entry.metric}</td>
                  <td className="px-5 py-3 text-sm text-slate-900 text-right">
                    {entry.totalQuantity.toLocaleString('pt-BR')}
                    {entry.unit && <span className="text-slate-400 ml-1">{entry.unit}</span>}
                  </td>
                  <td className="px-5 py-3 text-sm text-slate-900 text-right">
                    {formatCost(entry.totalCostMinorUnits, entry.currency)}
                  </td>
                  <td className="px-5 py-3 text-sm text-slate-500 text-right">{entry.count}</td>
                </tr>
              ))}
              {(!data?.entries || data.entries.length === 0) && (
                <tr>
                  <td colSpan={5} className="px-5 py-12 text-center">
                    <BarChart3 className="w-8 h-8 text-slate-300 mx-auto mb-2" />
                    <p className="text-slate-500">Nenhum registro de uso no período</p>
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  )
}

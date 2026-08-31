import { useQuery } from '@tanstack/react-query'
import { BarChart3, Loader2 } from 'lucide-react'
import { api } from '../../lib/api'

export function UsagePage() {
  const { data, isLoading } = useQuery({
    queryKey: ['usage'],
    queryFn: () => api.usage.get(),
  })

  if (isLoading) {
    return (
      <div className="h-full flex items-center justify-center">
        <Loader2 className="w-8 h-8 text-emerald-500 animate-spin" />
      </div>
    )
  }

  const quota = data?.aiResponseQuota
  const quotaPercent = quota?.utilizationPercentage ?? 0
  const quotaWarning = quota?.status === 'warning'
  const quotaExhausted = quota?.status === 'exhausted'

  return (
    <div className="h-full overflow-y-auto">
      <div className="max-w-4xl mx-auto px-6 py-8">
        <div className="flex items-center gap-3 mb-8">
          <div className="w-10 h-10 rounded-lg bg-amber-50 text-amber-600 flex items-center justify-center">
            <BarChart3 className="w-5 h-5" />
          </div>
          <div>
            <h1 className="text-xl font-bold text-slate-900">Uso</h1>
            <p className="text-sm text-slate-500">Acompanhe o saldo mensal de respostas da IA.</p>
          </div>
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
                      ? 'Você atingiu 80% da franquia. Solicite uma recarga de 500 respostas para evitar a suspensão da IA.'
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
      </div>
    </div>
  )
}

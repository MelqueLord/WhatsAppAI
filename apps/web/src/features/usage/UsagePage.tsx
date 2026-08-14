import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { BarChart3, Loader2, Info } from 'lucide-react'

interface UsageSummary {
  provider: string
  metric: string
  totalQuantity: number
  totalCostMinorUnits: number
  currency: string | null
  unit: string | null
  count: number
}

interface UsageResponse {
  from: string
  to: string
  entries: UsageSummary[]
  disclaimer: string
}

export function UsagePage() {
  const [days, setDays] = useState(30)

  const { data, isLoading } = useQuery({
    queryKey: ['usage', days],
    queryFn: async () => {
      const from = new Date(Date.now() - days * 86400000).toISOString()
      const res = await fetch(`/api/usage?from=${from}`, { credentials: 'include' })
      return res.json() as Promise<UsageResponse>
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
              {data?.entries?.map((entry, i) => (
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

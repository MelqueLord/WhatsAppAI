import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { CircleDollarSign, Loader2, Plus, RefreshCw } from 'lucide-react'
import { api, type AiModelPricing, type AiProviderInfo } from '../../../lib/api'

const money = (minorUnits: number, currency: string) =>
  `${currency} ${(minorUnits / 100).toLocaleString('pt-BR', { minimumFractionDigits: 2 })}`

export function AdminAiPricingPage() {
  const queryClient = useQueryClient()
  const [provider, setProvider] = useState('')
  const [modelId, setModelId] = useState('')
  const [inputCost, setInputCost] = useState('')
  const [outputCost, setOutputCost] = useState('')
  const [currency, setCurrency] = useState('BRL')
  const [effectiveFrom, setEffectiveFrom] = useState('')

  const prices = useQuery({
    queryKey: ['admin', 'ai-pricing'],
    queryFn: () => api.admin.aiPricing.list(),
  })
  const providers = useQuery<AiProviderInfo[]>({
    queryKey: ['admin', 'ai-providers'],
    queryFn: () => api.admin.tenants.aiProviders(),
  })

  const selectedProvider = providers.data?.find((item) => item.id === provider)
  const models = selectedProvider?.models ?? []

  const save = useMutation({
    mutationFn: () => api.admin.aiPricing.create({
      provider,
      modelId,
      inputCostPer1KMinorUnits: Number(inputCost),
      outputCostPer1KMinorUnits: Number(outputCost),
      currency: currency.trim().toUpperCase(),
      ...(effectiveFrom ? { effectiveFrom: new Date(effectiveFrom).toISOString() } : {}),
    }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'ai-pricing'] })
      setInputCost('')
      setOutputCost('')
      setEffectiveFrom('')
    },
  })

  const groupedPrices = useMemo(() => {
    const grouped = new Map<string, AiModelPricing[]>()
    for (const price of prices.data ?? []) {
      const key = `${price.provider}:${price.modelId}`
      grouped.set(key, [...(grouped.get(key) ?? []), price])
    }
    return [...grouped.values()]
  }, [prices.data])

  const canSave = provider && modelId && currency.trim().length === 3 &&
    inputCost !== '' && outputCost !== '' && Number(inputCost) >= 0 && Number(outputCost) >= 0

  return (
    <div className="h-full overflow-y-auto bg-slate-50">
      <div className="mx-auto max-w-7xl space-y-6 px-6 py-8">
        <header className="flex items-start gap-3">
          <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-emerald-50 text-emerald-600"><CircleDollarSign className="h-5 w-5" /></div>
          <div><h1 className="text-xl font-bold text-slate-900">Preços de IA</h1><p className="text-sm text-slate-500">Cadastre o custo operacional por provedor e modelo para calcular o gasto de cada empresa.</p></div>
        </header>

        <section className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
          <div className="flex items-center gap-2"><Plus className="h-4 w-4 text-emerald-600" /><h2 className="text-sm font-semibold text-slate-900">Cadastrar nova versão de preço</h2></div>
          <p className="mt-1 text-xs text-slate-500">Informe os valores em centavos por 1.000 tokens. Uma nova versão não altera o histórico já registrado.</p>
          <div className="mt-4 grid gap-4 md:grid-cols-2 lg:grid-cols-3">
            <label className="text-sm font-medium text-slate-700">Provedor<select value={provider} onChange={(event) => { setProvider(event.target.value); setModelId('') }} className="mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 font-normal"><option value="">Selecione</option>{(providers.data ?? []).map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
            <label className="text-sm font-medium text-slate-700">Modelo<select value={modelId} onChange={(event) => setModelId(event.target.value)} disabled={!provider} className="mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 font-normal"><option value="">Selecione</option>{models.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
            <label className="text-sm font-medium text-slate-700">Moeda<input value={currency} onChange={(event) => setCurrency(event.target.value.toUpperCase())} maxLength={3} className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 font-normal" /></label>
            <label className="text-sm font-medium text-slate-700">Entrada (centavos / 1k tokens)<input type="number" min="0" step="0.01" value={inputCost} onChange={(event) => setInputCost(event.target.value)} className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 font-normal" /></label>
            <label className="text-sm font-medium text-slate-700">Saída (centavos / 1k tokens)<input type="number" min="0" step="0.01" value={outputCost} onChange={(event) => setOutputCost(event.target.value)} className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 font-normal" /></label>
            <label className="text-sm font-medium text-slate-700">Vigência (opcional)<input type="datetime-local" value={effectiveFrom} onChange={(event) => setEffectiveFrom(event.target.value)} className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 font-normal" /></label>
          </div>
          {save.error && <p className="mt-3 text-sm text-red-600">{save.error.message}</p>}
          {save.isSuccess && <p className="mt-3 text-sm text-emerald-700">Preço cadastrado com sucesso.</p>}
          <button onClick={() => save.mutate()} disabled={!canSave || save.isPending} className="mt-4 inline-flex items-center gap-2 rounded-lg bg-emerald-600 px-4 py-2 text-sm font-medium text-white disabled:cursor-not-allowed disabled:opacity-50">{save.isPending && <Loader2 className="h-4 w-4 animate-spin" />}Salvar preço</button>
        </section>

        <section className="rounded-xl border border-slate-200 bg-white shadow-sm">
          <div className="flex items-center justify-between border-b border-slate-200 px-5 py-4"><div><h2 className="text-sm font-semibold text-slate-900">Preços cadastrados</h2><p className="mt-1 text-xs text-slate-500">As versões são preservadas para auditoria e cálculo histórico.</p></div><RefreshCw className="h-4 w-4 text-slate-400" /></div>
          {prices.isLoading ? <div className="flex justify-center py-12"><Loader2 className="h-6 w-6 animate-spin text-emerald-500" /></div> : prices.error ? <p className="p-5 text-sm text-red-600">Não foi possível carregar os preços.</p> : groupedPrices.length === 0 ? <p className="p-5 text-sm text-slate-500">Nenhum preço cadastrado.</p> : <div className="overflow-x-auto"><table className="min-w-[850px] w-full text-sm"><thead className="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500"><tr><th className="px-5 py-3">Provedor / modelo</th><th className="px-5 py-3">Entrada</th><th className="px-5 py-3">Saída</th><th className="px-5 py-3">Versão</th><th className="px-5 py-3">Vigência</th><th className="px-5 py-3">Status</th></tr></thead><tbody className="divide-y divide-slate-100">{groupedPrices.map((versions) => versions.map((price) => <tr key={price.id}><td className="px-5 py-4"><div className="font-medium text-slate-800">{price.provider}</div><div className="text-xs text-slate-500">{price.modelId}</div></td><td className="px-5 py-4">{money(price.inputCostPer1KMinorUnits, price.currency)} <span className="text-xs text-slate-400">/ 1k</span></td><td className="px-5 py-4">{money(price.outputCostPer1KMinorUnits, price.currency)} <span className="text-xs text-slate-400">/ 1k</span></td><td className="px-5 py-4">v{price.version}</td><td className="px-5 py-4 text-xs text-slate-600">{new Date(price.effectiveFrom).toLocaleString('pt-BR')}</td><td className="px-5 py-4">{price.effectiveTo ? <span className="rounded-full bg-slate-100 px-2 py-1 text-xs text-slate-600">Encerrado</span> : <span className="rounded-full bg-emerald-100 px-2 py-1 text-xs font-semibold text-emerald-700">Vigente</span>}</td></tr>))}</tbody></table></div>}
        </section>
      </div>
    </div>
  )
}

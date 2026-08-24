import { fetchWithCsrf } from '../../lib/api'
import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Bot, CheckCircle2, Loader2, Plus, Save, Trash2, Power } from 'lucide-react'

interface FlowStep {
  id: string
  title: string
  keywords: string
  response: string
}

interface BotConfig {
  configured: boolean
  mode: string
  welcomeMessage: string | null
  returningMessage?: string | null
  offlineMessage?: string | null
  fallbackMessage: string | null
  mediaMessage?: string | null
  handoffMessage?: string | null
  queueTransferMessage?: string | null
  enabled: boolean
  flowSteps?: FlowStep[]
}

const newStep = (): FlowStep => ({ id: `step-${Date.now()}`, title: '', keywords: '', response: '' })

export function BotConfigPage() {
  const queryClient = useQueryClient()
  const [welcomeMessage, setWelcomeMessage] = useState<string | undefined>(undefined)
  const [returningMessage, setReturningMessage] = useState<string | undefined>(undefined)
  const [offlineMessage, setOfflineMessage] = useState<string | undefined>(undefined)
  const [fallbackMessage, setFallbackMessage] = useState<string | undefined>(undefined)
  const [mediaMessage, setMediaMessage] = useState<string | undefined>(undefined)
  const [handoffMessage, setHandoffMessage] = useState<string | undefined>(undefined)
  const [queueTransferMessage, setQueueTransferMessage] = useState<string | undefined>(undefined)
  const [flowSteps, setFlowSteps] = useState<FlowStep[] | undefined>(undefined)
  const [success, setSuccess] = useState(false)

  const { data: config, isLoading } = useQuery({
    queryKey: ['bot-config'],
    queryFn: async () => {
      const res = await fetchWithCsrf('/api/bot-config')
      return res.json() as Promise<BotConfig>
    },
  })

  const toggleMutation = useMutation({
    mutationFn: async (enabled: boolean) => {
      const res = await fetchWithCsrf('/api/bot-config/toggle', {
        method: 'POST', headers: { 'Content-Type': 'application/json' }, credentials: 'include',
        body: JSON.stringify({ enabled, mode: enabled ? 'SimpleAutoReply' : undefined }),
      })
      if (!res.ok) throw new Error('Erro ao alterar status')
      return res.json()
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['bot-config'] }),
  })

  const saveMutation = useMutation({
    mutationFn: async () => {
      const res = await fetchWithCsrf('/api/bot-config', {
        method: 'POST', headers: { 'Content-Type': 'application/json' }, credentials: 'include',
        body: JSON.stringify({
          mode: 'SimpleAutoReply',
          welcomeMessage: welcomeMessage ?? config?.welcomeMessage ?? '',
          returningMessage: returningMessage ?? config?.returningMessage ?? '',
          offlineMessage: offlineMessage ?? config?.offlineMessage ?? '',
          fallbackMessage: fallbackMessage ?? config?.fallbackMessage ?? '',
          mediaMessage: mediaMessage ?? config?.mediaMessage ?? '',
          handoffMessage: handoffMessage ?? config?.handoffMessage ?? '',
          queueTransferMessage: queueTransferMessage ?? config?.queueTransferMessage ?? '',
          flowSteps: (flowSteps ?? config?.flowSteps ?? [newStep()]).filter((step) => step.title && step.response),
        }),
      })
      if (!res.ok) throw new Error('Erro ao salvar')
      return res.json()
    },
    onSuccess: () => {
      setSuccess(true)
      queryClient.invalidateQueries({ queryKey: ['bot-config'] })
      setTimeout(() => setSuccess(false), 3000)
    },
  })

  const updateStep = (id: string, patch: Partial<FlowStep>) => {
    setFlowSteps((items) => (items ?? []).map((item) => item.id === id ? { ...item, ...patch } : item))
  }

  if (isLoading) return <div className="h-full flex items-center justify-center"><Loader2 className="w-8 h-8 text-emerald-500 animate-spin" /></div>

  const effectiveFlowSteps = flowSteps ?? (config?.flowSteps?.length ? config.flowSteps : [newStep()])
  const isBotActive = config?.enabled === true && config.mode === 'SimpleAutoReply'

  return (
    <div className="h-full overflow-y-auto">
      <div className="max-w-4xl mx-auto px-6 py-8">
        <div className="flex items-center justify-between mb-8">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-lg bg-emerald-50 text-emerald-600 flex items-center justify-center"><Bot className="w-5 h-5" /></div>
            <div><h1 className="text-xl font-bold text-slate-900">Fluxo do bot</h1><p className="text-sm text-slate-500">Atendimento automático usado no WhatsApp</p></div>
          </div>
          <button onClick={() => toggleMutation.mutate(!isBotActive)} disabled={toggleMutation.isPending} className={`flex items-center gap-2 px-4 py-2 rounded-lg font-medium text-sm ${isBotActive ? 'bg-emerald-100 text-emerald-700' : 'bg-slate-100 text-slate-700'}`}>
            {toggleMutation.isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Power className="w-4 h-4" />}{isBotActive ? 'Bot ativo' : 'Bot inativo'}
          </button>
        </div>

        <div className="bg-white rounded-xl border border-slate-200 p-6 mb-6 space-y-4">
          <h2 className="font-semibold text-slate-900">Mensagens automáticas</h2>
          <textarea value={welcomeMessage ?? config?.welcomeMessage ?? ''} onChange={(e) => setWelcomeMessage(e.target.value)} rows={2} placeholder="Primeiro contato" className="w-full px-4 py-2.5 border border-slate-300 rounded-lg resize-none" />
          <textarea value={returningMessage ?? config?.returningMessage ?? ''} onChange={(e) => setReturningMessage(e.target.value)} rows={2} placeholder="Cliente que já conversou" className="w-full px-4 py-2.5 border border-slate-300 rounded-lg resize-none" />
          <textarea value={offlineMessage ?? config?.offlineMessage ?? ''} onChange={(e) => setOfflineMessage(e.target.value)} rows={2} placeholder="Quando não há atendente" className="w-full px-4 py-2.5 border border-slate-300 rounded-lg resize-none" />
          <textarea value={fallbackMessage ?? config?.fallbackMessage ?? ''} onChange={(e) => setFallbackMessage(e.target.value)} rows={2} placeholder="Fallback" className="w-full px-4 py-2.5 border border-slate-300 rounded-lg resize-none" />
          <textarea value={mediaMessage ?? config?.mediaMessage ?? ''} onChange={(e) => setMediaMessage(e.target.value)} rows={2} placeholder="Recebimento de mídia" className="w-full px-4 py-2.5 border border-slate-300 rounded-lg resize-none" />
          <textarea value={handoffMessage ?? config?.handoffMessage ?? ''} onChange={(e) => setHandoffMessage(e.target.value)} rows={2} placeholder="Transferência para atendente" className="w-full px-4 py-2.5 border border-slate-300 rounded-lg resize-none" />
          <textarea value={queueTransferMessage ?? config?.queueTransferMessage ?? ''} onChange={(e) => setQueueTransferMessage(e.target.value)} rows={2} placeholder="Transferência para fila (ex: Estou transferindo para a fila especializada. Aguarde.)" className="w-full px-4 py-2.5 border border-slate-300 rounded-lg resize-none" />
        </div>

        <div className="bg-white rounded-xl border border-slate-200 p-6 mb-6">
          <div className="flex items-center justify-between mb-4"><h2 className="font-semibold text-slate-900">Perguntas e respostas</h2><button type="button" onClick={() => setFlowSteps((items) => [...(items ?? effectiveFlowSteps), newStep()])} className="flex items-center gap-2 px-3 py-2 bg-slate-100 rounded-lg text-sm text-slate-700"><Plus className="w-4 h-4" /> Adicionar</button></div>
          <div className="space-y-4">{effectiveFlowSteps.map((step, index) => <div key={step.id} className="border border-slate-200 rounded-lg p-4"><div className="flex items-center justify-between mb-3"><span className="text-sm font-medium text-slate-700">Passo {index + 1}</span><button type="button" onClick={() => setFlowSteps((items) => (items ?? effectiveFlowSteps).filter((item) => item.id !== step.id))} className="p-2 text-slate-400 hover:text-red-600" title="Remover passo"><Trash2 className="w-4 h-4" /></button></div><input value={step.title} onChange={(e) => updateStep(step.id, { title: e.target.value })} placeholder="Título" className="w-full mb-3 px-4 py-2.5 border border-slate-300 rounded-lg" /><input value={step.keywords} onChange={(e) => updateStep(step.id, { keywords: e.target.value })} placeholder="Palavras-chave" className="w-full mb-3 px-4 py-2.5 border border-slate-300 rounded-lg" /><textarea value={step.response} onChange={(e) => updateStep(step.id, { response: e.target.value })} rows={3} placeholder="Resposta enviada ao cliente" className="w-full px-4 py-2.5 border border-slate-300 rounded-lg resize-none" /></div>)}</div>
        </div>

        <div className="flex items-center gap-3"><button onClick={() => saveMutation.mutate()} disabled={saveMutation.isPending} className="flex items-center gap-2 px-6 py-3 bg-emerald-500 hover:bg-emerald-600 text-white rounded-lg font-medium disabled:opacity-50">{saveMutation.isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />} Salvar fluxo</button>{success && <span className="flex items-center gap-1 text-emerald-600 text-sm"><CheckCircle2 className="w-4 h-4" /> Fluxo salvo com sucesso</span>}{saveMutation.isError && <span className="text-red-600 text-sm">{(saveMutation.error as Error).message || 'Erro ao salvar fluxo'}</span>}</div>
      </div>
    </div>
  )
}
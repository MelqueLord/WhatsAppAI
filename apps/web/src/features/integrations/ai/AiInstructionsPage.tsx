import { fetchWithCsrf } from '../../../lib/api'
import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { BookOpen, Loader2, Save } from 'lucide-react'

export function AiInstructionsPage() {
  const queryClient = useQueryClient()
  const [systemPrompt, setSystemPrompt] = useState('')
  const [businessType, setBusinessType] = useState('')
  const [maxTokens, setMaxTokens] = useState(300)
  const [routingQueueIds, setRoutingQueueIds] = useState<string[]>([])
  const [routingTagIds, setRoutingTagIds] = useState<string[]>([])
  const [loadedVersion, setLoadedVersion] = useState<number | null>(null)

  const { data: queues = [] } = useQuery({
    queryKey: ['service-queues'],
    queryFn: async () => {
      const response = await fetchWithCsrf('/api/service-queues')
      if (!response.ok) throw new Error('Não foi possível carregar as filas.')
      return response.json() as Promise<Array<{ id: string; name: string; description?: string; isActive: boolean }>>
    },
  })

  const { data: tags = [] } = useQuery({
    queryKey: ['client-tags'],
    queryFn: async () => {
      const response = await fetchWithCsrf('/api/client-tags')
      if (!response.ok) throw new Error('Não foi possível carregar as tags.')
      return response.json() as Promise<Array<{ id: string; name: string; description?: string; color?: string; isActive: boolean }>>
    },
  })

  const { data: config, isLoading } = useQuery({
    queryKey: ['ai-config'],
    queryFn: async () => (await fetchWithCsrf('/api/integrations/ai')).json(),
  })

  if (config && loadedVersion !== (config.version ?? 0)) {
    setLoadedVersion(config.version ?? 0)
    const storedPrompt = config.systemPrompt || ''
    const businessLine = storedPrompt.match(/^Tipo de negócio: (.+)\n\n/)
    setBusinessType(businessLine?.[1] || '')
    setSystemPrompt(businessLine ? storedPrompt.slice(businessLine[0].length) : storedPrompt)
    setMaxTokens(config.maxTokensPerResponse || 300)
    setRoutingQueueIds(config.routingQueueIds || [])
    setRoutingTagIds(config.routingTagIds || [])
  }

  const saveMutation = useMutation({
    mutationFn: async () => {
      const response = await fetchWithCsrf('/api/integrations/ai/instructions', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({ systemPrompt: `Tipo de negócio: ${businessType || 'Não informado'}\n\n${systemPrompt}`.slice(0, 4000), maxTokensPerResponse: maxTokens, routingQueueIds, routingTagIds }),
      })
      if (!response.ok) {
        const body = await response.text()
        let message = 'Não foi possível salvar as diretrizes.'
        try { message = JSON.parse(body).error || message } catch { if (body) message = body }
        throw new Error(message)
      }
      return response.json()
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['ai-config'] }),
  })

  if (isLoading) return <div className="h-full flex items-center justify-center"><Loader2 className="w-8 h-8 text-emerald-500 animate-spin" /></div>

  return (
    <div className="h-full overflow-y-auto"><div className="max-w-3xl mx-auto px-6 py-8">
      <div className="flex items-center gap-3 mb-8"><div className="w-10 h-10 rounded-lg bg-violet-50 text-violet-600 flex items-center justify-center"><BookOpen className="w-5 h-5" /></div><div><h1 className="text-xl font-bold text-slate-900">Diretrizes da IA</h1><p className="text-sm text-slate-500">Defina o que a IA precisa saber para atender com respostas curtas e objetivas.</p></div></div>
      <div className="bg-white rounded-xl border border-slate-200 p-6 space-y-5">
        <div><label className="block text-sm font-medium text-slate-700 mb-1">Tipo de negócio *</label><select value={businessType} onChange={(event) => setBusinessType(event.target.value)} className="w-full px-4 py-2.5 border border-slate-300 rounded-lg"><option value="">Selecione o segmento</option><option>Clínica e saúde</option><option>Restaurante e alimentação</option><option>Comércio e varejo</option><option>Serviços profissionais</option><option>Educação</option><option>Imobiliária</option><option>Outro</option></select><p className="text-xs text-slate-500 mt-1">A IA usará somente o contexto necessário para atender este segmento.</p></div>
        <div><label className="block text-sm font-medium text-slate-700 mb-1">Instruções de atendimento</label><textarea value={systemPrompt} onChange={(event) => setSystemPrompt(event.target.value)} rows={12} maxLength={3900} placeholder="Ex.: Você atende a Clínica X. Responda em português, seja breve, confirme informações antes de prometer prazos e encaminhe ao humano para pagamentos ou situações urgentes." className="w-full px-4 py-3 border border-slate-300 rounded-lg resize-y" /><p className="text-xs text-slate-500 mt-1">{systemPrompt.length}/3900 caracteres. Use informações essenciais; detalhes extensos devem ficar em Conhecimento.</p></div>
        <div>
          <label className="block text-sm font-medium text-slate-700 mb-2">Filas para encaminhamento automático</label>
          {queues.filter(queue => queue.isActive).length === 0 ? <p className="text-sm text-slate-500">Cadastre e ative uma fila no menu Filas.</p> : <div className="space-y-2">{queues.filter(queue => queue.isActive).map(queue => <label key={queue.id} className="flex items-start gap-3 p-3 border border-slate-200 rounded-lg cursor-pointer"><input type="checkbox" checked={routingQueueIds.includes(queue.id)} onChange={() => setRoutingQueueIds(current => current.includes(queue.id) ? current.filter(id => id !== queue.id) : [...current, queue.id])} className="mt-1" /><span><span className="block text-sm font-medium text-slate-800">{queue.name}</span>{queue.description && <span className="block text-xs text-slate-500">{queue.description}</span>}</span></label>)}</div>}
          <p className="text-xs text-slate-500 mt-2">A descrição da fila orienta a IA. Ao reconhecer a escolha do cliente, a conversa será enviada à fila e ao atendimento humano.</p>
        </div>
        <div>
          <label className="block text-sm font-medium text-slate-700 mb-2">Tags para categorização automática</label>
          {tags.filter(tag => tag.isActive).length === 0 ? <p className="text-sm text-slate-500">Cadastre e ative uma tag no menu Tags.</p> : <div className="space-y-2">{tags.filter(tag => tag.isActive).map(tag => <label key={tag.id} className="flex items-start gap-3 p-3 border border-slate-200 rounded-lg cursor-pointer"><input type="checkbox" checked={routingTagIds.includes(tag.id)} onChange={() => setRoutingTagIds(current => current.includes(tag.id) ? current.filter(id => id !== tag.id) : [...current, tag.id])} className="mt-1" /><span className="flex-1"><span className="flex items-center gap-2 text-sm font-medium text-slate-800"><span className="w-3 h-3 rounded-full" style={{ backgroundColor: tag.color || '#64748b' }} />{tag.name}</span>{tag.description && <span className="block text-xs text-slate-500 mt-1">{tag.description}</span>}</span></label>)}</div>}
          <p className="text-xs text-slate-500 mt-2">A IA adicionará somente as tags selecionadas que correspondam ao conteúdo da conversa.</p>
        </div>
        <div><label className="block text-sm font-medium text-slate-700 mb-1">Máximo de tokens por resposta</label><input type="number" value={maxTokens} onChange={(event) => setMaxTokens(Number(event.target.value))} min={50} max={1000} className="w-36 px-4 py-2.5 border border-slate-300 rounded-lg" /><p className="text-xs text-slate-500 mt-1">Respostas objetivas reduzem custo. Use 150 a 300 para atendimento comum.</p></div>
        <button onClick={() => saveMutation.mutate()} disabled={saveMutation.isPending || !config?.configured || !businessType} className="flex items-center gap-2 px-5 py-2.5 bg-emerald-500 hover:bg-emerald-600 text-white rounded-lg font-medium disabled:opacity-50">{saveMutation.isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}Salvar diretrizes</button>
        {saveMutation.isError && <p className="text-sm text-red-600">{(saveMutation.error as Error).message}</p>}{saveMutation.isSuccess && <p className="text-sm text-emerald-600">Diretrizes salvas.</p>}
      </div>
    </div></div>
  )
}

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { BotMessageSquare, CheckCircle2, Edit2, Loader2, Plus, XCircle } from 'lucide-react'
import { useState } from 'react'
import { fetchWithCsrf } from '../../../lib/api'

interface AiResponseExample {
  id: string
  customerMessage: string
  idealResponse: string
  source?: 'Manual' | 'OperatorFeedback'
  learnedFromOperator?: boolean
  isActive: boolean
  version: number
}

export function AiExamplesPage() {
  const queryClient = useQueryClient()
  const [editing, setEditing] = useState<AiResponseExample | null>(null)
  const [showForm, setShowForm] = useState(false)
  const [customerMessage, setCustomerMessage] = useState('')
  const [idealResponse, setIdealResponse] = useState('')

  const { data: examples = [], isLoading } = useQuery({
    queryKey: ['ai-response-examples'],
    queryFn: async () => {
      const response = await fetchWithCsrf('/api/ai-response-examples')
      if (!response.ok) throw new Error('Não foi possível carregar os exemplos.')
      return response.json() as Promise<AiResponseExample[]>
    },
  })

  const saveMutation = useMutation({
    mutationFn: async () => {
      const response = await fetchWithCsrf(editing ? `/api/ai-response-examples/${editing.id}` : '/api/ai-response-examples', {
        method: editing ? 'PUT' : 'POST',
        headers: {
          'Content-Type': 'application/json',
          ...(editing ? { 'If-Match': String(editing.version) } : {}),
        },
        credentials: 'include',
        body: JSON.stringify({ customerMessage, idealResponse }),
      })
      if (response.status === 409) throw new Error('O exemplo foi alterado. Recarregue e tente novamente.')
      if (!response.ok) throw new Error((await response.json().catch(() => null))?.error || 'Não foi possível salvar o exemplo.')
      return response.json()
    },
    onSuccess: () => {
      closeForm()
      queryClient.invalidateQueries({ queryKey: ['ai-response-examples'] })
    },
  })

  const statusMutation = useMutation({
    mutationFn: async (example: AiResponseExample) => {
      const action = example.isActive ? 'deactivate' : 'reactivate'
      const response = await fetchWithCsrf(`/api/ai-response-examples/${example.id}/${action}`, {
        method: 'POST',
        headers: { 'If-Match': String(example.version) },
        credentials: 'include',
      })
      if (response.status === 409) throw new Error('O exemplo foi alterado. Recarregue a página.')
      if (!response.ok) throw new Error('Não foi possível alterar o exemplo.')
      return response.json()
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['ai-response-examples'] }),
  })

  const closeForm = () => {
    setShowForm(false)
    setEditing(null)
    setCustomerMessage('')
    setIdealResponse('')
  }

  const startNew = () => {
    closeForm()
    setShowForm(true)
  }

  const startEdit = (example: AiResponseExample) => {
    setEditing(example)
    setCustomerMessage(example.customerMessage)
    setIdealResponse(example.idealResponse)
    setShowForm(true)
  }

  if (isLoading) return <div className="h-full flex items-center justify-center"><Loader2 className="w-8 h-8 text-emerald-500 animate-spin" /></div>

  const activeCount = examples.filter((example) => example.isActive).length
  return (
    <div className="h-full overflow-y-auto bg-slate-50">
      <div className="max-w-4xl mx-auto px-4 sm:px-6 py-6 sm:py-8 space-y-6">
        <div className="flex items-start justify-between gap-4">
          <div className="flex items-center gap-3"><div className="w-10 h-10 rounded-lg bg-violet-50 text-violet-600 flex items-center justify-center"><BotMessageSquare className="w-5 h-5" /></div><div><h1 className="text-xl font-bold text-slate-900">Exemplos de atendimento</h1><p className="text-sm text-slate-500">Ensine o estilo ideal de resposta para situações comuns da sua empresa.</p></div></div>
          <button onClick={startNew} className="flex items-center gap-2 px-4 py-2 bg-emerald-500 hover:bg-emerald-600 text-white rounded-lg text-sm font-medium"><Plus className="w-4 h-4" />Novo exemplo</button>
        </div>

        <div className="rounded-xl border border-violet-100 bg-violet-50 p-4 text-sm text-violet-950">
          <p><strong>{activeCount} exemplo(s) ativo(s).</strong> Correções aprovadas pelo operador entram aqui como aprendizado supervisionado desta empresa. A IA usa exemplos para copiar tom e abordagem; preços e regras continuam vindo da Base de Conhecimento.</p>
        </div>

        {showForm && <section className="bg-white rounded-xl border border-slate-200 p-6 space-y-4">
          <div><h2 className="font-semibold text-slate-900">{editing ? 'Editar exemplo' : 'Novo exemplo'}</h2><p className="text-xs text-slate-500 mt-1">Use uma pergunta comum e uma resposta curta, natural e aprovada pela empresa.</p></div>
          <label className="block text-sm font-medium text-slate-700" htmlFor="example-customer-message">Mensagem do cliente<textarea id="example-customer-message" value={customerMessage} onChange={(event) => setCustomerMessage(event.target.value)} maxLength={500} rows={3} placeholder="Ex.: Vocês atendem aos sábados?" className="mt-1 w-full px-4 py-3 border border-slate-300 rounded-lg resize-y" /></label>
          <label className="block text-sm font-medium text-slate-700" htmlFor="example-ideal-response">Resposta ideal<textarea id="example-ideal-response" value={idealResponse} onChange={(event) => setIdealResponse(event.target.value)} maxLength={500} rows={4} placeholder="Ex.: Sim! Aos sábados atendemos das 8h às 12h. Quer que eu encaminhe você para agendamento?" className="mt-1 w-full px-4 py-3 border border-slate-300 rounded-lg resize-y" /></label>
          <div className="flex gap-3"><button onClick={() => saveMutation.mutate()} disabled={saveMutation.isPending || !customerMessage.trim() || !idealResponse.trim()} className="flex items-center gap-2 px-5 py-2.5 bg-emerald-500 text-white rounded-lg disabled:opacity-50">{saveMutation.isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <CheckCircle2 className="w-4 h-4" />}{editing ? 'Atualizar' : 'Salvar exemplo'}</button><button onClick={closeForm} className="px-5 py-2.5 bg-slate-100 text-slate-700 rounded-lg">Cancelar</button></div>
          {saveMutation.isError && <p className="text-sm text-red-600">{saveMutation.error.message}</p>}
        </section>}

        <div className="space-y-3">
          {examples.map((example) => <article key={example.id} className={`bg-white rounded-xl border p-5 ${example.isActive ? 'border-slate-200' : 'border-slate-100 opacity-60'}`}><div className="flex items-start justify-between gap-4"><div className="min-w-0 space-y-2"><div className="flex items-center gap-2"><span className="text-xs font-semibold uppercase tracking-wide text-violet-600">Cliente</span>{example.learnedFromOperator && <span className="px-2 py-0.5 bg-amber-50 text-amber-700 text-xs rounded-full">Aprendido com operador</span>}{!example.isActive && <span className="px-2 py-0.5 bg-slate-100 text-slate-500 text-xs rounded-full">Inativo</span>}</div><p className="text-sm text-slate-800">{example.customerMessage}</p><p className="text-xs font-semibold uppercase tracking-wide text-emerald-600">Resposta ideal</p><p className="text-sm text-slate-600">{example.idealResponse}</p></div><div className="flex items-center gap-1"><button onClick={() => startEdit(example)} className="p-2 text-slate-400 hover:text-slate-700 hover:bg-slate-100 rounded-lg" title="Editar"><Edit2 className="w-4 h-4" /></button><button onClick={() => statusMutation.mutate(example)} className={`p-2 rounded-lg ${example.isActive ? 'text-slate-400 hover:text-red-600 hover:bg-red-50' : 'text-slate-400 hover:text-emerald-600 hover:bg-emerald-50'}`} title={example.isActive ? 'Desativar' : 'Reativar'}>{example.isActive ? <XCircle className="w-4 h-4" /> : <CheckCircle2 className="w-4 h-4" />}</button></div></div></article>)}
          {examples.length === 0 && <div className="bg-white rounded-xl border border-slate-200 p-12 text-center"><BotMessageSquare className="w-10 h-10 text-slate-300 mx-auto mb-3" /><p className="text-slate-500">Nenhum exemplo cadastrado</p><p className="text-sm text-slate-400 mt-1">Comece com as dúvidas que sua equipe mais responde.</p></div>}
        </div>
      </div>
    </div>
  )
}

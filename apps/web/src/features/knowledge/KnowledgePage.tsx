import { fetchWithCsrf } from '../../lib/api'
import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { BookOpen, Plus, Edit2, XCircle, CheckCircle2, Loader2, Sparkles } from 'lucide-react'

type KnowledgeCategory = 'General' | 'Faq' | 'Service' | 'Pricing' | 'BusinessHours' | 'Payment' | 'Location' | 'Policy'

interface KnowledgeItem {
  id: string
  title: string
  content: string
  category?: KnowledgeCategory
  priority: number
  isActive: boolean
  version: number
}

interface CategoryGuide {
  label: string
  titlePlaceholder: string
  contentPlaceholder: string
  help: string
  priority: number
}

const categoryGuides: Record<KnowledgeCategory, CategoryGuide> = {
  Faq: { label: 'Pergunta frequente', titlePlaceholder: 'Ex.: Como funciona a primeira consulta?', contentPlaceholder: 'Escreva a resposta oficial que deve ser enviada ao cliente.', help: 'Cadastre uma pergunta por item, com a resposta exata e atualizada.', priority: 80 },
  Service: { label: 'Serviço ou produto', titlePlaceholder: 'Ex.: Limpeza odontológica', contentPlaceholder: 'Explique o serviço, para quem é indicado e o que está incluído.', help: 'Descreva apenas serviços ou produtos realmente oferecidos.', priority: 70 },
  Pricing: { label: 'Preço e orçamento', titlePlaceholder: 'Ex.: Valor da avaliação inicial', contentPlaceholder: 'Informe valor, moeda, condições, validade e quando deve haver orçamento humano.', help: 'Use valores atuais e deixe claro quando o preço depende de avaliação.', priority: 100 },
  BusinessHours: { label: 'Horário de atendimento', titlePlaceholder: 'Ex.: Horários e feriados', contentPlaceholder: 'Informe dias, horários, fuso, exceções e como o cliente pode agir fora do expediente.', help: 'Mantenha feriados e horários especiais atualizados.', priority: 90 },
  Payment: { label: 'Pagamento', titlePlaceholder: 'Ex.: Formas de pagamento aceitas', contentPlaceholder: 'Informe meios aceitos, parcelamento e condições confirmadas pela empresa.', help: 'Não inclua dados bancários, chaves privadas ou instruções de cobrança manual.', priority: 90 },
  Location: { label: 'Localização e área atendida', titlePlaceholder: 'Ex.: Endereço e regiões atendidas', contentPlaceholder: 'Informe endereço público, referência, atendimento remoto ou áreas de cobertura.', help: 'Use apenas informações públicas de localização e cobertura.', priority: 70 },
  Policy: { label: 'Política da empresa', titlePlaceholder: 'Ex.: Política de troca e cancelamento', contentPlaceholder: 'Descreva a regra, prazo, condições e quando o caso deve ser encaminhado a uma pessoa.', help: 'Registre uma política por item para facilitar a revisão e evitar respostas conflitantes.', priority: 100 },
  General: { label: 'Informação geral', titlePlaceholder: 'Ex.: Como é o atendimento', contentPlaceholder: 'Cadastre uma informação oficial que não se encaixa nas outras categorias.', help: 'Prefira uma categoria específica sempre que houver uma disponível.', priority: 50 },
}

const categories = Object.entries(categoryGuides) as Array<[KnowledgeCategory, CategoryGuide]>
const isKnowledgeCategory = (value: string | undefined): value is KnowledgeCategory => value !== undefined && Object.prototype.hasOwnProperty.call(categoryGuides, value)

export function KnowledgePage() {
  const queryClient = useQueryClient()
  const [showForm, setShowForm] = useState(false)
  const [editing, setEditing] = useState<KnowledgeItem | null>(null)
  const [title, setTitle] = useState('')
  const [content, setContent] = useState('')
  const [category, setCategory] = useState<KnowledgeCategory>('Faq')
  const [priority, setPriority] = useState(categoryGuides.Faq.priority)
  const guide = categoryGuides[category]

  const { data: items, isLoading } = useQuery({
    queryKey: ['knowledge'],
    queryFn: async () => {
      const res = await fetchWithCsrf('/api/knowledge')
      return res.json() as Promise<KnowledgeItem[]>
    },
  })

  const createMutation = useMutation({
    mutationFn: async () => {
      const res = await fetchWithCsrf('/api/knowledge', {
        method: 'POST', headers: { 'Content-Type': 'application/json' }, credentials: 'include',
        body: JSON.stringify({ title, content, category, priority }),
      })
      if (!res.ok) throw new Error((await res.json().catch(() => null))?.error || 'Erro ao criar')
      return res.json()
    },
    onSuccess: () => { resetForm(); queryClient.invalidateQueries({ queryKey: ['knowledge'] }) },
  })

  const updateMutation = useMutation({
    mutationFn: async (item: KnowledgeItem) => {
      const res = await fetchWithCsrf(`/api/knowledge/${item.id}`, {
        method: 'PUT', headers: { 'Content-Type': 'application/json', 'If-Match': String(item.version) }, credentials: 'include',
        body: JSON.stringify({ title, content, category, priority }),
      })
      if (res.status === 409) throw new Error('Conflito de versão. Recarregue a página.')
      if (!res.ok) throw new Error((await res.json().catch(() => null))?.error || 'Erro ao atualizar')
      return res.json()
    },
    onSuccess: () => { resetForm(); queryClient.invalidateQueries({ queryKey: ['knowledge'] }) },
  })

  const toggleActiveMutation = useMutation({
    mutationFn: async ({ item, action }: { item: KnowledgeItem; action: 'deactivate' | 'reactivate' }) => {
      const res = await fetchWithCsrf(`/api/knowledge/${item.id}/${action}`, { method: 'POST', headers: { 'If-Match': String(item.version) }, credentials: 'include' })
      if (res.status === 409) throw new Error('Conflito de versão.')
      if (!res.ok) throw new Error('Erro')
      return res.json()
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['knowledge'] }),
  })

  const resetForm = () => {
    setShowForm(false); setEditing(null); setTitle(''); setContent(''); setCategory('Faq'); setPriority(categoryGuides.Faq.priority)
  }

  const startNew = () => { resetForm(); setShowForm(true) }

  const startEdit = (item: KnowledgeItem) => {
    const itemCategory = isKnowledgeCategory(item.category) ? item.category : 'General'
    setEditing(item); setTitle(item.title); setContent(item.content); setCategory(itemCategory); setPriority(item.priority); setShowForm(true)
  }

  const changeCategory = (nextCategory: KnowledgeCategory) => {
    setCategory(nextCategory)
    if (!editing) setPriority(categoryGuides[nextCategory].priority)
  }

  if (isLoading) return <div className="h-full flex items-center justify-center"><Loader2 className="w-8 h-8 text-emerald-500 animate-spin" /></div>

  return (
    <div className="h-full overflow-y-auto">
      <div className="max-w-4xl mx-auto px-4 sm:px-6 py-6 sm:py-8">
        <div className="flex items-center justify-between gap-3 mb-6 sm:mb-8">
          <div className="flex items-center gap-3 min-w-0">
            <div className="w-10 h-10 rounded-lg bg-blue-50 text-blue-600 flex items-center justify-center"><BookOpen className="w-5 h-5" /></div>
            <div><h1 className="text-xl font-bold text-slate-900">Base de Conhecimento</h1><p className="text-sm text-slate-500">Cadastre os fatos oficiais que orientam as respostas da IA</p></div>
          </div>
          <button onClick={startNew} className="flex items-center gap-2 px-3 sm:px-4 py-2 bg-emerald-500 hover:bg-emerald-600 text-white rounded-lg font-medium text-sm transition-colors whitespace-nowrap"><Plus className="w-4 h-4" /> <span className="hidden sm:inline">Novo item guiado</span></button>
        </div>

        <div className="mb-6 rounded-xl border border-violet-100 bg-violet-50 p-4 text-sm text-violet-950"><div className="flex gap-2"><Sparkles className="mt-0.5 w-4 h-4 shrink-0" /><p><strong>Como a IA usa esta base:</strong> registre um fato oficial por item. Preços, políticas, horários e disponibilidade só são respondidos quando houver um item relevante e ativo.</p></div></div>

        {showForm && (
          <div className="bg-white rounded-xl border border-slate-200 p-6 mb-6">
            <h2 className="font-semibold text-slate-900 mb-1">{editing ? 'Editar item guiado' : 'Novo item guiado'}</h2>
            <p className="text-sm text-slate-500 mb-5">Escolha o tipo para receber uma orientação de preenchimento adequada.</p>
            <div className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1" htmlFor="knowledge-category">Tipo de informação</label>
                <select id="knowledge-category" value={category} onChange={(event) => changeCategory(event.target.value as KnowledgeCategory)} className="w-full px-4 py-2.5 border border-slate-300 rounded-lg focus:ring-2 focus:ring-emerald-500 focus:border-transparent">
                  {categories.map(([value, itemGuide]) => <option key={value} value={value}>{itemGuide.label}</option>)}
                </select>
                <p className="mt-1 text-xs text-slate-500">{guide.help}</p>
              </div>
              <div><label className="block text-sm font-medium text-slate-700 mb-1">Assunto</label><input type="text" value={title} onChange={(event) => setTitle(event.target.value)} placeholder={guide.titlePlaceholder} maxLength={200} className="w-full px-4 py-2.5 border border-slate-300 rounded-lg focus:ring-2 focus:ring-emerald-500 focus:border-transparent" /></div>
              <div><label className="block text-sm font-medium text-slate-700 mb-1">Informação oficial</label><textarea value={content} onChange={(event) => setContent(event.target.value)} rows={5} maxLength={4000} placeholder={guide.contentPlaceholder} className="w-full px-4 py-2.5 border border-slate-300 rounded-lg focus:ring-2 focus:ring-emerald-500 focus:border-transparent resize-none" /></div>
              <div><label className="block text-sm font-medium text-slate-700 mb-1">Prioridade de busca</label><input type="number" value={priority} onChange={(event) => setPriority(Number(event.target.value))} className="w-32 px-4 py-2.5 border border-slate-300 rounded-lg focus:ring-2 focus:ring-emerald-500 focus:border-transparent" /></div>
              <div className="flex gap-3">
                <button onClick={() => editing ? updateMutation.mutate(editing) : createMutation.mutate()} disabled={createMutation.isPending || updateMutation.isPending || !title.trim() || !content.trim()} className="flex items-center gap-2 px-5 py-2.5 bg-emerald-500 hover:bg-emerald-600 text-white rounded-lg font-medium transition-colors disabled:opacity-50">{(createMutation.isPending || updateMutation.isPending) ? <Loader2 className="w-4 h-4 animate-spin" /> : <CheckCircle2 className="w-4 h-4" />}{editing ? 'Atualizar' : 'Salvar informação'}</button>
                <button onClick={resetForm} className="px-5 py-2.5 bg-slate-100 hover:bg-slate-200 text-slate-700 rounded-lg font-medium transition-colors">Cancelar</button>
              </div>
              {(createMutation.isError || updateMutation.isError) && <p className="text-sm text-red-600">{(createMutation.error || updateMutation.error)?.message}</p>}
            </div>
          </div>
        )}

        <div className="space-y-3">
          {items?.map((item) => {
            const itemCategory = isKnowledgeCategory(item.category) ? item.category : 'General'
            return <div key={item.id} className={`bg-white rounded-xl border p-5 transition-all ${item.isActive ? 'border-slate-200' : 'border-slate-100 opacity-60'}`}><div className="flex items-start justify-between"><div className="flex-1"><div className="flex flex-wrap items-center gap-2 mb-1"><h3 className="font-semibold text-slate-900">{item.title}</h3><span className="px-2 py-0.5 bg-blue-50 text-blue-700 text-xs rounded-full">{categoryGuides[itemCategory].label}</span>{!item.isActive && <span className="px-2 py-0.5 bg-slate-100 text-slate-500 text-xs rounded-full">Inativo</span>}</div><p className="text-sm text-slate-600 whitespace-pre-wrap">{item.content}</p><p className="text-xs text-slate-400 mt-2">Prioridade: {item.priority}</p></div><div className="flex items-center gap-1 ml-4"><button onClick={() => startEdit(item)} className="p-2 text-slate-400 hover:text-slate-600 hover:bg-slate-100 rounded-lg transition-colors" title="Editar"><Edit2 className="w-4 h-4" /></button><button onClick={() => toggleActiveMutation.mutate({ item, action: item.isActive ? 'deactivate' : 'reactivate' })} className={`p-2 rounded-lg transition-colors ${item.isActive ? 'text-slate-400 hover:text-red-600 hover:bg-red-50' : 'text-slate-400 hover:text-emerald-600 hover:bg-emerald-50'}`} title={item.isActive ? 'Desativar' : 'Reativar'}>{item.isActive ? <XCircle className="w-4 h-4" /> : <CheckCircle2 className="w-4 h-4" />}</button></div></div></div>
          })}
          {(!items || items.length === 0) && <div className="bg-white rounded-xl border border-slate-200 p-12 text-center"><BookOpen className="w-10 h-10 text-slate-300 mx-auto mb-3" /><p className="text-slate-500">Nenhuma informação cadastrada</p><p className="text-sm text-slate-400 mt-1">Comece por perguntas frequentes, serviços, preços ou horários.</p></div>}
        </div>
      </div>
    </div>
  )
}

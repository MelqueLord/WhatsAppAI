import { fetchWithCsrf } from '../../lib/api'
import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { BookOpen, Plus, Edit2, XCircle, CheckCircle2, Loader2 } from 'lucide-react'

interface KnowledgeItem {
  id: string
  title: string
  content: string
  priority: number
  isActive: boolean
  version: number
}

export function KnowledgePage() {
  const queryClient = useQueryClient()
  const [showForm, setShowForm] = useState(false)
  const [editing, setEditing] = useState<KnowledgeItem | null>(null)
  const [title, setTitle] = useState('')
  const [content, setContent] = useState('')
  const [priority, setPriority] = useState(0)

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
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({ title, content, priority }),
      })
      if (!res.ok) throw new Error('Erro ao criar')
      return res.json()
    },
    onSuccess: () => {
      resetForm()
      queryClient.invalidateQueries({ queryKey: ['knowledge'] })
    },
  })

  const updateMutation = useMutation({
    mutationFn: async (item: KnowledgeItem) => {
      const res = await fetchWithCsrf(`/api/knowledge/${item.id}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json', 'If-Match': String(item.version) },
        credentials: 'include',
        body: JSON.stringify({ title, content, priority }),
      })
      if (res.status === 409) throw new Error('Conflito de versão. Recarregue a página.')
      if (!res.ok) throw new Error('Erro ao atualizar')
      return res.json()
    },
    onSuccess: () => {
      resetForm()
      queryClient.invalidateQueries({ queryKey: ['knowledge'] })
    },
  })

  const toggleActiveMutation = useMutation({
    mutationFn: async ({ item, action }: { item: KnowledgeItem; action: 'deactivate' | 'reactivate' }) => {
      const res = await fetchWithCsrf(`/api/knowledge/${item.id}/${action}`, {
        method: 'POST',
        headers: { 'If-Match': String(item.version) },
        credentials: 'include',
      })
      if (res.status === 409) throw new Error('Conflito de versão.')
      if (!res.ok) throw new Error('Erro')
      return res.json()
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['knowledge'] }),
  })

  const resetForm = () => {
    setShowForm(false)
    setEditing(null)
    setTitle('')
    setContent('')
    setPriority(0)
  }

  const startEdit = (item: KnowledgeItem) => {
    setEditing(item)
    setTitle(item.title)
    setContent(item.content)
    setPriority(item.priority)
    setShowForm(true)
  }

  if (isLoading) {
    return (
      <div className="h-full flex items-center justify-center">
        <Loader2 className="w-8 h-8 text-emerald-500 animate-spin" />
      </div>
    )
  }

  return (
    <div className="h-full overflow-y-auto">
      <div className="max-w-4xl mx-auto px-4 sm:px-6 py-6 sm:py-8">
        <div className="flex items-center justify-between gap-3 mb-6 sm:mb-8">
          <div className="flex items-center gap-3 min-w-0">
            <div className="w-10 h-10 rounded-lg bg-blue-50 text-blue-600 flex items-center justify-center">
              <BookOpen className="w-5 h-5" />
            </div>
            <div>
              <h1 className="text-xl font-bold text-slate-900">Base de Conhecimento</h1>
              <p className="text-sm text-slate-500">Gerencie informações que a IA usa para responder</p>
            </div>
          </div>
          <button
            onClick={() => { resetForm(); setShowForm(true) }}
            className="flex items-center gap-2 px-3 sm:px-4 py-2 bg-emerald-500 hover:bg-emerald-600 text-white rounded-lg font-medium text-sm transition-colors whitespace-nowrap"
          >
            <Plus className="w-4 h-4" /> <span className="hidden sm:inline">Novo Item</span>
          </button>
        </div>

        {/* Form */}
        {showForm && (
          <div className="bg-white rounded-xl border border-slate-200 p-6 mb-6">
            <h3 className="font-semibold text-slate-900 mb-4">
              {editing ? 'Editar Item' : 'Novo Item'}
            </h3>
            <div className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1">Título</label>
                <input
                  type="text"
                  value={title}
                  onChange={(e) => setTitle(e.target.value)}
                  placeholder="Ex: Horário de atendimento"
                  className="w-full px-4 py-2.5 border border-slate-300 rounded-lg focus:ring-2 focus:ring-emerald-500 focus:border-transparent"
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1">Conteúdo</label>
                <textarea
                  value={content}
                  onChange={(e) => setContent(e.target.value)}
                  rows={4}
                  placeholder="Informação que a IA deve usar nas respostas..."
                  className="w-full px-4 py-2.5 border border-slate-300 rounded-lg focus:ring-2 focus:ring-emerald-500 focus:border-transparent resize-none"
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1">Prioridade</label>
                <input
                  type="number"
                  value={priority}
                  onChange={(e) => setPriority(Number(e.target.value))}
                  className="w-32 px-4 py-2.5 border border-slate-300 rounded-lg focus:ring-2 focus:ring-emerald-500 focus:border-transparent"
                />
              </div>
              <div className="flex gap-3">
                <button
                  onClick={() => editing ? updateMutation.mutate(editing) : createMutation.mutate()}
                  disabled={createMutation.isPending || updateMutation.isPending}
                  className="flex items-center gap-2 px-5 py-2.5 bg-emerald-500 hover:bg-emerald-600 text-white rounded-lg font-medium transition-colors disabled:opacity-50"
                >
                  {(createMutation.isPending || updateMutation.isPending) ? (
                    <Loader2 className="w-4 h-4 animate-spin" />
                  ) : (
                    <CheckCircle2 className="w-4 h-4" />
                  )}
                  {editing ? 'Atualizar' : 'Criar'}
                </button>
                <button
                  onClick={resetForm}
                  className="px-5 py-2.5 bg-slate-100 hover:bg-slate-200 text-slate-700 rounded-lg font-medium transition-colors"
                >
                  Cancelar
                </button>
              </div>
            </div>
          </div>
        )}

        {/* Items List */}
        <div className="space-y-3">
          {items?.map((item) => (
            <div
              key={item.id}
              className={`bg-white rounded-xl border p-5 transition-all ${
                item.isActive ? 'border-slate-200' : 'border-slate-100 opacity-60'
              }`}
            >
              <div className="flex items-start justify-between">
                <div className="flex-1">
                  <div className="flex items-center gap-2 mb-1">
                    <h3 className="font-semibold text-slate-900">{item.title}</h3>
                    {!item.isActive && (
                      <span className="px-2 py-0.5 bg-slate-100 text-slate-500 text-xs rounded-full">
                        Inativo
                      </span>
                    )}
                  </div>
                  <p className="text-sm text-slate-600 whitespace-pre-wrap">{item.content}</p>
                  <p className="text-xs text-slate-400 mt-2">Prioridade: {item.priority}</p>
                </div>
                <div className="flex items-center gap-1 ml-4">
                  <button
                    onClick={() => startEdit(item)}
                    className="p-2 text-slate-400 hover:text-slate-600 hover:bg-slate-100 rounded-lg transition-colors"
                    title="Editar"
                  >
                    <Edit2 className="w-4 h-4" />
                  </button>
                  <button
                    onClick={() =>
                      toggleActiveMutation.mutate({
                        item,
                        action: item.isActive ? 'deactivate' : 'reactivate',
                      })
                    }
                    className={`p-2 rounded-lg transition-colors ${
                      item.isActive
                        ? 'text-slate-400 hover:text-red-600 hover:bg-red-50'
                        : 'text-slate-400 hover:text-emerald-600 hover:bg-emerald-50'
                    }`}
                    title={item.isActive ? 'Desativar' : 'Reativar'}
                  >
                    {item.isActive ? <XCircle className="w-4 h-4" /> : <CheckCircle2 className="w-4 h-4" />}
                  </button>
                </div>
              </div>
            </div>
          ))}

          {(!items || items.length === 0) && (
            <div className="bg-white rounded-xl border border-slate-200 p-12 text-center">
              <BookOpen className="w-10 h-10 text-slate-300 mx-auto mb-3" />
              <p className="text-slate-500">Nenhum item de conhecimento</p>
              <p className="text-sm text-slate-400 mt-1">
                Adicione informações para que a IA use nas respostas.
              </p>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}

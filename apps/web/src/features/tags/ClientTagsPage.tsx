import { fetchWithCsrf } from '../../lib/api'
import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Tags, Plus, Edit2, XCircle, CheckCircle2, Loader2 } from 'lucide-react'

interface ClientTag {
  id: string
  name: string
  color: string | null
  description: string | null
  isActive: boolean
}

export function ClientTagsPage() {
  const queryClient = useQueryClient()
  const [showForm, setShowForm] = useState(false)
  const [editing, setEditing] = useState<ClientTag | null>(null)
  const [name, setName] = useState('')
  const [color, setColor] = useState('#10B981')
  const [description, setDescription] = useState('')

  const { data: tags, isLoading } = useQuery({
    queryKey: ['client-tags'],
    queryFn: async () => {
      const res = await fetchWithCsrf('/api/client-tags')
      return res.json() as Promise<ClientTag[]>
    },
  })

  const createMutation = useMutation({
    mutationFn: async () => {
      const res = await fetchWithCsrf('/api/client-tags', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({ name, color, description }),
      })
      if (!res.ok) throw new Error('Erro ao criar')
      return res.json()
    },
    onSuccess: () => {
      resetForm()
      queryClient.invalidateQueries({ queryKey: ['client-tags'] })
    },
  })

  const updateMutation = useMutation({
    mutationFn: async (tag: ClientTag) => {
      const res = await fetchWithCsrf(`/api/client-tags/${tag.id}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({ name, color, description }),
      })
      if (!res.ok) throw new Error('Erro ao atualizar')
      return res.json()
    },
    onSuccess: () => {
      resetForm()
      queryClient.invalidateQueries({ queryKey: ['client-tags'] })
    },
  })

  const toggleMutation = useMutation({
    mutationFn: async ({ id, action }: { id: string; action: 'deactivate' | 'reactivate' }) => {
      const res = await fetchWithCsrf(`/api/client-tags/${id}/${action}`, {
        method: 'POST',
        credentials: 'include',
      })
      if (!res.ok) throw new Error('Erro')
      return res.json()
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['client-tags'] }),
  })

  const resetForm = () => {
    setShowForm(false)
    setEditing(null)
    setName('')
    setColor('#10B981')
    setDescription('')
  }

  const startEdit = (tag: ClientTag) => {
    setEditing(tag)
    setName(tag.name)
    setColor(tag.color || '#10B981')
    setDescription(tag.description || '')
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
      <div className="max-w-4xl mx-auto px-6 py-8">
        <div className="flex items-center justify-between mb-8">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-lg bg-pink-50 text-pink-600 flex items-center justify-center">
              <Tags className="w-5 h-5" />
            </div>
            <div>
              <h1 className="text-xl font-bold text-slate-900">Tags de Clientes</h1>
              <p className="text-sm text-slate-500">Categorize seus clientes com tags personalizadas</p>
            </div>
          </div>
          <button
            onClick={() => { resetForm(); setShowForm(true) }}
            className="flex items-center gap-2 px-4 py-2 bg-emerald-500 hover:bg-emerald-600 text-white rounded-lg font-medium text-sm transition-colors"
          >
            <Plus className="w-4 h-4" /> Nova Tag
          </button>
        </div>

        {showForm && (
          <div className="bg-white rounded-xl border border-slate-200 p-6 mb-6">
            <h3 className="font-semibold text-slate-900 mb-4">{editing ? 'Editar Tag' : 'Nova Tag'}</h3>
            <div className="space-y-4">
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-medium text-slate-700 mb-1">Nome</label>
                  <input
                    type="text"
                    value={name}
                    onChange={(e) => setName(e.target.value)}
                    placeholder="Ex: VIP, B2B, Suporte"
                    className="w-full px-4 py-2.5 border border-slate-300 rounded-lg focus:ring-2 focus:ring-emerald-500 focus:border-transparent"
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-slate-700 mb-1">Cor</label>
                  <div className="flex items-center gap-2">
                    <input
                      type="color"
                      value={color}
                      onChange={(e) => setColor(e.target.value)}
                      className="w-10 h-10 rounded-lg cursor-pointer"
                    />
                    <input
                      type="text"
                      value={color}
                      onChange={(e) => setColor(e.target.value)}
                      className="flex-1 px-4 py-2.5 border border-slate-300 rounded-lg focus:ring-2 focus:ring-emerald-500 focus:border-transparent font-mono text-sm"
                    />
                  </div>
                </div>
              </div>
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1">Descrição</label>
                <input
                  type="text"
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  placeholder="Descreva o uso desta tag"
                  className="w-full px-4 py-2.5 border border-slate-300 rounded-lg focus:ring-2 focus:ring-emerald-500 focus:border-transparent"
                />
              </div>
              <div className="flex gap-3">
                <button
                  onClick={() => editing ? updateMutation.mutate(editing) : createMutation.mutate()}
                  disabled={createMutation.isPending || updateMutation.isPending || !name.trim()}
                  className="flex items-center gap-2 px-5 py-2.5 bg-emerald-500 hover:bg-emerald-600 text-white rounded-lg font-medium transition-colors disabled:opacity-50"
                >
                  {(createMutation.isPending || updateMutation.isPending) ? (
                    <Loader2 className="w-4 h-4 animate-spin" />
                  ) : (
                    <CheckCircle2 className="w-4 h-4" />
                  )}
                  {editing ? 'Atualizar' : 'Criar'}
                </button>
                <button onClick={resetForm} className="px-5 py-2.5 bg-slate-100 hover:bg-slate-200 text-slate-700 rounded-lg font-medium transition-colors">
                  Cancelar
                </button>
              </div>
            </div>
          </div>
        )}

        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
          {tags?.map((tag) => (
            <div
              key={tag.id}
              className={`bg-white rounded-xl border p-5 transition-all ${
                tag.isActive ? 'border-slate-200' : 'border-slate-100 opacity-60'
              }`}
            >
              <div className="flex items-start justify-between mb-3">
                <div className="flex items-center gap-2">
                  <div className="w-4 h-4 rounded-full" style={{ backgroundColor: tag.color || '#6B7280' }} />
                  <h3 className="font-semibold text-slate-900">{tag.name}</h3>
                </div>
                <div className="flex items-center gap-1">
                  <button onClick={() => startEdit(tag)} className="p-1.5 text-slate-400 hover:text-slate-600 hover:bg-slate-100 rounded-lg transition-colors">
                    <Edit2 className="w-3.5 h-3.5" />
                  </button>
                  <button
                    onClick={() => toggleMutation.mutate({ id: tag.id, action: tag.isActive ? 'deactivate' : 'reactivate' })}
                    className={`p-1.5 rounded-lg transition-colors ${tag.isActive ? 'text-slate-400 hover:text-red-600 hover:bg-red-50' : 'text-slate-400 hover:text-emerald-600 hover:bg-emerald-50'}`}
                  >
                    {tag.isActive ? <XCircle className="w-3.5 h-3.5" /> : <CheckCircle2 className="w-3.5 h-3.5" />}
                  </button>
                </div>
              </div>
              {tag.description && <p className="text-sm text-slate-500">{tag.description}</p>}
              {!tag.isActive && (
                <span className="inline-block mt-2 px-2 py-0.5 bg-slate-100 text-slate-500 text-xs rounded-full">Inativo</span>
              )}
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}

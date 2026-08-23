import { fetchWithCsrf } from '../../lib/api'
import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { ListOrdered, Plus, Edit2, XCircle, CheckCircle2, Loader2 } from 'lucide-react'

interface ServiceQueue {
  id: string
  name: string
  description: string | null
  color: string | null
  sortOrder: number
  isActive: boolean
}

export function QueuesPage() {
  const queryClient = useQueryClient()
  const [showForm, setShowForm] = useState(false)
  const [editing, setEditing] = useState<ServiceQueue | null>(null)
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [color, setColor] = useState('#6366F1')
  const [sortOrder, setSortOrder] = useState(0)

  const { data: queues, isLoading } = useQuery({
    queryKey: ['service-queues'],
    queryFn: async () => {
      const res = await fetchWithCsrf('/api/service-queues')
      return res.json() as Promise<ServiceQueue[]>
    },
  })

  const createMutation = useMutation({
    mutationFn: async () => {
      const res = await fetchWithCsrf('/api/service-queues', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({ name, description, color, sortOrder }),
      })
      if (!res.ok) throw new Error('Erro ao criar fila')
      return res.json()
    },
    onSuccess: () => { resetForm(); queryClient.invalidateQueries({ queryKey: ['service-queues'] }) },
  })

  const updateMutation = useMutation({
    mutationFn: async (q: ServiceQueue) => {
      const res = await fetchWithCsrf(`/api/service-queues/${q.id}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({ name, description, color, sortOrder }),
      })
      if (!res.ok) throw new Error('Erro ao atualizar fila')
      return res.json()
    },
    onSuccess: () => { resetForm(); queryClient.invalidateQueries({ queryKey: ['service-queues'] }) },
  })

  const toggleMutation = useMutation({
    mutationFn: async ({ id, action }: { id: string; action: 'deactivate' | 'reactivate' }) => {
      const res = await fetchWithCsrf(`/api/service-queues/${id}/${action}`, { method: 'POST' })
      if (!res.ok) throw new Error('Erro')
      return res.json()
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['service-queues'] }),
  })

  const resetForm = () => {
    setShowForm(false); setEditing(null)
    setName(''); setDescription(''); setColor('#6366F1'); setSortOrder(0)
  }

  const startEdit = (q: ServiceQueue) => {
    setEditing(q); setName(q.name); setDescription(q.description || '')
    setColor(q.color || '#6366F1'); setSortOrder(q.sortOrder); setShowForm(true)
  }

  return (
    <div className="p-6 max-w-4xl mx-auto">
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-slate-800 flex items-center gap-2">
            <ListOrdered className="w-6 h-6 text-indigo-500" /> Filas de Atendimento
          </h1>
          <p className="text-sm text-slate-500 mt-1">Crie filas para categorizar os atendimentos</p>
        </div>
        <button onClick={() => { resetForm(); setShowForm(true) }}
          className="flex items-center gap-2 px-4 py-2.5 bg-indigo-500 text-white rounded-xl hover:bg-indigo-600 transition-colors shadow-sm">
          <Plus className="w-4 h-4" /> Nova Fila
        </button>
      </div>

      {showForm && (
        <div className="bg-white rounded-2xl border border-slate-200 p-6 mb-6 shadow-sm">
          <h2 className="text-lg font-semibold text-slate-800 mb-4">{editing ? 'Editar Fila' : 'Nova Fila'}</h2>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <input value={name} onChange={(e) => setName(e.target.value)} placeholder="Nome da fila *"
              className="px-4 py-2.5 border border-slate-300 rounded-xl text-sm focus:ring-2 focus:ring-indigo-500 focus:border-transparent" />
            <div className="flex items-center gap-2">
              <input type="color" value={color} onChange={(e) => setColor(e.target.value)}
                className="w-10 h-10 rounded-lg cursor-pointer border-0" />
              <input type="number" value={sortOrder} onChange={(e) => setSortOrder(Number(e.target.value))} placeholder="Ordem"
                className="flex-1 px-4 py-2.5 border border-slate-300 rounded-xl text-sm focus:ring-2 focus:ring-indigo-500" />
            </div>
            <textarea value={description} onChange={(e) => setDescription(e.target.value)} placeholder="Descrição (opcional)" rows={2}
              className="md:col-span-2 px-4 py-2.5 border border-slate-300 rounded-xl text-sm resize-none focus:ring-2 focus:ring-indigo-500" />
          </div>
          <div className="flex gap-3 mt-4">
            <button onClick={() => editing ? updateMutation.mutate(editing) : createMutation.mutate()}
              disabled={!name.trim() || createMutation.isPending || updateMutation.isPending}
              className="px-4 py-2 bg-indigo-500 text-white rounded-xl text-sm disabled:opacity-50">
              {(createMutation.isPending || updateMutation.isPending) ? 'Salvando...' : 'Salvar'}
            </button>
            <button onClick={resetForm} className="px-4 py-2 border border-slate-300 rounded-xl text-sm">Cancelar</button>
          </div>
        </div>
      )}

      {isLoading ? (
        <div className="flex justify-center py-12"><Loader2 className="w-6 h-6 text-indigo-500 animate-spin" /></div>
      ) : !queues?.length ? (
        <div className="text-center py-12 text-slate-400">
          <ListOrdered className="w-12 h-12 mx-auto mb-3 text-slate-300" />
          <p className="font-medium text-slate-500">Nenhuma fila criada</p>
          <p className="text-sm mt-1">Crie filas para organizar os atendimentos</p>
        </div>
      ) : (
        <div className="space-y-3">
          {queues.map((q) => (
            <div key={q.id} className={`flex items-center gap-4 bg-white rounded-xl border border-slate-200 p-4 transition-all ${!q.isActive ? 'opacity-50' : ''}`}>
              <div className="w-4 h-4 rounded-full flex-shrink-0" style={{ backgroundColor: q.color || '#6366F1' }} />
              <div className="flex-1 min-w-0">
                <h3 className="font-medium text-slate-800">{q.name}</h3>
                {q.description && <p className="text-sm text-slate-500 truncate">{q.description}</p>}
              </div>
              <span className="text-xs text-slate-400">#{q.sortOrder}</span>
              <div className="flex gap-1">
                <button onClick={() => startEdit(q)} className="p-2 hover:bg-slate-100 rounded-lg"><Edit2 className="w-4 h-4 text-slate-500" /></button>
                {q.isActive ? (
                  <button onClick={() => toggleMutation.mutate({ id: q.id, action: 'deactivate' })} className="p-2 hover:bg-red-50 rounded-lg"><XCircle className="w-4 h-4 text-red-400" /></button>
                ) : (
                  <button onClick={() => toggleMutation.mutate({ id: q.id, action: 'reactivate' })} className="p-2 hover:bg-emerald-50 rounded-lg"><CheckCircle2 className="w-4 h-4 text-emerald-500" /></button>
                )}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}

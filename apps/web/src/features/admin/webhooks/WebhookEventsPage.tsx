import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  RefreshCw,
  CheckCircle2,
  Clock,
  AlertTriangle,
  XCircle,
  HelpCircle,
  Loader2,
} from 'lucide-react'
import { api, type WebhookEvent } from '../../../lib/api'

export function WebhookEventsPage() {
  const [statusFilter, setStatusFilter] = useState<string>('all')
  const queryClient = useQueryClient()
  const eventsQuery = useQuery({
    queryKey: ['webhook-events', statusFilter],
    queryFn: () => api.webhookEvents.list(statusFilter === 'all' ? undefined : statusFilter),
  })
  const reprocessMutation = useMutation({
    mutationFn: (eventId: string) => api.webhookEvents.reprocess(eventId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['webhook-events'] }),
  })

  const events: WebhookEvent[] = eventsQuery.data ?? []

  const getStatusBadge = (status: string) => {
    switch (status.toLowerCase()) {
      case 'processed': return <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-emerald-100 text-emerald-700"><CheckCircle2 className="w-3 h-3" /> Processado</span>
      case 'pending': return <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-amber-100 text-amber-700"><Clock className="w-3 h-3" /> Pendente</span>
      case 'processing': return <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-blue-100 text-blue-700"><Loader2 className="w-3 h-3 animate-spin" /> Processando</span>
      case 'failed': return <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-orange-100 text-orange-700"><AlertTriangle className="w-3 h-3" /> Falhou</span>
      case 'dead': return <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-red-100 text-red-700"><XCircle className="w-3 h-3" /> Morto</span>
      case 'unknown': return <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-slate-100 text-slate-700"><HelpCircle className="w-3 h-3" /> Desconhecido</span>
      default: return <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-slate-100 text-slate-700">{status}</span>
    }
  }

  const filters = [
    { value: 'all', label: 'Todos' },
    { value: 'pending', label: 'Pendentes' },
    { value: 'failed', label: 'Falhos' },
    { value: 'dead', label: 'Mortos' },
    { value: 'unknown', label: 'Desconhecidos' },
  ]

  return (
    <div className="h-full flex flex-col bg-slate-50">
      <div className="bg-white border-b border-slate-200 px-6 py-4">
        <h1 className="text-xl font-semibold text-slate-800">Eventos Webhook</h1>
        <p className="text-sm text-slate-500 mt-0.5">Monitoramento dos eventos recebidos da Meta</p>
      </div>

      <div className="flex-1 overflow-auto p-6">
        <div className="mb-4 flex items-center gap-2 flex-wrap">
          {filters.map((filter) => (
            <button key={filter.value} onClick={() => setStatusFilter(filter.value)} className={`px-4 py-2 rounded-lg text-sm font-medium transition-colors ${statusFilter === filter.value ? 'bg-emerald-500 text-white shadow-sm' : 'bg-white text-slate-600 border border-slate-200 hover:bg-slate-50'}`}>
              {filter.label}
            </button>
          ))}
        </div>

        <div className="bg-white rounded-xl border border-slate-200 overflow-hidden shadow-sm">
          <table className="min-w-full">
            <thead>
              <tr className="bg-slate-50 border-b border-slate-200">
                <th className="px-6 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">ID</th>
                <th className="px-6 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">Phone Number</th>
                <th className="px-6 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">Status</th>
                <th className="px-6 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">Criado em</th>
                <th className="px-6 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">Tentativas</th>
                <th className="px-6 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">Erro</th>
                <th className="px-6 py-3 text-right text-xs font-semibold text-slate-500 uppercase tracking-wider">Ações</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {eventsQuery.isLoading && <tr><td colSpan={7} className="px-6 py-8 text-center text-sm text-slate-500">Carregando eventos...</td></tr>}
              {eventsQuery.isError && <tr><td colSpan={7} className="px-6 py-8 text-center text-sm text-red-600">Não foi possível carregar os eventos.</td></tr>}
              {!eventsQuery.isLoading && !eventsQuery.isError && events.length === 0 && <tr><td colSpan={7} className="px-6 py-8 text-center text-sm text-slate-500">Nenhum evento encontrado.</td></tr>}
              {events.map((event) => (
                <tr key={event.id} className="hover:bg-slate-50 transition-colors">
                  <td className="px-6 py-4"><code className="text-xs font-mono text-slate-600 bg-slate-100 px-2 py-1 rounded">{event.id.slice(0, 12)}...</code></td>
                  <td className="px-6 py-4 text-sm text-slate-700">{event.phoneNumberId}</td>
                  <td className="px-6 py-4">{getStatusBadge(event.status)}</td>
                  <td className="px-6 py-4 text-sm text-slate-500">{new Date(event.createdAt).toLocaleString('pt-BR')}</td>
                  <td className="px-6 py-4"><span className={`text-sm font-medium ${event.retryCount > 0 ? 'text-amber-600' : 'text-slate-500'}`}>{event.retryCount}</span></td>
                  <td className="px-6 py-4 text-sm text-red-600 max-w-[200px] truncate">{event.errorMessage || '-'}</td>
                  <td className="px-6 py-4 text-right">
                    {(event.status === 'Failed' || event.status === 'Dead') && (
                      <button onClick={() => reprocessMutation.mutate(event.id)} disabled={reprocessMutation.isPending} className="flex items-center gap-1.5 text-sm text-blue-600 hover:text-blue-700 font-medium ml-auto disabled:opacity-50"><RefreshCw className="w-3.5 h-3.5" /> Reprocessar</button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <div className="mt-6 bg-white border border-slate-200 rounded-xl p-5 shadow-sm">
          <h3 className="font-medium text-slate-800 mb-3">Legenda de Status</h3>
          <div className="grid grid-cols-2 md:grid-cols-3 gap-3">
            {[
              { status: 'pending', desc: 'Aguardando processamento' },
              { status: 'processing', desc: 'Em processamento' },
              { status: 'processed', desc: 'Processado com sucesso' },
              { status: 'failed', desc: 'Falhou, será reintentado' },
              { status: 'dead', desc: 'Falhou após máx. tentativas' },
              { status: 'unknown', desc: 'Tipo de evento não reconhecido' },
            ].map((item) => (
              <div key={item.status} className="flex items-start gap-2">
                {getStatusBadge(item.status)}
                <p className="text-[10px] text-slate-400 mt-1">{item.desc}</p>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  )
}

import { useState, useEffect } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  Radio,
  Plus,
  Loader2,
  X,
  Play,
  StopCircle,
  Trash2,
  ChevronDown,
  ChevronUp,
  Search,
} from 'lucide-react'
import { api } from '../../lib/api'
import type { BroadcastList, Contact, ServiceQueue } from '../../lib/api'
import { useAuth } from '../../lib/auth'

// ──────────────────────────────── helpers ────────────────────────────────────

function statusLabel(s: string) {
  switch (s) {
    case 'Draft':    return { text: 'Rascunho',   cls: 'bg-slate-100 text-slate-600' }
    case 'Sending':  return { text: 'Enviando',   cls: 'bg-blue-100 text-blue-700' }
    case 'Finished': return { text: 'Concluído',  cls: 'bg-emerald-100 text-emerald-700' }
    case 'Cancelled':return { text: 'Cancelado',  cls: 'bg-red-100 text-red-600' }
    default:         return { text: s,            cls: 'bg-slate-100 text-slate-600' }
  }
}

function recipientStatusLabel(s: string) {
  switch (s) {
    case 'Pending': return { text: 'Pendente',  cls: 'text-slate-500' }
    case 'Sent':    return { text: 'Enviado',   cls: 'text-emerald-600' }
    case 'Failed':  return { text: 'Falhou',    cls: 'text-red-600' }
    default:        return { text: s,           cls: 'text-slate-500' }
  }
}

function ProgressBar({ sent, failed, total }: { sent: number; failed: number; total: number }) {
  if (total === 0) return null
  const sentPct  = Math.round((sent  / total) * 100)
  const failPct  = Math.round((failed / total) * 100)
  return (
    <div className="w-full h-2 bg-slate-100 rounded-full overflow-hidden flex">
      <div className="bg-emerald-500 h-full transition-all" style={{ width: `${sentPct}%` }} />
      <div className="bg-red-400 h-full transition-all"    style={{ width: `${failPct}%` }} />
    </div>
  )
}

// ──────────────────────────────── Create Dialog ───────────────────────────────

function CreateBroadcastDialog({ onClose }: { onClose: () => void }) {
  const queryClient = useQueryClient()
  const [name, setName] = useState('')
  const [message, setMessage] = useState('')
  const [search, setSearch] = useState('')
  const [selectedSourceQueueId, setSelectedSourceQueueId] = useState('')
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set())

  const { data: contacts, isLoading: loadingContacts } = useQuery({
    queryKey: ['contacts', 'broadcast', selectedSourceQueueId],
    queryFn: () => api.contacts.list(undefined, 500, selectedSourceQueueId || undefined),
  })

  const { data: queues } = useQuery({
    queryKey: ['service-queues'],
    queryFn: api.serviceQueues.list,
    select: (data) => data.filter((q) => q.isActive),
  })

  useEffect(() => {
    setSelectedIds(new Set())
  }, [selectedSourceQueueId])

  const createMutation = useMutation({
    mutationFn: () =>
      api.broadcasts.create({
        name,
        message,
        contactIds: [...selectedIds],
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['broadcasts'] })
      onClose()
    },
  })

  const filtered = (contacts ?? []).filter(
    (c: Contact) =>
      (c.name ?? '').toLowerCase().includes(search.toLowerCase()) ||
      c.phoneNumber.includes(search)
  )

  const toggleContact = (id: string) =>
    setSelectedIds((prev) => {
      const next = new Set(prev)
      next.has(id) ? next.delete(id) : next.add(id)
      return next
    })

  const toggleAll = () => {
    if (selectedIds.size === filtered.length) {
      setSelectedIds(new Set())
    } else {
      setSelectedIds(new Set(filtered.map((c: Contact) => c.id)))
    }
  }

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!name.trim() || !message.trim() || selectedIds.size === 0) return
    createMutation.mutate()
  }

  return (
    <div className="fixed inset-0 bg-black/50 backdrop-blur-sm flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-2xl w-full max-w-2xl shadow-2xl flex flex-col max-h-[90vh]">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-slate-200">
          <h2 className="text-lg font-semibold text-slate-800">Nova Lista de Disparo em Massa</h2>
          <button onClick={onClose} className="p-2 hover:bg-slate-100 rounded-lg">
            <X className="w-5 h-5 text-slate-400" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="flex flex-col flex-1 overflow-hidden">
          <div className="px-6 py-4 space-y-4 overflow-y-auto flex-1">
            {createMutation.isError && (
              <div className="p-3 bg-red-50 border border-red-200 rounded-xl text-sm text-red-700">
                {(createMutation.error as Error).message}
              </div>
            )}

            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1.5">Fila de origem</label>
              <select
                value={selectedSourceQueueId}
                onChange={(e) => setSelectedSourceQueueId(e.target.value)}
                className="w-full px-4 py-2.5 border border-slate-200 rounded-xl text-sm focus:ring-2 focus:ring-emerald-500 focus:border-transparent"
              >
                <option value="">Todos os contatos</option>
                {(queues ?? []).map((queue: ServiceQueue) => (
                  <option key={queue.id} value={queue.id}>{queue.name}</option>
                ))}
              </select>
            </div>

            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1.5">Nome da lista *</label>
              <input
                type="text"
                required
                value={name}
                onChange={(e) => setName(e.target.value)}
                maxLength={100}
                placeholder="Ex: Promoção de Natal"
                className="w-full px-4 py-2.5 border border-slate-200 rounded-xl text-sm focus:ring-2 focus:ring-emerald-500 focus:border-transparent"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1.5">Mensagem *</label>
              <textarea
                required
                value={message}
                onChange={(e) => setMessage(e.target.value)}
                maxLength={4096}
                rows={4}
                placeholder="Digite a mensagem que será enviada a todos os destinatários..."
                className="w-full px-4 py-2.5 border border-slate-200 rounded-xl text-sm focus:ring-2 focus:ring-emerald-500 focus:border-transparent resize-none"
              />
              <p className="text-xs text-slate-400 mt-1 text-right">{message.length}/4096</p>
            </div>

            {/* Contact picker */}
            <div>
              <div className="flex items-center justify-between mb-2">
                <label className="text-sm font-medium text-slate-700">
                  Destinatários{' '}
                  <span className="text-slate-400 font-normal">
                    ({selectedIds.size} selecionado{selectedIds.size !== 1 ? 's' : ''})
                  </span>
                </label>
                {filtered.length > 0 && (
                  <button type="button" onClick={toggleAll} className="text-xs text-emerald-600 hover:underline">
                    {selectedIds.size === filtered.length ? 'Desmarcar todos' : 'Selecionar todos'}
                  </button>
                )}
              </div>
              <div className="relative mb-2">
                <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
                <input
                  type="text"
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                  placeholder="Buscar contatos..."
                  className="w-full pl-9 pr-4 py-2 border border-slate-200 rounded-xl text-sm focus:ring-2 focus:ring-emerald-500 focus:border-transparent"
                />
              </div>
              <div className="border border-slate-200 rounded-xl overflow-hidden max-h-48 overflow-y-auto">
                {loadingContacts ? (
                  <div className="flex items-center justify-center py-6">
                    <Loader2 className="w-5 h-5 animate-spin text-emerald-500" />
                  </div>
                ) : filtered.length === 0 ? (
                  <p className="text-sm text-slate-400 text-center py-6">Nenhum contato encontrado.</p>
                ) : (
                  filtered.map((c: Contact) => (
                    <label
                      key={c.id}
                      className="flex items-center gap-3 px-4 py-2.5 hover:bg-slate-50 cursor-pointer border-b border-slate-100 last:border-b-0"
                    >
                      <input
                        type="checkbox"
                        checked={selectedIds.has(c.id)}
                        onChange={() => toggleContact(c.id)}
                        className="rounded accent-emerald-500"
                      />
                      <span className="text-sm text-slate-700 flex-1 truncate">{c.name || 'Sem nome'}</span>
                      <span className="text-xs text-slate-400">{c.phoneNumber}</span>
                    </label>
                  ))
                )}
              </div>
              {selectedIds.size > 500 && (
                <p className="text-xs text-red-600 mt-1">Máximo de 500 destinatários por transmissão.</p>
              )}
            </div>
          </div>

          {/* Footer */}
          <div className="flex justify-end gap-3 px-6 py-4 border-t border-slate-200">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2.5 border border-slate-200 rounded-xl text-sm text-slate-700 hover:bg-slate-50"
            >
              Cancelar
            </button>
            <button
              type="submit"
              disabled={
                createMutation.isPending ||
                !name.trim() ||
                !message.trim() ||
                selectedIds.size === 0 ||
                selectedIds.size > 500
              }
              className="flex items-center gap-2 px-4 py-2.5 bg-emerald-500 text-white rounded-xl text-sm disabled:opacity-50 hover:bg-emerald-600"
            >
              {createMutation.isPending ? (
                <><Loader2 className="w-4 h-4 animate-spin" /> Salvando...</>
              ) : (
                'Criar Lista'
              )}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

// ──────────────────────────── Dispatch Dialog ────────────────────────────────

function DispatchDialog({
  broadcast,
  onClose,
}: {
  broadcast: BroadcastList
  onClose: () => void
}) {
  const { user, isOperator } = useAuth()
  const queryClient = useQueryClient()
  const [selectedLine, setSelectedLine] = useState('')
  const [selectedQueueId, setSelectedQueueId] = useState('')

  const operatorLine =
    isOperator &&
    user?.assignedConnectionType === 'QrCode' &&
    (user?.assignedLineNumber ?? 0) > 0
      ? user.assignedLineNumber!
      : null

  const { data: lines, isLoading: loadingLines } = useQuery({
    queryKey: ['whatsapp-lines'],
    queryFn: () => api.whatsapp.getLines(),
    select: (data) => data.filter((l) => l.connectionType === 'QrCode' && l.isActive),
  })

  const { data: queues } = useQuery({
    queryKey: ['service-queues'],
    queryFn: api.serviceQueues.list,
    select: (data) => data.filter((q) => q.isActive),
  })

  // Resolve the operator's assigned line phoneNumberId once lines are loaded
  const operatorPhoneNumberId =
    operatorLine != null
      ? (lines?.find((l) => l.lineNumber === operatorLine)?.phoneNumberId ?? null)
      : null

  const dispatchMutation = useMutation({
    mutationFn: ({ linePhoneNumberId, queueId }: { linePhoneNumberId: string; queueId?: string }) =>
      api.broadcasts.dispatch(broadcast.id, linePhoneNumberId, queueId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['broadcasts'] })
      onClose()
    },
  })

  // Auto-dispatch as soon as the operator's line is resolved
  useEffect(() => {
    if (operatorPhoneNumberId && !dispatchMutation.isPending && !dispatchMutation.isError) {
      dispatchMutation.mutate({ linePhoneNumberId: operatorPhoneNumberId })
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [operatorPhoneNumberId])

  // Operator path: show a minimal loading/error state — no selector needed
  if (operatorLine != null) {
    return (
      <div className="fixed inset-0 bg-black/50 backdrop-blur-sm flex items-center justify-center z-50 p-4">
        <div className="bg-white rounded-2xl w-full max-w-sm shadow-2xl">
          <div className="flex items-center justify-between px-6 py-4 border-b border-slate-200">
            <h2 className="text-lg font-semibold text-slate-800">Disparar em Massa</h2>
            <button onClick={onClose} className="p-2 hover:bg-slate-100 rounded-lg">
              <X className="w-5 h-5 text-slate-400" />
            </button>
          </div>

          <div className="px-6 py-6 space-y-4">
            {dispatchMutation.isError ? (
              <>
                <div className="p-3 bg-red-50 border border-red-200 rounded-xl text-sm text-red-700">
                  {(dispatchMutation.error as Error).message}
                </div>
                <div className="flex justify-end gap-3">
                  <button
                    onClick={onClose}
                    className="px-4 py-2.5 border border-slate-200 rounded-xl text-sm text-slate-700 hover:bg-slate-50"
                  >
                    Fechar
                  </button>
                  <button
                    onClick={() => operatorPhoneNumberId && dispatchMutation.mutate({ linePhoneNumberId: operatorPhoneNumberId })}
                    disabled={!operatorPhoneNumberId || dispatchMutation.isPending}
                    className="flex items-center gap-2 px-4 py-2.5 bg-emerald-500 text-white rounded-xl text-sm disabled:opacity-50 hover:bg-emerald-600"
                  >
                    <Play className="w-4 h-4" /> Tentar novamente
                  </button>
                </div>
              </>
            ) : (
              <div className="flex flex-col items-center gap-3 py-2">
                <Loader2 className="w-8 h-8 text-emerald-500 animate-spin" />
                <p className="text-sm text-slate-600">
                  Disparando via linha QR Code {operatorLine}…
                </p>
              </div>
            )}
          </div>
        </div>
      </div>
    )
  }

  // Owner / manual path: show line selector as before
  return (
    <div className="fixed inset-0 bg-black/50 backdrop-blur-sm flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-2xl w-full max-w-sm shadow-2xl">
        <div className="flex items-center justify-between px-6 py-4 border-b border-slate-200">
          <h2 className="text-lg font-semibold text-slate-800">Disparar em Massa</h2>
          <button onClick={onClose} className="p-2 hover:bg-slate-100 rounded-lg">
            <X className="w-5 h-5 text-slate-400" />
          </button>
        </div>

        <div className="px-6 py-4 space-y-4">
          <p className="text-sm text-slate-600">
            Selecione a linha QR Code para enviar <strong>{broadcast.name}</strong> a{' '}
            {broadcast.totalCount} destinatário{broadcast.totalCount !== 1 ? 's' : ''}.
          </p>

          {dispatchMutation.isError && (
            <div className="p-3 bg-red-50 border border-red-200 rounded-xl text-sm text-red-700">
              {(dispatchMutation.error as Error).message}
            </div>
          )}

          {queues && queues.length > 0 && (
            <div className="space-y-2">
              <label className="text-sm font-medium text-slate-700">Fila de atendimento <span className="font-normal text-slate-400">(opcional)</span></label>
              <select
                value={selectedQueueId}
                onChange={(e) => setSelectedQueueId(e.target.value)}
                className="w-full px-4 py-2.5 border border-slate-200 rounded-xl text-sm focus:ring-2 focus:ring-emerald-500 focus:border-transparent"
              >
                <option value="">Nenhuma (sem fila)</option>
                {queues.map((q) => (
                  <option key={q.id} value={q.id}>{q.name}</option>
                ))}
              </select>
            </div>
          )}

          {loadingLines ? (
            <div className="flex items-center justify-center py-4">
              <Loader2 className="w-5 h-5 animate-spin text-emerald-500" />
            </div>
          ) : !lines || lines.length === 0 ? (
            <p className="text-sm text-slate-500 text-center py-4">
              Nenhuma linha QR Code ativa encontrada.
            </p>
          ) : (
            <div className="space-y-2">
              <label className="text-sm font-medium text-slate-700">Linha QR Code</label>
              <select
                value={selectedLine}
                onChange={(e) => setSelectedLine(e.target.value)}
                className="w-full px-4 py-2.5 border border-slate-200 rounded-xl text-sm focus:ring-2 focus:ring-emerald-500 focus:border-transparent"
              >
                <option value="">Selecione...</option>
                {lines.map((l) => (
                  <option key={l.phoneNumberId} value={l.phoneNumberId}>
                    Linha {l.lineNumber} — {l.phoneNumberId}
                  </option>
                ))}
              </select>
            </div>
          )}
        </div>

        <div className="flex justify-end gap-3 px-6 py-4 border-t border-slate-200">
          <button
            onClick={onClose}
            className="px-4 py-2.5 border border-slate-200 rounded-xl text-sm text-slate-700 hover:bg-slate-50"
          >
            Cancelar
          </button>
          <button
            onClick={() => dispatchMutation.mutate({ linePhoneNumberId: selectedLine, queueId: selectedQueueId || undefined })}
            disabled={!selectedLine || dispatchMutation.isPending}
            className="flex items-center gap-2 px-4 py-2.5 bg-emerald-500 text-white rounded-xl text-sm disabled:opacity-50 hover:bg-emerald-600"
          >
            {dispatchMutation.isPending ? (
              <><Loader2 className="w-4 h-4 animate-spin" /> Disparando...</>
            ) : (
              <><Play className="w-4 h-4" /> Disparar</>
            )}
          </button>
        </div>
      </div>
    </div>
  )
}

// ────────────────────────────── Broadcast Row ────────────────────────────────

function BroadcastRow({ broadcast }: { broadcast: BroadcastList }) {
  const queryClient = useQueryClient()
  const [expanded, setExpanded] = useState(false)
  const [showDispatch, setShowDispatch] = useState(false)

  const { data: detail, isLoading: loadingDetail } = useQuery({
    queryKey: ['broadcast', broadcast.id],
    queryFn: () => api.broadcasts.get(broadcast.id),
    enabled: expanded,
    refetchInterval: broadcast.status === 'Sending' ? 3000 : false,
  })

  const cancelMutation = useMutation({
    mutationFn: () => api.broadcasts.cancel(broadcast.id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['broadcasts'] }),
  })

  const deleteMutation = useMutation({
    mutationFn: () => api.broadcasts.delete(broadcast.id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['broadcasts'] }),
  })

  const { text: statusText, cls: statusCls } = statusLabel(broadcast.status)

  return (
    <>
      <tr className="hover:bg-slate-50">
        <td className="px-4 py-3">
          <button
            onClick={() => setExpanded(!expanded)}
            className="flex items-center gap-2 text-sm font-medium text-slate-800 hover:text-emerald-600"
          >
            {expanded ? <ChevronUp className="w-4 h-4" /> : <ChevronDown className="w-4 h-4" />}
            {broadcast.name}
          </button>
        </td>
        <td className="px-4 py-3">
          <span className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-medium ${statusCls}`}>
            {statusText}
          </span>
        </td>
        <td className="hidden sm:table-cell px-4 py-3 text-sm text-slate-500">
          {broadcast.sentCount}/{broadcast.totalCount}
          {broadcast.failedCount > 0 && (
            <span className="text-red-400 ml-1">({broadcast.failedCount} falhas)</span>
          )}
        </td>
        <td className="hidden sm:table-cell px-4 py-3">
          <ProgressBar
            sent={broadcast.sentCount}
            failed={broadcast.failedCount}
            total={broadcast.totalCount}
          />
        </td>
        <td className="px-4 py-3 text-right">
          <div className="flex items-center justify-end gap-1">
            {broadcast.status === 'Draft' && (
              <button
                onClick={() => setShowDispatch(true)}
                title="Disparar"
                className="p-2 text-emerald-600 hover:bg-emerald-50 rounded-lg"
              >
                <Play className="w-4 h-4" />
              </button>
            )}
            {broadcast.status === 'Sending' && (
              <button
                onClick={() => cancelMutation.mutate()}
                disabled={cancelMutation.isPending}
                title="Cancelar"
                className="p-2 text-amber-500 hover:bg-amber-50 rounded-lg disabled:opacity-50"
              >
                {cancelMutation.isPending ? (
                  <Loader2 className="w-4 h-4 animate-spin" />
                ) : (
                  <StopCircle className="w-4 h-4" />
                )}
              </button>
            )}
            {broadcast.status !== 'Sending' && (
              <button
                onClick={() => deleteMutation.mutate()}
                disabled={deleteMutation.isPending}
                title="Excluir"
                className="p-2 text-slate-400 hover:bg-red-50 hover:text-red-500 rounded-lg disabled:opacity-50"
              >
                {deleteMutation.isPending ? (
                  <Loader2 className="w-4 h-4 animate-spin" />
                ) : (
                  <Trash2 className="w-4 h-4" />
                )}
              </button>
            )}
          </div>
        </td>
      </tr>

      {/* Expanded recipients */}
      {expanded && (
        <tr>
          <td colSpan={5} className="px-4 pb-4">
            <div className="bg-slate-50 rounded-xl border border-slate-200 overflow-hidden">
              <p className="px-4 py-2 text-xs font-semibold text-slate-500 uppercase border-b border-slate-200 bg-white">
                Mensagem
              </p>
              <p className="px-4 py-3 text-sm text-slate-700 whitespace-pre-wrap">{broadcast.message}</p>
              {loadingDetail ? (
                <div className="flex items-center justify-center py-6">
                  <Loader2 className="w-5 h-5 animate-spin text-emerald-500" />
                </div>
              ) : detail && detail.recipients.length > 0 ? (
                <table className="min-w-full">
                  <thead>
                    <tr className="border-t border-slate-200 bg-white">
                      <th className="px-4 py-2 text-left text-xs font-semibold text-slate-400 uppercase">Contato</th>
                      <th className="px-4 py-2 text-left text-xs font-semibold text-slate-400 uppercase">Status</th>
                      <th className="hidden sm:table-cell px-4 py-2 text-left text-xs font-semibold text-slate-400 uppercase">Enviado em</th>
                      <th className="hidden sm:table-cell px-4 py-2 text-left text-xs font-semibold text-slate-400 uppercase">Erro</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {detail.recipients.map((r) => {
                      const { text: rText, cls: rCls } = recipientStatusLabel(r.status)
                      return (
                        <tr key={r.id}>
                          <td className="px-4 py-2 text-sm text-slate-600">{r.contactId.slice(0, 8)}…</td>
                          <td className={`px-4 py-2 text-sm font-medium ${rCls}`}>{rText}</td>
                          <td className="hidden sm:table-cell px-4 py-2 text-sm text-slate-400">
                            {r.sentAt ? new Date(r.sentAt).toLocaleString('pt-BR') : '—'}
                          </td>
                          <td className="hidden sm:table-cell px-4 py-2 text-sm text-red-500 truncate max-w-xs">
                            {r.errorMessage ?? '—'}
                          </td>
                        </tr>
                      )
                    })}
                  </tbody>
                </table>
              ) : null}
            </div>
          </td>
        </tr>
      )}

      {showDispatch && (
        <DispatchDialog broadcast={broadcast} onClose={() => setShowDispatch(false)} />
      )}
    </>
  )
}

// ──────────────────────────────── Main Page ──────────────────────────────────

export function BroadcastPage() {
  const [showCreate, setShowCreate] = useState(false)

  const { data: broadcasts, isLoading } = useQuery({
    queryKey: ['broadcasts'],
    queryFn: () => api.broadcasts.list(),
    refetchInterval: 5000,
  })

  return (
    <div className="h-full flex flex-col bg-slate-50">
      <div className="bg-white border-b border-slate-200 px-4 sm:px-6 py-4">
        <div className="flex items-center justify-between gap-3">
          <div>
            <h1 className="text-xl font-semibold text-slate-800">Disparo em Massa</h1>
            <p className="text-sm text-slate-500 mt-0.5">
              Envie mensagens em massa via linha QR Code
            </p>
          </div>
          <button
            onClick={() => setShowCreate(true)}
            className="flex items-center gap-2 px-3 sm:px-4 py-2.5 bg-emerald-500 text-white rounded-xl hover:bg-emerald-600 transition-colors whitespace-nowrap"
          >
            <Plus className="w-4 h-4" />
            <span className="hidden sm:inline">Novo Disparo</span>
          </button>
        </div>
      </div>

      <div className="flex-1 overflow-auto p-4 sm:p-6">
        {isLoading ? (
          <div className="flex items-center justify-center py-16">
            <Loader2 className="w-6 h-6 animate-spin text-emerald-500" />
          </div>
        ) : !broadcasts || broadcasts.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-20 text-slate-400 gap-3">
            <Radio className="w-10 h-10 opacity-40" />
            <p className="text-sm">Nenhuma transmissão criada ainda.</p>
            <button
              onClick={() => setShowCreate(true)}
              className="text-sm text-emerald-600 hover:underline"
            >
              Criar primeira transmissão
            </button>
          </div>
        ) : (
          <div className="bg-white rounded-xl border border-slate-200 overflow-hidden">
            <div className="overflow-x-auto">
              <table className="min-w-full">
                <thead>
                  <tr className="bg-slate-50 border-b border-slate-200">
                    <th className="px-4 py-3 text-left text-xs font-semibold text-slate-500 uppercase">Nome</th>
                    <th className="px-4 py-3 text-left text-xs font-semibold text-slate-500 uppercase">Status</th>
                    <th className="hidden sm:table-cell px-4 py-3 text-left text-xs font-semibold text-slate-500 uppercase">Progresso</th>
                    <th className="hidden sm:table-cell px-4 py-3 text-left text-xs font-semibold text-slate-500 uppercase w-32">Envios</th>
                    <th className="px-4 py-3 text-right text-xs font-semibold text-slate-500 uppercase">Ações</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {broadcasts.map((b) => (
                    <BroadcastRow key={b.id} broadcast={b} />
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}
      </div>

      {showCreate && <CreateBroadcastDialog onClose={() => setShowCreate(false)} />}
    </div>
  )
}

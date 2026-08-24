import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Inbox, Loader2, MessageCircle } from 'lucide-react'
import { api, type Conversation, type ServiceQueue } from '../../lib/api'
import { MessagePanel } from '../inbox/MessagePanel'

export function QueueInboxPage() {
  const [selectedConversation, setSelectedConversation] = useState<Conversation | null>(null)

  const queuesQuery = useQuery({
    queryKey: ['service-queues'],
    queryFn: api.serviceQueues.list,
  })
  const conversationsQuery = useQuery({
    queryKey: ['queue-inbox-conversations'],
    queryFn: () => api.conversations.list(undefined, 100),
    refetchInterval: 15000,
  })

  if (selectedConversation) {
    return (
      <MessagePanel
        conversation={selectedConversation}
        onBack={() => setSelectedConversation(null)}
      />
    )
  }

  const conversations = conversationsQuery.data?.items ?? []
  const activeQueues = (queuesQuery.data ?? []).filter((queue) => queue.isActive)
  const transferred = conversations.filter((conversation) => conversation.queueId)

  return (
    <div className="h-full overflow-y-auto bg-[#070b16] p-6 text-white">
      <div className="mx-auto max-w-6xl">
        <div className="mb-6 flex items-center gap-3">
          <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-cyan-500/15 text-cyan-300">
            <Inbox className="h-5 w-5" />
          </div>
          <div>
            <h1 className="text-xl font-bold">Filas Inbox</h1>
            <p className="text-sm text-slate-400">Contatos transferidos manualmente ou pela IA</p>
          </div>
        </div>

        {queuesQuery.isLoading || conversationsQuery.isLoading ? (
          <div className="flex justify-center py-16"><Loader2 className="h-7 w-7 animate-spin text-cyan-400" /></div>
        ) : queuesQuery.isError || conversationsQuery.isError ? (
          <p className="py-16 text-center text-red-400">Não foi possível carregar as filas.</p>
        ) : activeQueues.length === 0 ? (
          <div className="py-16 text-center text-slate-400">
            <Inbox className="mx-auto mb-3 h-10 w-10 text-slate-600" />
            <p>Nenhuma fila ativa criada.</p>
          </div>
        ) : (
          <div className="grid gap-5 md:grid-cols-2 xl:grid-cols-3">
            {activeQueues.map((queue: ServiceQueue) => {
              const queueConversations = transferred.filter((conversation) => conversation.queueId === queue.id)
              return (
                <section key={queue.id} className="overflow-hidden rounded-xl border border-white/10 bg-[#0b1222]">
                  <div className="flex items-center justify-between border-b border-white/10 px-4 py-3">
                    <div className="flex min-w-0 items-center gap-2">
                      <span className="h-3 w-3 shrink-0 rounded-full" style={{ backgroundColor: queue.color || '#22d3ee' }} />
                      <h2 className="truncate font-semibold">{queue.name}</h2>
                    </div>
                    <span className="rounded-full bg-white/10 px-2 py-0.5 text-xs text-slate-300">{queueConversations.length}</span>
                  </div>
                  {queueConversations.length === 0 ? (
                    <p className="px-4 py-8 text-center text-sm text-slate-500">Nenhum contato nesta fila.</p>
                  ) : (
                    <div className="divide-y divide-white/10">
                      {queueConversations.map((conversation) => (
                        <button
                          key={conversation.id}
                          onClick={() => setSelectedConversation(conversation)}
                          className="flex w-full items-center gap-3 px-4 py-3 text-left hover:bg-white/5"
                        >
                          <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-cyan-500/20 font-semibold text-cyan-200">
                            {conversation.contactName.charAt(0).toUpperCase()}
                          </span>
                          <span className="min-w-0 flex-1">
                            <span className="block truncate text-sm font-medium text-white">{conversation.contactName}</span>
                            <span className="block truncate text-xs text-slate-400">{conversation.lastMessage || 'Sem mensagens'}</span>
                          </span>
                          <MessageCircle className="h-4 w-4 shrink-0 text-cyan-400" />
                        </button>
                      ))}
                    </div>
                  )}
                </section>
              )
            })}
          </div>
        )}

        {transferred.length === 0 && activeQueues.length > 0 && !conversationsQuery.isLoading && (
          <p className="mt-6 text-center text-sm text-slate-500">As conversas transferidas aparecerão aqui.</p>
        )}
      </div>
    </div>
  )
}
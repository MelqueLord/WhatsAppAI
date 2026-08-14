import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { api, type Conversation } from '../../lib/api'
import { cn, formatTime, truncate } from '../../lib/utils'
import { MessageCircle, Search, Bot, User, Pause, Loader2 } from 'lucide-react'

interface ConversationListProps {
  selectedId?: string
  onSelect: (conversation: Conversation) => void
}

export function ConversationList({ selectedId, onSelect }: ConversationListProps) {
  const [search, setSearch] = useState('')

  const { data, isLoading } = useQuery({
    queryKey: ['conversations'],
    queryFn: () => api.conversations.list(),
    refetchInterval: 15000,
  })

  const conversations = (data?.items ?? []).filter((c) =>
    c.contactName.toLowerCase().includes(search.toLowerCase()) ||
    c.contactPhone.includes(search)
  )

  return (
    <div className="flex flex-col h-full bg-white">
      <div className="p-4 border-b border-slate-200">
        <h2 className="text-lg font-semibold text-slate-800 mb-3">Conversas</h2>
        <div className="relative">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
          <input
            type="text"
            placeholder="Buscar conversas..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="w-full pl-10 pr-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:border-transparent"
          />
        </div>
      </div>

      <div className="flex-1 overflow-y-auto">
        {isLoading ? (
          <div className="flex items-center justify-center h-full">
            <Loader2 className="w-6 h-6 text-emerald-500 animate-spin" />
          </div>
        ) : conversations.length === 0 ? (
          <div className="flex flex-col items-center justify-center h-full text-slate-400 px-8">
            <div className="w-16 h-16 mb-4 rounded-2xl bg-slate-100 flex items-center justify-center">
              <MessageCircle className="w-8 h-8 text-slate-300" />
            </div>
            <p className="font-medium text-slate-500">Nenhuma conversa</p>
            <p className="text-sm text-center mt-1">As mensagens recebidas aparecerão aqui</p>
          </div>
        ) : (
          conversations.map((conv) => (
            <button
              key={conv.id}
              onClick={() => onSelect(conv)}
              className={cn(
                'w-full flex items-center p-4 hover:bg-slate-50 transition-all duration-200 border-b border-slate-100',
                selectedId === conv.id && 'bg-emerald-50/50 border-l-2 border-l-emerald-500'
              )}
            >
              <div className="w-12 h-12 rounded-full bg-gradient-to-br from-emerald-400 to-emerald-600 flex items-center justify-center text-white font-semibold mr-3 flex-shrink-0 shadow-sm">
                {conv.contactName.charAt(0).toUpperCase()}
              </div>
              <div className="flex-1 min-w-0 text-left">
                <div className="flex items-center justify-between">
                  <h3 className="font-medium text-slate-800 truncate">{conv.contactName}</h3>
                  {conv.lastMessageAt && (
                    <span className="text-[11px] text-slate-400 ml-2 flex-shrink-0">
                      {formatTime(conv.lastMessageAt)}
                    </span>
                  )}
                </div>
                <div className="flex items-center justify-between mt-0.5">
                  <p className="text-sm text-slate-500 truncate">
                    {truncate(conv.lastMessage || 'Sem mensagens', 38)}
                  </p>
                  <div className="flex items-center gap-1.5 ml-2 flex-shrink-0">
                    {conv.mode === 'Human' && (
                      <span className="w-5 h-5 rounded-full bg-blue-100 flex items-center justify-center" title="Humano">
                        <User className="w-3 h-3 text-blue-600" />
                      </span>
                    )}
                    {conv.mode === 'Automatic' && (
                      <span className="w-5 h-5 rounded-full bg-emerald-100 flex items-center justify-center" title="Automático">
                        <Bot className="w-3 h-3 text-emerald-600" />
                      </span>
                    )}
                    {conv.mode === 'Paused' && (
                      <span className="w-5 h-5 rounded-full bg-amber-100 flex items-center justify-center" title="Pausado">
                        <Pause className="w-3 h-3 text-amber-600" />
                      </span>
                    )}
                  </div>
                </div>
              </div>
            </button>
          ))
        )}
      </div>
    </div>
  )
}

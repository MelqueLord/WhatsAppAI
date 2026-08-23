import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { api, type Conversation } from '../../lib/api'
import { cn, formatTime, truncate } from '../../lib/utils'
import { useAuth } from '../../lib/auth'
import { MessageCircle, Search, Bot, User, Pause, Loader2, Filter } from 'lucide-react'

interface ConversationListProps {
  selectedId?: string
  onSelect: (conversation: Conversation) => void
}

export function ConversationList({ selectedId, onSelect }: ConversationListProps) {
  const { isTenantOwner } = useAuth()
  const [search, setSearch] = useState('')
  const [operatorFilter, setOperatorFilter] = useState<string>('all')

  const { data, isLoading, isError } = useQuery({
    queryKey: ['conversations', operatorFilter],
    queryFn: () => api.conversations.list(undefined, 50, operatorFilter === 'all' ? undefined : operatorFilter),
    refetchInterval: 15000,
    refetchOnMount: 'always',
    retry: 2,
  })

  const allConversations = data?.items ?? []

  const { data: operators = [] } = useQuery({
    queryKey: ['operators'],
    queryFn: api.operators.list,
    enabled: isTenantOwner,
  })

  const conversations = [...allConversations].reverse().filter((c) => {
    const matchesSearch =
      c.contactName.toLowerCase().includes(search.toLowerCase()) ||
      c.contactPhone.includes(search)

    return matchesSearch
  })

  return (
    <div className="flex flex-col h-full bg-[#0b1222] text-white">
      <div className="p-4 border-b border-white/10 space-y-3">
        <h2 className="text-lg font-semibold text-white">Conversas</h2>

        {/* Search */}
        <div className="relative">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
          <input
            type="text"
            placeholder="Buscar conversas..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="w-full pl-10 pr-4 py-2.5 bg-[#10223f] border border-white/10 rounded-xl text-sm text-white focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
          />
        </div>

        {/* Operator Filter */}
        {isTenantOwner && (
          <div className="flex items-center gap-2">
            <Filter className="w-4 h-4 text-slate-400 flex-shrink-0" />
            <select
              value={operatorFilter}
              onChange={(e) => setOperatorFilter(e.target.value)}
              className="flex-1 text-sm border border-white/10 rounded-lg px-3 py-1.5 bg-[#10223f] text-white focus:ring-2 focus:ring-blue-500 cursor-pointer"
            >
              <option value="all">Todos os operadores</option>
              <option value="unassigned">Não atribuídas</option>
              {operators.map((op) => (
                <option key={op.userId} value={op.userId}>{op.displayName || op.email}</option>
              ))}
            </select>
          </div>
        )}
      </div>

      <div className="flex-1 overflow-y-auto">
        {isLoading ? (
          <div className="flex items-center justify-center h-full">
            <Loader2 className="w-6 h-6 text-emerald-500 animate-spin" />
          </div>
        ) : isError ? (
          <div className="flex items-center justify-center h-full px-8 text-center text-red-500">
            Não foi possível carregar as conversas.
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
                'w-full flex items-center p-4 hover:bg-[#10223f] transition-all duration-200 border-b border-white/10',
                selectedId === conv.id && 'bg-blue-500/15 border-l-2 border-l-cyan-400'
              )}
            >
              <div className="w-12 h-12 rounded-full bg-gradient-to-br from-emerald-400 to-emerald-600 flex items-center justify-center text-white font-semibold mr-3 flex-shrink-0 shadow-sm">
                {conv.contactName.charAt(0).toUpperCase()}
              </div>
              <div className="flex-1 min-w-0 text-left">
                <div className="flex items-center justify-between">
                  <h3 className="font-medium text-white truncate">{conv.contactName}</h3>
                  {conv.lastMessageAt && (
                    <span className="text-[11px] text-slate-400 ml-2 flex-shrink-0">
                      {formatTime(conv.lastMessageAt)}
                    </span>
                  )}
                </div>
                <div className="flex items-center justify-between mt-0.5">
                  <p className="text-sm text-slate-500 truncate">
                    {truncate(conv.lastMessage || 'Sem mensagens', 32)}
                  </p>
                  <div className="flex items-center gap-1.5 ml-2 flex-shrink-0">
                    {conv.assignedToUserName && (
                      <span
                        className="text-[10px] text-blue-600 bg-blue-50 px-1.5 py-0.5 rounded-full max-w-[60px] truncate"
                        title={conv.assignedToUserName}
                      >
                        {conv.assignedToUserName.split(' ')[0]}
                      </span>
                    )}
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

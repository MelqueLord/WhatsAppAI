import { useQuery } from '@tanstack/react-query'
import { api, type Conversation } from '../../lib/api'
import { cn, formatTime, truncate } from '../../lib/utils'
import { MessageCircle, Search, MoreVertical } from 'lucide-react'

interface ConversationListProps {
  selectedId?: string
  onSelect: (conversation: Conversation) => void
}

export function ConversationList({ selectedId, onSelect }: ConversationListProps) {
  const { data, isLoading, error } = useQuery({
    queryKey: ['conversations'],
    queryFn: () => api.conversations.list(),
  })

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-full">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-green-600" />
      </div>
    )
  }

  if (error) {
    return (
      <div className="flex items-center justify-center h-full text-red-500">
        Erro ao carregar conversas
      </div>
    )
  }

  const conversations = data?.items || []

  return (
    <div className="flex flex-col h-full bg-white">
      {/* Header */}
      <div className="p-4 bg-gray-50 border-b">
        <div className="flex items-center justify-between mb-3">
          <h2 className="text-xl font-semibold text-gray-800">Conversas</h2>
          <button className="p-2 hover:bg-gray-200 rounded-full">
            <MoreVertical className="w-5 h-5 text-gray-600" />
          </button>
        </div>
        <div className="relative">
          <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 w-4 h-4 text-gray-400" />
          <input
            type="text"
            placeholder="Buscar conversas..."
            className="w-full pl-10 pr-4 py-2 bg-white border rounded-lg focus:outline-none focus:ring-2 focus:ring-green-500"
          />
        </div>
      </div>

      {/* Conversation List */}
      <div className="flex-1 overflow-y-auto scrollbar-thin">
        {conversations.length === 0 ? (
          <div className="flex flex-col items-center justify-center h-full text-gray-500">
            <MessageCircle className="w-16 h-16 mb-4 opacity-50" />
            <p className="text-lg">Nenhuma conversa</p>
            <p className="text-sm">As mensagens aparecerão aqui</p>
          </div>
        ) : (
          conversations.map((conv) => (
            <button
              key={conv.id}
              onClick={() => onSelect(conv)}
              className={cn(
                'w-full flex items-center p-4 hover:bg-gray-50 transition-colors border-b',
                selectedId === conv.id && 'bg-green-50'
              )}
            >
              {/* Avatar */}
              <div className="w-12 h-12 rounded-full bg-green-500 flex items-center justify-center text-white font-semibold mr-3 flex-shrink-0">
                {conv.contactName.charAt(0).toUpperCase()}
              </div>

              {/* Content */}
              <div className="flex-1 min-w-0 text-left">
                <div className="flex items-center justify-between">
                  <h3 className="font-medium text-gray-900 truncate">
                    {conv.contactName}
                  </h3>
                  {conv.lastMessageAt && (
                    <span className="text-xs text-gray-500 ml-2">
                      {formatTime(conv.lastMessageAt)}
                    </span>
                  )}
                </div>
                <div className="flex items-center justify-between">
                  <p className="text-sm text-gray-500 truncate">
                    {truncate(conv.lastMessage || 'Sem mensagens', 40)}
                  </p>
                  <div className="flex items-center gap-1 ml-2">
                    {conv.mode === 'Human' && (
                      <span className="w-2 h-2 bg-blue-500 rounded-full" title="Humano" />
                    )}
                    {conv.mode === 'Automatic' && (
                      <span className="w-2 h-2 bg-green-500 rounded-full" title="Automático" />
                    )}
                    {conv.mode === 'Paused' && (
                      <span className="w-2 h-2 bg-yellow-500 rounded-full" title="Pausado" />
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

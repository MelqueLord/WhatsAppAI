import { useState, useEffect, useCallback } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { type Conversation } from '../../lib/api'
import { useSignalR } from '../../lib/signalr'
import { ConversationList } from './ConversationList'
import { MessagePanel } from './MessagePanel'
import { MessageCircle, Wifi, WifiOff } from 'lucide-react'

export function InboxPage() {
  const [selectedConversation, setSelectedConversation] = useState<Conversation | null>(null)
  const [showMobileList, setShowMobileList] = useState(true)
  const queryClient = useQueryClient()

  const handleMessage = useCallback((message: any) => {
    queryClient.invalidateQueries({ queryKey: ['conversations'] })
    queryClient.invalidateQueries({ queryKey: ['messages', message.conversationId] })
  }, [queryClient])

  const handleConversationUpdate = useCallback((conversation: any) => {
    queryClient.invalidateQueries({ queryKey: ['conversations'] })
  }, [queryClient])

  const { isConnected, start } = useSignalR({
    hubUrl: '/hubs/inbox',
    onMessage: handleMessage,
    onConversationUpdate: handleConversationUpdate,
  })

  useEffect(() => {
    start()
  }, [start])

  const handleSelect = (conversation: Conversation) => {
    setSelectedConversation(conversation)
    setShowMobileList(false)
  }

  const handleBack = () => {
    setShowMobileList(true)
    setSelectedConversation(null)
  }

  return (
    <div className="h-screen flex flex-col">
      {/* Top Bar */}
      <div className="whatsapp-gradient text-white px-4 py-2 flex items-center justify-between">
        <h1 className="text-lg font-semibold">WhatsApp AI Manager</h1>
        <div className="flex items-center gap-2">
          {isConnected ? (
            <span className="flex items-center gap-1 text-xs">
              <Wifi className="w-3 h-3" /> Conectado
            </span>
          ) : (
            <span className="flex items-center gap-1 text-xs opacity-70">
              <WifiOff className="w-3 h-3" /> Desconectado
            </span>
          )}
        </div>
      </div>

      {/* Main Content */}
      <div className="flex-1 flex overflow-hidden">
        {/* Sidebar - Conversation List */}
        <div
          className={`
            w-full lg:w-[400px] lg:min-w-[400px] border-r
            ${showMobileList ? 'block' : 'hidden lg:block'}
          `}
        >
          <ConversationList
            selectedId={selectedConversation?.id}
            onSelect={handleSelect}
          />
        </div>

        {/* Main - Message Panel */}
        <div
          className={`
            flex-1
            ${!showMobileList ? 'block' : 'hidden lg:block'}
          `}
        >
          {selectedConversation ? (
            <MessagePanel
              conversation={selectedConversation}
              onBack={handleBack}
            />
          ) : (
            <EmptyState />
          )}
        </div>
      </div>
    </div>
  )
}

function EmptyState() {
  return (
    <div className="flex items-center justify-center h-full bg-gray-50">
      <div className="text-center">
        <MessageCircle className="w-24 h-24 mx-auto mb-6 text-gray-300" />
        <h2 className="text-2xl font-semibold text-gray-600 mb-2">
          WhatsApp AI Manager
        </h2>
        <p className="text-gray-500 max-w-md">
          Selecione uma conversa para visualizar as mensagens ou inicie um novo atendimento.
        </p>
      </div>
    </div>
  )
}

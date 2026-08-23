import { useState } from 'react'
import { type Conversation } from '../../lib/api'
import { ConversationList } from './ConversationList'
import { MessagePanel } from './MessagePanel'
import { MessageCircle, Wifi, WifiOff } from 'lucide-react'
import { useSignalR } from '../../lib/signalr'
import { useQueryClient } from '@tanstack/react-query'
import { useEffect } from 'react'
import { useAuth } from '../../lib/auth'
import { LockKeyhole } from 'lucide-react'

export function InboxPage() {
  const { user } = useAuth()
  const [selectedConversation, setSelectedConversation] = useState<Conversation | null>(null)
  const [showMobileList, setShowMobileList] = useState(true)
  const queryClient = useQueryClient()

  const { isConnected, start: startSignalR } = useSignalR({
    hubUrl: '/hubs/inbox',
    onMessage: () => {
      queryClient.invalidateQueries({ queryKey: ['conversations'] })
    },
    onConversationUpdate: () => {
      queryClient.invalidateQueries({ queryKey: ['conversations'] })
      if (selectedConversation) {
        queryClient.invalidateQueries({ queryKey: ['messages', selectedConversation.id] })
      }
    },
    onStatusUpdate: () => {
      if (selectedConversation) {
        queryClient.invalidateQueries({ queryKey: ['messages', selectedConversation.id] })
      }
    },
  })

  useEffect(() => {
    startSignalR()
  }, [startSignalR])

  if (user?.tenantStatus === 'Suspended') {
    return <SuspendedInbox />
  }

  const handleSelect = (conversation: Conversation) => {
    setSelectedConversation(conversation)
    setShowMobileList(false)
  }

  const handleBack = () => {
    setShowMobileList(true)
    setSelectedConversation(null)
  }

  return (
    <div className="inbox-page h-screen flex flex-col bg-[#070b16] text-white">
      {/* Connection status bar */}
      {!isConnected && (
        <div className="bg-amber-50 border-b border-amber-200 px-4 py-1.5 flex items-center justify-center gap-2">
          <WifiOff className="w-3.5 h-3.5 text-amber-600" />
          <span className="text-xs text-amber-700">Reconectando ao servidor...</span>
        </div>
      )}

      <div className="flex-1 flex overflow-hidden">
        {/* Sidebar - Conversation List */}
        <div
          className={`
            w-full lg:w-[380px] lg:min-w-[380px] border-r border-slate-200 bg-white
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
            <EmptyState isConnected={isConnected} />
          )}
        </div>
      </div>
    </div>
  )
}

function SuspendedInbox() {
  return (
    <div className="h-screen flex items-center justify-center bg-slate-50 p-6">
      <div className="max-w-md text-center">
        <div className="w-16 h-16 mx-auto mb-5 rounded-lg bg-red-50 flex items-center justify-center"><LockKeyhole className="w-8 h-8 text-red-600" /></div>
        <h1 className="text-xl font-semibold text-slate-800">Atendimento suspenso</h1>
        <p className="mt-2 text-sm text-slate-500">A caixa de atendimento, Bot e IA ficam indisponíveis até a regularização do pagamento.</p>
      </div>
    </div>
  )
}

function EmptyState({ isConnected }: { isConnected: boolean }) {
  return (
    <div className="flex items-center justify-center h-full bg-gradient-to-br from-slate-50 to-slate-100">
      <div className="text-center">
        <div className="w-20 h-20 mx-auto mb-6 rounded-2xl bg-emerald-100 flex items-center justify-center">
          <MessageCircle className="w-10 h-10 text-emerald-500" />
        </div>
        <h2 className="text-xl font-semibold text-slate-800 mb-2">
          WhatsApp AI Inbox
        </h2>
        <p className="text-slate-500 max-w-sm mx-auto mb-6">
          Selecione uma conversa para visualizar as mensagens.
        </p>
        <span
          className={`flex items-center gap-1.5 text-xs px-3 py-1.5 rounded-full mx-auto w-fit ${
            isConnected
              ? 'text-emerald-600 bg-emerald-50'
              : 'text-amber-600 bg-amber-50'
          }`}
        >
          {isConnected ? (
            <>
              <Wifi className="w-3 h-3" /> Conectado
            </>
          ) : (
            <>
              <WifiOff className="w-3 h-3" /> Desconectado
            </>
          )}
        </span>
      </div>
    </div>
  )
}

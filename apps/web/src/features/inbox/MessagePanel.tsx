import { useState, useRef, useEffect } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { api, type Conversation } from '../../lib/api'
import { useSignalR } from '../../lib/signalr'
import { cn, formatTime } from '../../lib/utils'
import { TagAssigner } from '../../components/TagAssigner'
import {
  Send,
  Paperclip,
  Smile,
  ArrowLeft,
  Check,
  CheckCheck,
  Clock,
  Bot,
  User,
  AlertCircle,
  MessageCircle,
  Loader2,
  Wifi,
  WifiOff,
  UserPlus,
} from 'lucide-react'

interface MessagePanelProps {
  conversation: Conversation
  onBack?: () => void
}

export function MessagePanel({
  conversation,
  onBack,
}: MessagePanelProps) {
  const [message, setMessage] = useState('')
  const [modeOverride, setModeOverride] = useState<string | null>(null)
  const [showSaveContact, setShowSaveContact] = useState(false)
  const [contactName, setContactName] = useState('')

  const messagesEndRef = useRef<HTMLDivElement>(null)
  const queryClient = useQueryClient()

  const isPhoneNumber = /^\+?\d+$/.test(
    conversation.contactName.replace(/\s/g, '')
  )

  const saveContactMutation = useMutation({
    mutationFn: (name: string) =>
      api.contacts.create({
        phoneNumber: conversation.contactPhone,
        name,
        startConversation: false,
      }),

    onSuccess: () => {
      setShowSaveContact(false)

      queryClient.invalidateQueries({
        queryKey: ['conversations'],
      })
    },
  })

  const {
    data: messagesData,
    isLoading,
    isError,
  } = useQuery({
    queryKey: ['messages', conversation.id],

    queryFn: () =>
      api.conversations.getMessages(conversation.id),

    refetchInterval: 10000,

    refetchOnMount: 'always',

    retry: 2,

    enabled: Boolean(conversation.id),
  })

  const messages = [...(messagesData?.items ?? [])].reverse()

  const sendMutation = useMutation({
    mutationFn: (content: string) =>
      api.conversations.sendMessage(
        conversation.id,
        content
      ),

    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: ['messages', conversation.id],
      })

      queryClient.invalidateQueries({
        queryKey: ['conversations'],
      })
    },
  })

  const modeMutation = useMutation({
    mutationFn: (newMode: string) =>
      api.conversations.switchMode(
        conversation.id,
        newMode,
        conversation.version
      ),

    onSuccess: (data) => {
      setModeOverride(data.mode)

      queryClient.invalidateQueries({
        queryKey: ['conversations'],
      })
    },
  })

  const {
    isConnected,
    start: startSignalR,
  } = useSignalR({
    hubUrl: '/hubs/inbox',

    onMessage: (msg) => {
      if (
        typeof msg === 'object' &&
        msg !== null &&
        'conversationId' in msg &&
        msg.conversationId === conversation.id
      ) {
        queryClient.invalidateQueries({
          queryKey: [
            'messages',
            conversation.id,
          ],
        })
      }

      queryClient.invalidateQueries({
        queryKey: ['conversations'],
      })
    },

    onStatusUpdate: () => {
      queryClient.invalidateQueries({
        queryKey: [
          'messages',
          conversation.id,
        ],
      })
    },

    onConversationUpdate: () => {
      queryClient.invalidateQueries({
        queryKey: ['conversations'],
      })
    },
  })

  useEffect(() => {
    startSignalR()
  }, [startSignalR])

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({
      behavior: 'smooth',
    })
  }, [messages])

  const mode =
    modeOverride ?? conversation.mode

  const handleSend = () => {
    if (!message.trim()) {
      return
    }

    sendMutation.mutate(message)

    setMessage('')
  }

  const handleModeChange = (
    newMode: string
  ) => {
    modeMutation.mutate(newMode)
  }

  const getStatusIcon = (
    status: string
  ) => {
    switch (status) {
      case 'Queued':
        return (
          <Clock className="w-3.5 h-3.5 text-slate-400" />
        )

      case 'Sent':
        return (
          <Check className="w-3.5 h-3.5 text-slate-400" />
        )

      case 'Delivered':
        return (
          <CheckCheck className="w-3.5 h-3.5 text-slate-400" />
        )

      case 'Read':
        return (
          <CheckCheck className="w-3.5 h-3.5 text-emerald-500" />
        )

      case 'Failed':
        return (
          <AlertCircle className="w-3.5 h-3.5 text-red-500" />
        )

      default:
        return null
    }
  }

  const getModeBadge = () => {
    switch (mode) {
      case 'Automatic':
        return (
          <span className="flex items-center gap-1.5 text-xs font-medium text-emerald-700 bg-emerald-100 px-2.5 py-1 rounded-full">
            <Bot className="w-3.5 h-3.5" />
            Automático
          </span>
        )

      case 'Human':
        return (
          <span className="flex items-center gap-1.5 text-xs font-medium text-blue-700 bg-blue-100 px-2.5 py-1 rounded-full">
            <User className="w-3.5 h-3.5" />
            Humano
          </span>
        )

      default:
        return (
          <span className="flex items-center gap-1.5 text-xs font-medium text-amber-700 bg-amber-100 px-2.5 py-1 rounded-full">
            Pausado
          </span>
        )
    }
  }

  return (
    <div className="flex flex-col h-full bg-[#070b16] text-white">
      {/* Header */}
      <div className="bg-[#0b1222] border-b border-white/10 px-4 py-3 flex items-center shadow-sm">
        {onBack && (
          <button
            onClick={onBack}
            className="mr-3 lg:hidden p-1 hover:bg-slate-100 rounded-lg"
          >
            <ArrowLeft className="w-5 h-5 text-slate-600" />
          </button>
        )}

        <div className="w-10 h-10 rounded-full bg-gradient-to-br from-emerald-400 to-emerald-600 flex items-center justify-center text-white font-semibold mr-3 shadow-sm">
          {conversation.contactName
            .charAt(0)
            .toUpperCase()}
        </div>

        <div className="flex-1 min-w-0">
          <h3 className="font-semibold text-white truncate">
            {conversation.contactName}
          </h3>

          <div className="flex items-center gap-2">
            <p className="text-xs text-slate-500">
              {conversation.contactPhone}
            </p>

            {conversation.contactId && (
              <TagAssigner
                contactId={conversation.contactId}
                compact
              />
            )}
          </div>
        </div>

        <div className="flex items-center gap-2">
          {isPhoneNumber && (
            <button
              onClick={() =>
                setShowSaveContact(true)
              }
              className="flex items-center gap-1 px-2 py-1 text-xs text-emerald-600 hover:bg-emerald-50 rounded-lg"
              title="Salvar contato"
            >
              <UserPlus className="w-4 h-4" />
              Salvar
            </button>
          )}

          {isConnected ? (
            <Wifi className="w-4 h-4 text-emerald-500" />
          ) : (
            <WifiOff className="w-4 h-4 text-slate-400" />
          )}

          {getModeBadge()}

          <button
            onClick={() =>
              handleModeChange('Human')
            }
            disabled={
              modeMutation.isPending ||
              mode === 'Human'
            }
            className="text-xs rounded-lg bg-blue-50 px-2 py-1.5 font-medium text-blue-700 disabled:opacity-50"
          >
            <User className="mr-1 inline h-3.5 w-3.5" />
            Assumir
          </button>

          <button
            onClick={() =>
              handleModeChange(
                'Automatic'
              )
            }
            disabled={
              modeMutation.isPending ||
              mode === 'Automatic'
            }
            className="text-xs rounded-lg bg-emerald-50 px-2 py-1.5 font-medium text-emerald-700 disabled:opacity-50"
          >
            <Bot className="mr-1 inline h-3.5 w-3.5" />
            Automático
          </button>
        </div>
      </div>

      {/* Messages */}
      <div className="flex-1 overflow-y-auto p-4">
        {isLoading ? (
          <div className="flex items-center justify-center h-full">
            <Loader2 className="w-6 h-6 text-emerald-500 animate-spin" />
          </div>
        ) : isError ? (
          <div className="flex items-center justify-center h-full">
            <div className="text-center text-red-500">
              Não foi possível carregar as
              mensagens.
            </div>
          </div>
        ) : messages.length === 0 ? (
          <div className="flex items-center justify-center h-full">
            <div className="text-center">
              <div className="w-16 h-16 mx-auto mb-3 rounded-2xl bg-white/80 flex items-center justify-center">
                <MessageCircle className="w-8 h-8 text-slate-300" />
              </div>

              <p className="text-slate-500 font-medium">
                Nenhuma mensagem
              </p>

              <p className="text-sm text-slate-400">
                Inicie a conversa enviando
                uma mensagem
              </p>
            </div>
          </div>
        ) : (
          <div className="space-y-2 max-w-3xl mx-auto">
            {messages.map((msg) => (
              <div
                key={msg.id}
                className={cn(
                  'flex',
                  msg.direction ===
                    'Outbound'
                    ? 'justify-end'
                    : 'justify-start'
                )}
              >
                <div
                  className={cn(
                    'max-w-[75%] px-3.5 py-2.5 rounded-2xl',

                    msg.direction ===
                      'Outbound'
                      ? 'bg-emerald-500 text-white rounded-br-md'
                      : 'bg-[#10223f] text-slate-100 rounded-bl-md shadow-sm'
                  )}
                >
                  {msg.direction ===
                    'Inbound' &&
                    msg.senderName && (
                      <p className="text-[11px] font-medium text-emerald-600 mb-0.5">
                        {
                          msg.senderName
                        }
                      </p>
                    )}

                  {msg.content && (
                    <p className="text-sm whitespace-pre-wrap break-words">
                      {msg.content}
                    </p>
                  )}

                  <div
                    className={cn(
                      'flex items-center gap-1 mt-1',

                      msg.direction ===
                        'Outbound'
                        ? 'justify-end'
                        : 'justify-start'
                    )}
                  >
                    <span
                      className={cn(
                        'text-[10px]',

                        msg.direction ===
                          'Outbound'
                          ? 'text-emerald-100'
                          : 'text-slate-400'
                      )}
                    >
                      {formatTime(
                        msg.createdAt
                      )}
                    </span>

                    {msg.direction ===
                      'Outbound' &&
                      getStatusIcon(
                        msg.status
                      )}
                  </div>
                </div>
              </div>
            ))}

            <div
              ref={messagesEndRef}
            />
          </div>
        )}
      </div>

      {/* Input */}
      <div className="bg-[#0b1222] border-t border-white/10 p-4">
        {!conversation.isWindowOpen && (
          <div className="flex items-center gap-2 bg-amber-50 border border-amber-200 rounded-xl p-3 mb-3">
            <AlertCircle className="w-4 h-4 text-amber-500 flex-shrink-0" />

            <p className="text-xs text-amber-700">
              Janela de 24h fechada.
              Apenas templates são
              permitidos.
            </p>
          </div>
        )}

        <div className="flex items-center gap-2">
          <button className="p-2.5 hover:bg-slate-100 rounded-xl transition-colors">
            <Smile className="w-5 h-5 text-slate-400" />
          </button>

          <button className="p-2.5 hover:bg-slate-100 rounded-xl transition-colors">
            <Paperclip className="w-5 h-5 text-slate-400" />
          </button>

          <input
            type="text"
            value={message}
            onChange={(e) =>
              setMessage(
                e.target.value
              )
            }
            onKeyDown={(e) =>
              e.key === 'Enter' &&
              !e.shiftKey &&
              handleSend()
            }
            placeholder="Digite uma mensagem..."
            disabled={
              !conversation.isWindowOpen ||
              sendMutation.isPending
            }
            className="flex-1 px-4 py-2.5 bg-[#10223f] border border-white/10 rounded-xl text-sm text-white focus:ring-2 focus:ring-blue-500 focus:border-transparent disabled:opacity-50 disabled:cursor-not-allowed"
          />

          <button
            onClick={handleSend}
            disabled={
              !message.trim() ||
              !conversation.isWindowOpen ||
              sendMutation.isPending
            }
            className="p-2.5 bg-emerald-500 text-white rounded-xl hover:bg-emerald-600 disabled:opacity-50 disabled:cursor-not-allowed transition-all shadow-sm"
          >
            {sendMutation.isPending ? (
              <Loader2 className="w-5 h-5 animate-spin" />
            ) : (
              <Send className="w-5 h-5" />
            )}
          </button>
        </div>
      </div>

      {/* Save Contact Modal */}
      {showSaveContact && (
        <div className="fixed inset-0 bg-black/50 backdrop-blur-sm flex items-center justify-center z-50">
          <div className="bg-[#0b1222] border border-white/10 rounded-2xl p-6 w-full max-w-md shadow-2xl shadow-black/40">
            <h2 className="text-lg font-semibold text-white mb-4">
              Salvar Contato
            </h2>

            <p className="text-sm text-slate-500 mb-4">
              Salvar{' '}
              <strong>
                {
                  conversation.contactPhone
                }
              </strong>{' '}
              na lista de contatos.
            </p>

            <form
              onSubmit={(e) => {
                e.preventDefault()

                saveContactMutation.mutate(
                  contactName ||
                    conversation.contactPhone
                )
              }}
            >
              <input
                type="text"
                value={contactName}
                onChange={(e) =>
                  setContactName(
                    e.target.value
                  )
                }
                placeholder="Nome do contato"
                className="w-full px-4 py-2.5 border border-slate-200 rounded-xl text-sm mb-4"
                autoFocus
              />

              <div className="flex justify-end gap-3">
                <button
                  type="button"
                  onClick={() =>
                    setShowSaveContact(
                      false
                    )
                  }
                  className="px-4 py-2.5 border border-slate-200 rounded-xl text-sm"
                >
                  Cancelar
                </button>

                <button
                  type="submit"
                  disabled={
                    saveContactMutation.isPending
                  }
                  className="px-4 py-2.5 bg-emerald-500 text-white rounded-xl text-sm disabled:opacity-50"
                >
                  {saveContactMutation.isPending
                    ? 'Salvando...'
                    : 'Salvar'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}
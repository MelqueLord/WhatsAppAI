import { useState, useRef, useEffect } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { api, type Conversation, type ServiceQueue } from '../../lib/api'
import { useSignalR } from '../../lib/signalr'
import { cn, formatTime } from '../../lib/utils'
import { TagAssigner } from '../../components/TagAssigner'
import { useAuth } from '../../lib/auth'
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
  XCircle,
  ThumbsDown,
  ThumbsUp,
} from 'lucide-react'

interface MessagePanelProps {
  conversation: Conversation
  onBack?: () => void
  onConversationClosed?: () => void
}

export function MessagePanel({
  conversation,
  onBack,
  onConversationClosed,
}: MessagePanelProps) {
  const { user } = useAuth()
  const queuesEnabled = user?.automaticDistributionEnabled === true
  const tagsEnabled = user?.tagsEnabled === true
  const [message, setMessage] = useState('')
  const [templateName, setTemplateName] = useState('')
  const [templateLanguage, setTemplateLanguage] = useState('pt_BR')
  const [templateParameters, setTemplateParameters] = useState('')
  const [modeOverride, setModeOverride] = useState<string | null>(null)
  const [showSaveContact, setShowSaveContact] = useState(false)
  const [contactName, setContactName] = useState('')
  const [selectedQueueId, setSelectedQueueId] = useState(conversation.queueId ?? '')
  const [feedbackDraft, setFeedbackDraft] = useState<{
    messageId: string
    rating: 'Helpful' | 'NeedsCorrection'
  } | null>(null)
  const [feedbackNote, setFeedbackNote] = useState('')
  const [correctedResponse, setCorrectedResponse] = useState('')
  const [closeError, setCloseError] = useState<string | null>(null)

  const messagesEndRef = useRef<HTMLDivElement>(null)
  const queryClient = useQueryClient()

  const isPhoneNumber = /^\+?\d+$/.test(
    conversation.contactName.replace(/\s/g, '')
  )
  const isConversationClosed = conversation.status === 'Closed'
  const isConversationOpen = !isConversationClosed && (conversation.isQrCode || conversation.isWindowOpen)

  const { data: serviceQueues = [] } = useQuery({
    queryKey: ['service-queues', 'active'],
    queryFn: async () => {
      const response = await api.serviceQueues.list()
      return response.filter((queue) => queue.isActive)
    },
    enabled: queuesEnabled,
  })

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

    refetchInterval: 2000,

    refetchOnMount: 'always',

    retry: 2,

    enabled: Boolean(conversation.id),
  })

  const messages = [...(messagesData?.items ?? [])].reverse()

  const sendMutation = useMutation({
    mutationFn: (payload: { content: string; template?: { name: string; language: string; parameters?: string[] } }) =>
      api.conversations.sendMessage(
        conversation.id,
        payload.content,
        payload.template
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

  const queueMutation = useMutation({
    mutationFn: (queueId: string | null) =>
      api.serviceQueues.assign(conversation.id, queueId),
    onSuccess: (data) => {
      setSelectedQueueId(data.queueId ?? '')
      queryClient.invalidateQueries({ queryKey: ['conversations'] })
      queryClient.invalidateQueries({ queryKey: ['messages', conversation.id] })
    },
  })

  const closeMutation = useMutation({
    mutationFn: async () => {
      // The list item can have an older version after a new message, queue
      // assignment or mode change. Always read the current aggregate before
      // applying the optimistic-concurrency guarded close operation.
      const latest = await api.conversations.get(conversation.id)

      try {
        return await api.conversations.close(conversation.id, latest.version)
      } catch (error) {
        // A message may arrive between GET and POST. Refresh once so closing
        // remains reliable without weakening the backend concurrency guard.
        if (!(error instanceof Error) || !error.message.toLowerCase().includes('version conflict'))
          throw error

        const refreshed = await api.conversations.get(conversation.id)
        return api.conversations.close(conversation.id, refreshed.version)
      }
    },
    onMutate: () => setCloseError(null),
    onSuccess: (data) => {
      queryClient.setQueryData<Conversation>(['conversation', conversation.id], (current) =>
        current
          ? { ...current, status: data.status, version: data.version, queueId: undefined }
          : current,
      )
      queryClient.invalidateQueries({ queryKey: ['conversations'] })
      onConversationClosed?.()
    },
    onError: (error) => {
      setCloseError(error instanceof Error ? error.message : 'Não foi possível encerrar a conversa.')
    },
  })

  const feedbackMutation = useMutation({
    mutationFn: (payload: {
      responseMessageId: string
      rating: 'Helpful' | 'NeedsCorrection'
      note?: string
      correctedResponse?: string
    }) => api.conversations.submitAiFeedback(conversation.id, payload.responseMessageId, payload),
    onSuccess: () => {
      setFeedbackDraft(null)
      setFeedbackNote('')
      setCorrectedResponse('')
      queryClient.invalidateQueries({ queryKey: ['messages', conversation.id] })
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

    sendMutation.mutate({ content: message })

    setMessage('')
  }

  const handleTemplateSend = () => {
    const name = templateName.trim()
    const language = templateLanguage.trim()
    if (!name || !language) return

    sendMutation.mutate({
      content: '',
      template: {
        name,
        language,
        parameters: templateParameters
          .split(',')
          .map((parameter) => parameter.trim())
          .filter(Boolean),
      },
    })
  }

  const handleAiFeedback = (messageId: string, rating: 'Helpful' | 'NeedsCorrection') => {
    if (rating === 'NeedsCorrection') {
      setFeedbackDraft({ messageId, rating })
      setFeedbackNote('')
      setCorrectedResponse('')
      return
    }

    feedbackMutation.mutate({ responseMessageId: messageId, rating })
  }

  const submitCorrection = () => {
    if (!feedbackDraft || (!feedbackNote.trim() && !correctedResponse.trim())) return
    feedbackMutation.mutate({
      responseMessageId: feedbackDraft.messageId,
      rating: feedbackDraft.rating,
      note: feedbackNote.trim() || undefined,
      correctedResponse: correctedResponse.trim() || undefined,
    })
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
    <div className="flex h-full min-h-0 min-w-0 flex-col overflow-hidden bg-[#070b16] text-white">
      {/* Header */}
      <div className="flex shrink-0 flex-wrap items-center gap-x-2 gap-y-2 border-b border-white/10 bg-[#0b1222] px-3 py-3 shadow-sm sm:px-4">
        {onBack && (
          <button
            onClick={onBack}
            className="mr-1 rounded-lg p-1 hover:bg-slate-100 lg:mr-3 lg:hidden"
          >
            <ArrowLeft className="w-5 h-5 text-slate-600" />
          </button>
        )}

        <div className="mr-1 flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-gradient-to-br from-emerald-400 to-emerald-600 font-semibold text-white shadow-sm sm:mr-3">
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

            {conversation.contactId && tagsEnabled && (
              <TagAssigner
                contactId={conversation.contactId}
                compact
              />
            )}
          </div>
        </div>

        <div className="flex w-full flex-wrap items-center justify-end gap-2 lg:w-auto">
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

          {serviceQueues.length > 0 && (
            <select
              value={selectedQueueId}
              onChange={(event) => queueMutation.mutate(event.target.value || null)}
              disabled={queueMutation.isPending}
              aria-label="Fila da conversa"
              className="max-w-36 rounded-lg border border-white/10 bg-[#10223f] px-2 py-1.5 text-xs text-white"
            >
              <option value="">Sem fila</option>
              {serviceQueues.map((queue: ServiceQueue) => (
                <option key={queue.id} value={queue.id}>{queue.name}</option>
              ))}
            </select>
          )}
          {isConnected ? (
            <Wifi className="w-4 h-4 text-emerald-500" />
          ) : (
            <WifiOff className="w-4 h-4 text-slate-400" />
          )}

          {getModeBadge()}

          {isConversationClosed ? (
            <span className="rounded-lg bg-slate-700 px-2 py-1.5 text-xs font-medium text-slate-200">
              Encerrada
            </span>
          ) : (
            <button
              onClick={() => closeMutation.mutate()}
              disabled={closeMutation.isPending}
              className="text-xs rounded-lg bg-red-50 px-2 py-1.5 font-medium text-red-700 disabled:opacity-50"
              title="Mover para conversas encerradas"
            >
              <XCircle className="mr-1 inline h-3.5 w-3.5" />
              {closeMutation.isPending ? 'Encerrando…' : 'Encerrar'}
            </button>
          )}

          {closeError && (
            <span role="alert" className="w-full text-right text-[11px] text-red-300">
              {closeError}
            </span>
          )}

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
      <div className="min-h-0 min-w-0 flex-1 overflow-x-hidden overflow-y-auto p-3 sm:p-4">
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
          <div className="mx-auto w-full max-w-3xl space-y-2">
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
                    'min-w-0 max-w-[88%] px-3.5 py-2.5 rounded-2xl sm:max-w-[75%]',

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

                  {msg.direction === 'Outbound' && msg.aiInteractionId && (
                    <div className="mt-1 border-t border-white/10 pt-1.5">
                      <div className="flex items-center gap-1 text-[10px] text-emerald-100">
                        <span className="mr-1">Resposta da IA</span>
                        <button
                          type="button"
                          aria-label="Marcar resposta da IA como útil"
                          title="Resposta útil"
                          onClick={() => handleAiFeedback(msg.id, 'Helpful')}
                          disabled={feedbackMutation.isPending}
                          className={cn(
                            'rounded p-1 transition-colors hover:bg-white/20 disabled:opacity-50',
                            msg.aiFeedback?.rating === 'Helpful' && 'bg-white/20',
                          )}
                        >
                          <ThumbsUp className="h-3.5 w-3.5" />
                        </button>
                        <button
                          type="button"
                          aria-label="Informar correção na resposta da IA"
                          title="Precisa de correção"
                          onClick={() => handleAiFeedback(msg.id, 'NeedsCorrection')}
                          disabled={feedbackMutation.isPending}
                          className={cn(
                            'rounded p-1 transition-colors hover:bg-white/20 disabled:opacity-50',
                            msg.aiFeedback?.rating === 'NeedsCorrection' && 'bg-white/20',
                          )}
                        >
                          <ThumbsDown className="h-3.5 w-3.5" />
                        </button>
                        {msg.aiFeedback && <span className="ml-1">Registrado</span>}
                      </div>

                      {feedbackDraft?.messageId === msg.id && (
                        <div className="mt-2 space-y-2">
                          <textarea
                            value={correctedResponse}
                            onChange={(event) => setCorrectedResponse(event.target.value)}
                            maxLength={160}
                            rows={2}
                            placeholder="Como a IA deveria responder? (até 160 caracteres)"
                            className="w-full resize-none rounded-lg border border-white/10 bg-[#0b1222] px-2 py-1.5 text-xs text-white placeholder:text-slate-500"
                          />
                          <input
                            value={feedbackNote}
                            onChange={(event) => setFeedbackNote(event.target.value)}
                            maxLength={1000}
                            placeholder="Explique o que precisa melhorar (opcional)"
                            className="w-full rounded-lg border border-white/10 bg-[#0b1222] px-2 py-1.5 text-xs text-white placeholder:text-slate-500"
                          />
                          <div className="flex justify-end gap-2">
                            <button
                              type="button"
                              onClick={() => setFeedbackDraft(null)}
                              className="rounded-lg px-2 py-1 text-[11px] text-slate-300 hover:bg-white/10"
                            >
                              Cancelar
                            </button>
                            <button
                              type="button"
                              onClick={submitCorrection}
                              disabled={feedbackMutation.isPending || (!feedbackNote.trim() && !correctedResponse.trim())}
                              className="rounded-lg bg-white/20 px-2 py-1 text-[11px] font-medium text-white hover:bg-white/30 disabled:opacity-50"
                            >
                              {feedbackMutation.isPending ? 'Salvando…' : 'Salvar correção'}
                            </button>
                          </div>
                        </div>
                      )}
                    </div>
                  )}
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
      <div className="shrink-0 border-t border-white/10 bg-[#0b1222] p-3 sm:p-4">
        {isConversationClosed ? (
          <div className="flex items-center gap-2 bg-slate-100 border border-slate-200 rounded-xl p-3 mb-3">
            <XCircle className="w-4 h-4 text-slate-500 flex-shrink-0" />
            <p className="text-xs text-slate-600">
              Conversa encerrada. Se o cliente enviar uma nova mensagem, ela será reaberta automaticamente com o histórico preservado.
            </p>
          </div>
        ) : !isConversationOpen && (
          <div className="flex items-center gap-2 bg-amber-50 border border-amber-200 rounded-xl p-3 mb-3">
            <AlertCircle className="w-4 h-4 text-amber-500 flex-shrink-0" />

            <p className="text-xs text-amber-700">
              Janela de 24h fechada.
              Apenas templates são
              permitidos.
            </p>
          </div>
        )}

        {!isConversationClosed && !isConversationOpen && !conversation.isQrCode && (
          <div className="mb-3 space-y-2 rounded-xl border border-white/10 bg-[#10223f] p-3">
            <p className="text-xs font-medium text-slate-200">Enviar template aprovado pela Meta</p>
            <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
              <input
                value={templateName}
                onChange={(event) => setTemplateName(event.target.value)}
                placeholder="Nome do template"
                maxLength={512}
                className="rounded-lg border border-white/10 bg-[#0b1222] px-3 py-2 text-xs text-white placeholder:text-slate-500"
              />
              <input
                value={templateLanguage}
                onChange={(event) => setTemplateLanguage(event.target.value)}
                placeholder="Idioma (ex.: pt_BR)"
                maxLength={20}
                className="rounded-lg border border-white/10 bg-[#0b1222] px-3 py-2 text-xs text-white placeholder:text-slate-500"
              />
            </div>
            <div className="flex gap-2">
              <input
                value={templateParameters}
                onChange={(event) => setTemplateParameters(event.target.value)}
                placeholder="Parâmetros do corpo, separados por vírgula (opcional)"
                className="flex-1 rounded-lg border border-white/10 bg-[#0b1222] px-3 py-2 text-xs text-white placeholder:text-slate-500"
              />
              <button
                onClick={handleTemplateSend}
                disabled={!templateName.trim() || !templateLanguage.trim() || sendMutation.isPending}
                className="rounded-lg bg-emerald-500 px-3 py-2 text-xs font-medium text-white hover:bg-emerald-600 disabled:opacity-50"
              >
                {sendMutation.isPending ? 'Enviando…' : 'Enviar template'}
              </button>
            </div>
          </div>
        )}

        <div className="flex items-center gap-2">
          <button className="shrink-0 rounded-xl p-2.5 hover:bg-slate-100 transition-colors">
            <Smile className="w-5 h-5 text-slate-400" />
          </button>

          <button className="shrink-0 rounded-xl p-2.5 hover:bg-slate-100 transition-colors">
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
              !isConversationOpen ||
              sendMutation.isPending
            }
            className="min-w-0 flex-1 rounded-xl border border-white/10 bg-[#10223f] px-3 py-2.5 text-sm text-white focus:border-transparent focus:ring-2 focus:ring-blue-500 disabled:cursor-not-allowed disabled:opacity-50 sm:px-4"
          />

          <button
            onClick={handleSend}
            disabled={
              !message.trim() ||
              !isConversationOpen ||
              sendMutation.isPending
            }
            className="shrink-0 rounded-xl bg-emerald-500 p-2.5 text-white shadow-sm transition-all hover:bg-emerald-600 disabled:cursor-not-allowed disabled:opacity-50"
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

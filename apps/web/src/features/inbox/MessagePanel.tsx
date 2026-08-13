import { useState, useRef, useEffect } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { api, type Conversation, type Message } from '../../lib/api'
import { cn, formatTime } from '../../lib/utils'
import { MediaDisplay } from './media/MediaDisplay'
import {
  Phone,
  Video,
  MoreVertical,
  Send,
  Paperclip,
  Smile,
  ArrowLeft,
  Check,
  CheckCheck,
  Clock,
  Bot,
  User,
  Shield,
} from 'lucide-react'

interface MessagePanelProps {
  conversation: Conversation
  onBack?: () => void
}

export function MessagePanel({ conversation, onBack }: MessagePanelProps) {
  const [message, setMessage] = useState('')
  const messagesEndRef = useRef<HTMLDivElement>(null)
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['messages', conversation.id],
    queryFn: () => api.conversations.getMessages(conversation.id),
  })

  const messages = data?.items || []

  const sendMutation = useMutation({
    mutationFn: (content: string) => api.conversations.sendMessage(conversation.id, content),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['messages', conversation.id] })
      queryClient.invalidateQueries({ queryKey: ['conversations'] })
    },
  })

  const modeMutation = useMutation({
    mutationFn: (mode: string) => api.conversations.switchMode(conversation.id, mode),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['conversations'] })
    },
  })

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages])

  const handleSend = () => {
    if (!message.trim()) return
    sendMutation.mutate(message)
    setMessage('')
  }

  const getStatusIcon = (status: string) => {
    switch (status) {
      case 'Queued':
        return <Clock className="w-3 h-3 text-gray-400" />
      case 'Sent':
        return <Check className="w-3 h-3 text-gray-400" />
      case 'Delivered':
        return <CheckCheck className="w-3 h-3 text-gray-400" />
      case 'Read':
        return <CheckCheck className="w-3 h-3 text-blue-500" />
      case 'Failed':
        return <Clock className="w-3 h-3 text-red-500" />
      default:
        return null
    }
  }

  return (
    <div className="flex flex-col h-full bg-gray-100">
      {/* Header */}
      <div className="bg-gray-50 border-b px-4 py-3 flex items-center">
        {onBack && (
          <button onClick={onBack} className="mr-3 lg:hidden">
            <ArrowLeft className="w-5 h-5" />
          </button>
        )}
        <div className="w-10 h-10 rounded-full bg-green-500 flex items-center justify-center text-white font-semibold mr-3">
          {conversation.contactName.charAt(0).toUpperCase()}
        </div>
        <div className="flex-1">
          <h3 className="font-medium text-gray-900">{conversation.contactName}</h3>
          <p className="text-xs text-gray-500">
            {conversation.mode === 'Automatic' ? (
              <span className="flex items-center gap-1">
                <Bot className="w-3 h-3" /> Automático
              </span>
            ) : conversation.mode === 'Human' ? (
              <span className="flex items-center gap-1">
                <User className="w-3 h-3" /> Humano
              </span>
            ) : (
              'Pausado'
            )}
          </p>
        </div>
        <div className="flex items-center gap-2">
          <select
            value={conversation.mode}
            onChange={(e) => modeMutation.mutate(e.target.value)}
            disabled={modeMutation.isPending}
            className="text-xs border rounded px-2 py-1 bg-white"
          >
            <option value="Automatic">🤖 Automático</option>
            <option value="Human">👤 Humano</option>
            <option value="Paused">⏸️ Pausado</option>
          </select>
          <button className="p-2 hover:bg-gray-200 rounded-full">
            <MoreVertical className="w-5 h-5 text-gray-600" />
          </button>
        </div>
      </div>

      {/* Messages */}
      <div className="flex-1 overflow-y-auto p-4 whatsapp-bg scrollbar-thin">
        {isLoading ? (
          <div className="flex items-center justify-center h-full">
            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-green-600" />
          </div>
        ) : messages.length === 0 ? (
          <div className="flex items-center justify-center h-full text-gray-500">
            Nenhuma mensagem
          </div>
        ) : (
          <div className="space-y-2">
            {messages.map((msg) => (
              <div
                key={msg.id}
                className={cn(
                  'flex',
                  msg.direction === 'Outbound' ? 'justify-end' : 'justify-start'
                )}
              >
                <div
                  className={cn(
                    'max-w-[70%] px-3 py-2 shadow-sm',
                    msg.direction === 'Outbound' ? 'message-bubble-out' : 'message-bubble-in'
                  )}
                >
                  {msg.type !== 'Text' && msg.mediaId && (
                    <MediaDisplay
                      messageId={msg.id}
                      type={msg.type}
                      mediaId={msg.mediaId}
                      caption={msg.caption}
                    />
                  )}
                  {msg.content && (
                    <p className="text-sm text-gray-800">{msg.content}</p>
                  )}
                  {msg.caption && msg.type === 'Text' && (
                    <p className="text-sm text-gray-600 mt-1">{msg.caption}</p>
                  )}
                  <div className="flex items-center justify-end gap-1 mt-1">
                    <span className="text-[10px] text-gray-500">
                      {formatTime(msg.createdAt)}
                    </span>
                    {msg.direction === 'Outbound' && getStatusIcon(msg.status)}
                  </div>
                </div>
              </div>
            ))}
            <div ref={messagesEndRef} />
          </div>
        )}
      </div>

      {/* Input */}
      <div className="bg-gray-50 border-t p-3">
        {!conversation.isWindowOpen && (
          <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-2 mb-2">
            <p className="text-xs text-yellow-800">
              Janela de 24h fechada. Apenas templates são permitidos.
            </p>
          </div>
        )}
        <div className="flex items-center gap-2">
          <button className="p-2 hover:bg-gray-200 rounded-full">
            <Smile className="w-5 h-5 text-gray-600" />
          </button>
          <button className="p-2 hover:bg-gray-200 rounded-full">
            <Paperclip className="w-5 h-5 text-gray-600" />
          </button>
          <input
            type="text"
            value={message}
            onChange={(e) => setMessage(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && handleSend()}
            placeholder="Digite uma mensagem..."
            disabled={!conversation.isWindowOpen}
            className="flex-1 px-4 py-2 border rounded-lg focus:outline-none focus:ring-2 focus:ring-green-500 disabled:opacity-50"
          />
          <button
            onClick={handleSend}
            disabled={!message.trim() || !conversation.isWindowOpen}
            className="p-2 bg-green-600 text-white rounded-full hover:bg-green-700 disabled:opacity-50"
          >
            <Send className="w-5 h-5" />
          </button>
        </div>
      </div>
    </div>
  )
}

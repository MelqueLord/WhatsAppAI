import { useEffect, useState, useCallback, useRef } from 'react'
import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr'

interface UseSignalROptions {
  hubUrl: string
  onMessage?: (message: any) => void
  onStatusUpdate?: (update: any) => void
  onConversationUpdate?: (conversation: any) => void
}

export function useSignalR({ hubUrl, onMessage, onStatusUpdate, onConversationUpdate }: UseSignalROptions) {
  const [connection, setConnection] = useState<HubConnection | null>(null)
  const [isConnected, setIsConnected] = useState(false)
  const callbacksRef = useRef({ onMessage, onStatusUpdate, onConversationUpdate })

  useEffect(() => {
    callbacksRef.current = { onMessage, onStatusUpdate, onConversationUpdate }
  }, [onMessage, onStatusUpdate, onConversationUpdate])

  useEffect(() => {
    const newConnection = new HubConnectionBuilder()
      .withUrl(hubUrl, { withCredentials: true })
      .withAutomaticReconnect({ nextRetryDelayInMilliseconds: () => 10000 })
      .configureLogging(LogLevel.Warning)
      .build()

    newConnection.on('NewMessage', (message) => {
      callbacksRef.current.onMessage?.(message)
    })

    newConnection.on('MessageStatusUpdated', (update) => {
      callbacksRef.current.onStatusUpdate?.(update)
    })

    newConnection.on('ConversationUpdated', (conversation) => {
      callbacksRef.current.onConversationUpdate?.(conversation)
    })

    newConnection.onreconnected(() => setIsConnected(true))
    newConnection.onclose(() => setIsConnected(false))
    newConnection.onreconnecting(() => setIsConnected(false))

    setConnection(newConnection)

    return () => {
      newConnection.stop().catch(() => {})
    }
  }, [hubUrl])

  const start = useCallback(async () => {
    if (connection) {
      try {
        await connection.start()
        setIsConnected(true)
      } catch {
        // Hub not available (mock mode or server down)
        setIsConnected(false)
      }
    }
  }, [connection])

  const joinConversation = useCallback(async (conversationId: string) => {
    if (connection && isConnected) {
      try { await connection.invoke('JoinConversation', conversationId) } catch {}
    }
  }, [connection, isConnected])

  const leaveConversation = useCallback(async (conversationId: string) => {
    if (connection && isConnected) {
      try { await connection.invoke('LeaveConversation', conversationId) } catch {}
    }
  }, [connection, isConnected])

  return { connection, isConnected, start, joinConversation, leaveConversation }
}

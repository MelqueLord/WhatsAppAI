import { useEffect, useState, useCallback, useRef } from 'react'
import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr'

interface UseSignalROptions {
  hubUrl: string
  onMessage?: (message: unknown) => void
  onStatusUpdate?: (update: unknown) => void
  onConversationUpdate?: (conversation: unknown) => void
}

export function useSignalR({ hubUrl, onMessage, onStatusUpdate, onConversationUpdate }: UseSignalROptions) {
  const connectionRef = useRef<HubConnection | null>(null)
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

    connectionRef.current = newConnection

    return () => {
      connectionRef.current = null
      newConnection.stop().catch(() => undefined)
    }
  }, [hubUrl])

  const start = useCallback(async () => {
    const connection = connectionRef.current
    if (connection) {
      try {
        await connection.start()
        setIsConnected(true)
      } catch {
        // The hub may be unavailable while the API remains usable.
        setIsConnected(false)
      }
    }
  }, [])

  const joinConversation = useCallback(async (conversationId: string) => {
    const connection = connectionRef.current
    if (connection && isConnected) {
      try {
        await connection.invoke('JoinConversation', conversationId)
      } catch {
        return
      }
    }
  }, [isConnected])

  const leaveConversation = useCallback(async (conversationId: string) => {
    const connection = connectionRef.current
    if (connection && isConnected) {
      try {
        await connection.invoke('LeaveConversation', conversationId)
      } catch {
        return
      }
    }
  }, [isConnected])

  return { isConnected, start, joinConversation, leaveConversation }
}

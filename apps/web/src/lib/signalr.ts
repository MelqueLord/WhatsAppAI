import { useEffect, useState, useCallback, useRef } from 'react'
import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr'
import { getStoredToken } from './api'

interface UseSignalROptions {
  hubUrl: string
  onMessage?: (message: unknown) => void
  onStatusUpdate?: (update: unknown) => void
  onConversationUpdate?: (conversation: unknown) => void
}

export function useSignalR({ hubUrl, onMessage, onStatusUpdate, onConversationUpdate }: UseSignalROptions) {
  const connectionRef = useRef<HubConnection | null>(null)
  // null = still connecting (initial), true = connected, false = lost connection
  const [isConnected, setIsConnected] = useState<boolean | null>(null)
  const callbacksRef = useRef({ onMessage, onStatusUpdate, onConversationUpdate })

  useEffect(() => {
    callbacksRef.current = { onMessage, onStatusUpdate, onConversationUpdate }
  }, [onMessage, onStatusUpdate, onConversationUpdate])

  useEffect(() => {
    const newConnection = new HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => getStoredToken() ?? '',
      })
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
        // Hub unavailable — mark as disconnected (not just unknown)
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

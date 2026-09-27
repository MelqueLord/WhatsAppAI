import { friendlyErrorMessage } from './errors'

export type ErrorNotice = {
  id: number
  message: string
}

const listeners = new Set<(notice: ErrorNotice) => void>()
let nextNoticeId = 1
let latestMessage = ''
let latestMessageAt = 0

export function notifyError(error: unknown): void {
  const message = friendlyErrorMessage(error)
  const now = Date.now()

  if (message === latestMessage && now - latestMessageAt < 1_500) return

  latestMessage = message
  latestMessageAt = now
  const notice = { id: nextNoticeId++, message }
  listeners.forEach((listener) => listener(notice))
}

export function subscribeToErrorNotifications(listener: (notice: ErrorNotice) => void): () => void {
  listeners.add(listener)
  return () => listeners.delete(listener)
}

import { AlertCircle, X } from 'lucide-react'
import { useEffect, useState, type ReactNode } from 'react'
import { notifyError, subscribeToErrorNotifications, type ErrorNotice } from '../lib/errorNotifications'

export function ErrorNotifications({ children }: { children: ReactNode }) {
  const [notices, setNotices] = useState<ErrorNotice[]>([])

  useEffect(() => {
    const listener = (notice: ErrorNotice) => {
      setNotices((current) => [...current.slice(-2), notice])
      window.setTimeout(() => {
        setNotices((current) => current.filter((item) => item.id !== notice.id))
      }, 6_000)
    }

    return subscribeToErrorNotifications(listener)
  }, [])

  useEffect(() => {
    const handleUnhandledError = (event: ErrorEvent) => notifyError(event.error)
    const handleUnhandledRejection = (event: PromiseRejectionEvent) => notifyError(event.reason)

    window.addEventListener('error', handleUnhandledError)
    window.addEventListener('unhandledrejection', handleUnhandledRejection)
    return () => {
      window.removeEventListener('error', handleUnhandledError)
      window.removeEventListener('unhandledrejection', handleUnhandledRejection)
    }
  }, [])

  return (
    <>
      {children}
      <div className="pointer-events-none fixed inset-x-4 bottom-4 z-[100] flex flex-col items-end gap-3 sm:left-auto sm:w-96" aria-live="assertive">
        {notices.map((notice) => (
          <div key={notice.id} role="alert" className="pointer-events-auto flex w-full items-start gap-3 rounded-xl border border-red-200 bg-white p-4 text-sm text-slate-700 shadow-xl">
            <AlertCircle className="mt-0.5 h-5 w-5 shrink-0 text-red-500" aria-hidden="true" />
            <p className="flex-1 leading-5">{notice.message}</p>
            <button type="button" onClick={() => setNotices((current) => current.filter((item) => item.id !== notice.id))} className="rounded p-0.5 text-slate-400 hover:bg-slate-100 hover:text-slate-700" aria-label="Fechar aviso">
              <X className="h-4 w-4" aria-hidden="true" />
            </button>
          </div>
        ))}
      </div>
    </>
  )
}

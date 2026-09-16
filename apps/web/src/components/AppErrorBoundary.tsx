import { Component, type ErrorInfo, type ReactNode } from 'react'
import { AlertTriangle, RefreshCw } from 'lucide-react'

type Props = { children: ReactNode }
type State = { hasError: boolean }

export class AppErrorBoundary extends Component<Props, State> {
  state: State = { hasError: false }

  static getDerivedStateFromError(): State {
    return { hasError: true }
  }

  componentDidCatch(error: Error, errorInfo: ErrorInfo): void {
    void error
    void errorInfo
  }

  render() {
    if (this.state.hasError) {
      return (
        <main className="flex min-h-screen items-center justify-center bg-slate-50 p-6 text-center">
          <section className="max-w-md rounded-2xl border border-slate-200 bg-white p-8 shadow-sm">
            <AlertTriangle className="mx-auto h-10 w-10 text-amber-500" aria-hidden="true" />
            <h1 className="mt-4 text-xl font-bold text-slate-900">Não foi possível abrir esta tela</h1>
            <p className="mt-2 text-sm leading-6 text-slate-600">Atualize a página para tentar novamente. Se o problema continuar, fale com o suporte e informe o horário em que ele aconteceu.</p>
            <button type="button" onClick={() => window.location.reload()} className="mx-auto mt-6 inline-flex items-center gap-2 rounded-lg bg-emerald-600 px-4 py-2.5 text-sm font-medium text-white hover:bg-emerald-700">
              <RefreshCw className="h-4 w-4" aria-hidden="true" />
              Atualizar página
            </button>
          </section>
        </main>
      )
    }

    return this.props.children
  }
}

import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '../../../lib/api'
import { Bot, Save, Loader2, CheckCircle2, XCircle, Zap } from 'lucide-react'

export function AiConfigPage() {
  const queryClient = useQueryClient()
  const [modelId, setModelId] = useState('')
  const [apiKey, setApiKey] = useState('')
  const [success, setSuccess] = useState(false)
  const [testResult, setTestResult] = useState<{ success: boolean; error?: string } | null>(null)

  const { data: config, isLoading } = useQuery({
    queryKey: ['ai-config'],
    queryFn: async () => {
      const res = await fetch('/api/integrations/ai', { credentials: 'include' })
      return res.json()
    },
  })

  const saveMutation = useMutation({
    mutationFn: async () => {
      const res = await fetch('/api/integrations/ai', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({ modelId, apiKey }),
      })
      if (!res.ok) throw new Error('Erro ao salvar')
      return res.json()
    },
    onSuccess: () => {
      setSuccess(true)
      setApiKey('')
      queryClient.invalidateQueries({ queryKey: ['ai-config'] })
      setTimeout(() => setSuccess(false), 3000)
    },
  })

  const testMutation = useMutation({
    mutationFn: async () => {
      const res = await fetch('/api/integrations/ai/test-connection', {
        method: 'POST',
        credentials: 'include',
      })
      return res.json()
    },
    onSuccess: (data) => setTestResult(data),
  })

  if (isLoading) {
    return (
      <div className="h-full flex items-center justify-center">
        <Loader2 className="w-8 h-8 text-emerald-500 animate-spin" />
      </div>
    )
  }

  return (
    <div className="h-full overflow-y-auto">
      <div className="max-w-2xl mx-auto px-6 py-8">
        <div className="flex items-center gap-3 mb-8">
          <div className="w-10 h-10 rounded-lg bg-violet-50 text-violet-600 flex items-center justify-center">
            <Bot className="w-5 h-5" />
          </div>
          <div>
            <h1 className="text-xl font-bold text-slate-900">Configuração de IA</h1>
            <p className="text-sm text-slate-500">Configure o provedor de IA para respostas automáticas</p>
          </div>
        </div>

        <div className="bg-white rounded-xl border border-slate-200 p-6">
          {config?.configured && (
            <div className="mb-6 p-4 bg-emerald-50 border border-emerald-200 rounded-lg">
              <div className="flex items-center gap-2 text-emerald-700">
                <CheckCircle2 className="w-4 h-4" />
                <span className="text-sm font-medium">IA configurada</span>
              </div>
              <p className="text-sm text-emerald-600 mt-1">
                Modelo: {config.modelId} | Status: {config.isActive ? 'Ativo' : 'Inativo'}
              </p>
            </div>
          )}

          <div className="space-y-4">
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1">Modelo</label>
              <input
                type="text"
                value={modelId}
                onChange={(e) => setModelId(e.target.value)}
                placeholder={config?.modelId || 'gpt-4o-mini'}
                className="w-full px-4 py-2.5 border border-slate-300 rounded-lg focus:ring-2 focus:ring-emerald-500 focus:border-transparent"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1">API Key</label>
              <input
                type="password"
                value={apiKey}
                onChange={(e) => setApiKey(e.target.value)}
                placeholder="sk-..."
                className="w-full px-4 py-2.5 border border-slate-300 rounded-lg focus:ring-2 focus:ring-emerald-500 focus:border-transparent"
              />
              <p className="text-xs text-slate-500 mt-1">
                A chave é criptografada e nunca é exibida novamente.
              </p>
            </div>

            <div className="flex gap-3 pt-2">
              <button
                onClick={() => saveMutation.mutate()}
                disabled={saveMutation.isPending || (!modelId && !apiKey)}
                className="flex items-center gap-2 px-5 py-2.5 bg-emerald-500 hover:bg-emerald-600 text-white rounded-lg font-medium transition-colors disabled:opacity-50"
              >
                {saveMutation.isPending ? (
                  <Loader2 className="w-4 h-4 animate-spin" />
                ) : (
                  <Save className="w-4 h-4" />
                )}
                Salvar
              </button>

              <button
                onClick={() => testMutation.mutate()}
                disabled={testMutation.isPending || !config?.configured}
                className="flex items-center gap-2 px-5 py-2.5 bg-slate-100 hover:bg-slate-200 text-slate-700 rounded-lg font-medium transition-colors disabled:opacity-50"
              >
                {testMutation.isPending ? (
                  <Loader2 className="w-4 h-4 animate-spin" />
                ) : (
                  <Zap className="w-4 h-4" />
                )}
                Testar Conexão
              </button>
            </div>

            {success && (
              <div className="p-3 bg-emerald-50 border border-emerald-200 rounded-lg text-emerald-700 text-sm">
                Configuração salva com sucesso!
              </div>
            )}

            {testResult && (
              <div
                className={`p-3 border rounded-lg text-sm ${
                  testResult.success
                    ? 'bg-emerald-50 border-emerald-200 text-emerald-700'
                    : 'bg-red-50 border-red-200 text-red-700'
                }`}
              >
                {testResult.success ? (
                  <span className="flex items-center gap-2">
                    <CheckCircle2 className="w-4 h-4" /> Conexão OK
                  </span>
                ) : (
                  <span className="flex items-center gap-2">
                    <XCircle className="w-4 h-4" /> {testResult.error || 'Falha na conexão'}
                  </span>
                )}
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}

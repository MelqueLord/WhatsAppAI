import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../../../lib/auth'
import { Bot, Save, Loader2, CheckCircle2, XCircle, Zap, Lock, Power } from 'lucide-react'

interface ProviderInfo {
  id: string
  name: string
  models: { id: string; name: string }[]
}

interface AiConfigResponse {
  configured: boolean
  provider?: string
  modelId?: string
  aiActive?: boolean
}

const providerColors: Record<string, string> = {
  openai: 'bg-green-100 text-green-800 border-green-300',
  gemini: 'bg-blue-100 text-blue-800 border-blue-300',
  anthropic: 'bg-orange-100 text-orange-800 border-orange-300',
  xiaomi: 'bg-purple-100 text-purple-800 border-purple-300',
  grok: 'bg-slate-200 text-slate-800 border-slate-400',
  groq: 'bg-cyan-100 text-cyan-800 border-cyan-300',
}

export function AiConfigPage() {
  const queryClient = useQueryClient()
  const { user } = useAuth()
  const aiEnabled = user?.aiEnabled === true

  const [selectedProvider, setSelectedProvider] = useState<string | null>(null)
  const [selectedModel, setSelectedModel] = useState<string | null>(null)
  const [apiKey, setApiKey] = useState('')
  const [success, setSuccess] = useState(false)
  const [testResult, setTestResult] = useState<{ success: boolean; error?: string } | null>(null)

  const { data: providers = [] } = useQuery({
    queryKey: ['ai-providers'],
    queryFn: async () => {
      const response = await fetch('/api/integrations/ai/providers', { credentials: 'include' })
      return response.json() as Promise<ProviderInfo[]>
    },
    enabled: aiEnabled,
  })

  const { data: config, isLoading } = useQuery({
    queryKey: ['ai-config'],
    queryFn: async () => {
      const response = await fetch('/api/integrations/ai', { credentials: 'include' })
      return response.json() as Promise<AiConfigResponse>
    },
    enabled: aiEnabled,
  })

  const provider = selectedProvider ?? config?.provider ?? providers[0]?.id ?? 'openai'
  const modelId = selectedModel ?? config?.modelId ?? ''
  const models = providers.find((item) => item.id === provider)?.models ?? []
  const isAiActive = config?.aiActive === true

  const saveMutation = useMutation({
    mutationFn: async () => {
      const res = await fetch('/api/integrations/ai', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({ provider, modelId, apiKey: apiKey || undefined }),
      })

      if (!res.ok) {
        const body = (await res.json().catch(() => null)) as { error?: string } | null
        throw new Error(body?.error || 'Erro ao salvar configuração de IA')
      }

      return res.json()
    },
    onSuccess: () => {
      setSuccess(true)
      setApiKey('')
      queryClient.invalidateQueries({ queryKey: ['ai-config'] })
      setTimeout(() => setSuccess(false), 3000)
    },
  })

  const toggleMutation = useMutation({
    mutationFn: async (enabled: boolean) => {
      const res = await fetch('/api/integrations/ai/toggle', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({ enabled }),
      })

      if (!res.ok) {
        const body = (await res.json().catch(() => null)) as { error?: string } | null
        throw new Error(body?.error || 'Erro ao alterar IA')
      }

      return res.json()
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['ai-config'] }),
  })

  const testMutation = useMutation({
    mutationFn: async () => {
      const res = await fetch('/api/integrations/ai/test-connection', {
        method: 'POST',
        credentials: 'include',
      })
      return (await res.json()) as { success: boolean; error?: string }
    },
    onSuccess: (data) => setTestResult(data),
  })

  if (!aiEnabled) {
    return (
      <div className="h-full flex items-center justify-center">
        <div className="text-center">
          <Lock className="w-12 h-12 text-slate-300 mx-auto mb-4" />
          <h2 className="text-lg font-semibold text-slate-700">IA não disponível</h2>
          <p className="text-sm text-slate-500 mt-1">Seu plano não inclui IA.</p>
        </div>
      </div>
    )
  }

  if (isLoading) {
    return (
      <div className="h-full flex items-center justify-center">
        <Loader2 className="w-8 h-8 text-emerald-500 animate-spin" />
      </div>
    )
  }

  return (
    <div className="h-full overflow-y-auto">
      <div className="max-w-3xl mx-auto px-6 py-8">
        <div className="flex items-center justify-between mb-8">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-lg bg-violet-50 text-violet-600 flex items-center justify-center">
              <Bot className="w-5 h-5" />
            </div>
            <div>
              <h1 className="text-xl font-bold text-slate-900">Atendimento com IA</h1>
              <p className="text-sm text-slate-500">Provedor e modelo para respostas por IA</p>
            </div>
          </div>

          <button
            onClick={() => toggleMutation.mutate(!isAiActive)}
            disabled={toggleMutation.isPending || !config?.configured}
            className={`flex items-center gap-2 px-4 py-2 rounded-lg font-medium text-sm disabled:opacity-50 ${
              isAiActive ? 'bg-violet-100 text-violet-700' : 'bg-slate-100 text-slate-700'
            }`}
          >
            {toggleMutation.isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Power className="w-4 h-4" />}
            {isAiActive ? 'IA ativa' : 'Ativar IA'}
          </button>
        </div>

        <div className="bg-white rounded-xl border border-slate-200 p-6">
          <h2 className="font-semibold text-slate-900 mb-4">Provedor de IA</h2>

          <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 mb-4">
            {providers.map((item) => (
              <button
                key={item.id}
                onClick={() => {
                  setSelectedProvider(item.id)
                  setSelectedModel('')
                }}
                className={`px-4 py-3 rounded-lg border-2 font-medium text-sm ${
                  provider === item.id
                    ? `${providerColors[item.id] ?? 'bg-slate-100 text-slate-700 border-slate-300'} border-current ring-2 ring-offset-1`
                    : 'border-slate-200 text-slate-600 hover:border-slate-300'
                }`}
              >
                {item.name}
              </button>
            ))}
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1">Modelo</label>
              <select
                value={modelId}
                onChange={(event) => setSelectedModel(event.target.value)}
                className="w-full px-4 py-2.5 border border-slate-300 rounded-lg"
              >
                <option value="">Selecione...</option>
                {models.map((item) => (
                  <option key={item.id} value={item.id}>
                    {item.name}
                  </option>
                ))}
              </select>
            </div>

            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1">API Key</label>
              <input
                type="password"
                value={apiKey}
                onChange={(event) => setApiKey(event.target.value)}
                placeholder={config?.configured ? '••••••••' : 'Cole sua chave...'}
                className="w-full px-4 py-2.5 border border-slate-300 rounded-lg"
              />
              <p className="text-xs text-slate-500 mt-1">Criptografada e nunca exibida novamente.</p>
            </div>
          </div>

          <div className="flex gap-3 mt-4">
            <button
              onClick={() => saveMutation.mutate()}
              disabled={saveMutation.isPending || !modelId}
              className="flex items-center gap-2 px-5 py-2.5 bg-emerald-500 hover:bg-emerald-600 text-white rounded-lg font-medium disabled:opacity-50"
            >
              {saveMutation.isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
              Salvar
            </button>

            <button
              onClick={() => testMutation.mutate()}
              disabled={testMutation.isPending || !config?.configured}
              className="flex items-center gap-2 px-5 py-2.5 bg-slate-100 hover:bg-slate-200 text-slate-700 rounded-lg font-medium disabled:opacity-50"
            >
              {testMutation.isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Zap className="w-4 h-4" />}
              Testar conexão
            </button>
          </div>

          {success && (
            <p className="mt-4 text-sm text-emerald-600 flex items-center gap-1">
              <CheckCircle2 className="w-4 h-4" /> Configuração salva com sucesso
            </p>
          )}
          {testResult && (
            <p className={`mt-2 text-sm flex items-center gap-1 ${testResult.success ? 'text-emerald-600' : 'text-red-600'}`}>
              {testResult.success ? <CheckCircle2 className="w-4 h-4" /> : <XCircle className="w-4 h-4" />}
              {testResult.success ? 'Conexão validada.' : testResult.error || 'Falha na conexão'}
            </p>
          )}
        </div>
      </div>
    </div>
  )
}

import { useEffect, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../../../lib/auth'
import { Bot, Save, Loader2, CheckCircle2, XCircle, Zap, Lock } from 'lucide-react'

interface ProviderInfo { id: string; name: string; models: { id: string; name: string }[] }
interface BotConfig { mode: string; welcomeMessage: string | null; offlineMessage: string | null; fallbackMessage: string | null; maxTokensPerResponse: number; enabled: boolean }

const PROVIDER_COLORS: Record<string, string> = {
  openai: 'bg-green-100 text-green-800 border-green-300',
  gemini: 'bg-blue-100 text-blue-800 border-blue-300',
  anthropic: 'bg-orange-100 text-orange-800 border-orange-300',
  xiaomi: 'bg-purple-100 text-purple-800 border-purple-300',
}

export function AiConfigPage() {
  const queryClient = useQueryClient()
  const { user } = useAuth()
  const aiEnabled = user?.aiEnabled === true

  const [provider, setProvider] = useState('openai')
  const [modelId, setModelId] = useState('')
  const [apiKey, setApiKey] = useState('')
  const [mode, setMode] = useState('Manual')
  const [welcomeMessage, setWelcomeMessage] = useState('')
  const [offlineMessage, setOfflineMessage] = useState('')
  const [fallbackMessage, setFallbackMessage] = useState('')
  const [maxTokens, setMaxTokens] = useState(500)
  const [success, setSuccess] = useState(false)
  const [testResult, setTestResult] = useState<{ success: boolean; error?: string } | null>(null)

  const { data: providers } = useQuery({
    queryKey: ['ai-providers'],
    queryFn: async () => {
      const res = await fetch('/api/integrations/ai/providers', { credentials: 'include' })
      return res.json() as Promise<ProviderInfo[]>
    },
    enabled: aiEnabled,
  })

  const { data: config, isLoading } = useQuery({
    queryKey: ['ai-config'],
    queryFn: async () => {
      const res = await fetch('/api/integrations/ai', { credentials: 'include' })
      return res.json()
    },
    enabled: aiEnabled,
  })

  useEffect(() => {
    if (!config) return
    if (config.provider) setProvider(config.provider)
    if (config.modelId) setModelId(config.modelId)
    if (config.botConfig) {
      const bc = config.botConfig as BotConfig
      setMode(bc.mode || 'Manual')
      setWelcomeMessage(bc.welcomeMessage || '')
      setOfflineMessage(bc.offlineMessage || '')
      setFallbackMessage(bc.fallbackMessage || '')
      setMaxTokens(bc.maxTokensPerResponse || 500)
    }
  }, [config])

  const currentModels = providers?.find(p => p.id === provider)?.models ?? []

  const saveMutation = useMutation({
    mutationFn: async () => {
      const res = await fetch('/api/integrations/ai', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({
          provider, modelId, apiKey: apiKey || undefined,
          botConfig: { mode, welcomeMessage, offlineMessage, fallbackMessage, maxTokensPerResponse: maxTokens },
        }),
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
      const res = await fetch('/api/integrations/ai/test-connection', { method: 'POST', credentials: 'include' })
      return res.json()
    },
    onSuccess: (data) => setTestResult(data),
  })

  if (!aiEnabled) {
    return (
      <div className="h-full flex items-center justify-center">
        <div className="text-center">
          <Lock className="w-12 h-12 text-slate-300 mx-auto mb-4" />
          <h2 className="text-lg font-semibold text-slate-700">IA não disponível</h2>
          <p className="text-sm text-slate-500 mt-1">Seu plano (BOT) não inclui funcionalidades de IA.</p>
        </div>
      </div>
    )
  }

  if (isLoading) {
    return <div className="h-full flex items-center justify-center"><Loader2 className="w-8 h-8 text-emerald-500 animate-spin" /></div>
  }

  return (
    <div className="h-full overflow-y-auto">
      <div className="max-w-3xl mx-auto px-6 py-8">
        <div className="flex items-center gap-3 mb-8">
          <div className="w-10 h-10 rounded-lg bg-violet-50 text-violet-600 flex items-center justify-center">
            <Bot className="w-5 h-5" />
          </div>
          <div>
            <h1 className="text-xl font-bold text-slate-900">Atendimento com IA</h1>
            <p className="text-sm text-slate-500">Provedor, modelo, modo e mensagens automáticas</p>
          </div>
        </div>

        {/* Provider selector */}
        <div className="bg-white rounded-xl border border-slate-200 p-6 mb-6">
          <h2 className="font-semibold text-slate-900 mb-4">Provedor de IA</h2>
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 mb-4">
            {(providers ?? []).map(p => (
              <button key={p.id} onClick={() => { setProvider(p.id); setModelId('') }}
                className={`px-4 py-3 rounded-lg border-2 font-medium text-sm transition-all ${provider === p.id ? PROVIDER_COLORS[p.id] + ' border-current ring-2 ring-offset-1' : 'border-slate-200 text-slate-600 hover:border-slate-300'}`}>
                {p.name}
              </button>
            ))}
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1">Modelo</label>
              <select value={modelId} onChange={e => setModelId(e.target.value)}
                className="w-full px-4 py-2.5 border border-slate-300 rounded-lg">
                <option value="">Selecione...</option>
                {currentModels.map(m => <option key={m.id} value={m.id}>{m.name}</option>)}
              </select>
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1">API Key</label>
              <input type="password" value={apiKey} onChange={e => setApiKey(e.target.value)}
                placeholder={config?.configured ? '••••••••' : 'Cole sua chave...'}
                className="w-full px-4 py-2.5 border border-slate-300 rounded-lg" />
              <p className="text-xs text-slate-500 mt-1">Criptografada e nunca exibida novamente.</p>
            </div>
          </div>
          <div className="flex gap-3 mt-4">
            <button onClick={() => saveMutation.mutate()} disabled={saveMutation.isPending || !modelId}
              className="flex items-center gap-2 px-5 py-2.5 bg-emerald-500 hover:bg-emerald-600 text-white rounded-lg font-medium disabled:opacity-50">
              {saveMutation.isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
              Salvar
            </button>
            <button onClick={() => testMutation.mutate()} disabled={testMutation.isPending || !config?.configured}
              className="flex items-center gap-2 px-5 py-2.5 bg-slate-100 hover:bg-slate-200 text-slate-700 rounded-lg font-medium disabled:opacity-50">
              {testMutation.isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Zap className="w-4 h-4" />}
              Testar Conexão
            </button>
          </div>
          {success && <div className="mt-3 p-3 bg-emerald-50 border border-emerald-200 rounded-lg text-emerald-700 text-sm">Configuração salva!</div>}
          {testResult && (
            <div className={`mt-3 p-3 border rounded-lg text-sm ${testResult.success ? 'bg-emerald-50 border-emerald-200 text-emerald-700' : 'bg-red-50 border-red-200 text-red-700'}`}>
              {testResult.success ? <span className="flex items-center gap-2"><CheckCircle2 className="w-4 h-4" /> Conexão OK</span>
                : <span className="flex items-center gap-2"><XCircle className="w-4 h-4" /> {testResult.error || 'Falha'}</span>}
            </div>
          )}
        </div>

        {/* Mode */}
        <div className="bg-white rounded-xl border border-slate-200 p-6 mb-6">
          <h2 className="font-semibold text-slate-900 mb-4">Modo de operação</h2>
          <div className="flex gap-3">
            {['Manual', 'SimpleAutoReply', 'AiPowered'].map(m => (
              <button key={m} onClick={() => setMode(m)}
                className={`px-4 py-2 rounded-lg font-medium text-sm ${mode === m ? 'bg-emerald-500 text-white' : 'bg-slate-100 text-slate-700 hover:bg-slate-200'}`}>
                {m === 'Manual' ? 'Manual' : m === 'SimpleAutoReply' ? 'Auto-reply' : 'IA'}
              </button>
            ))}
          </div>
        </div>

        {/* Messages */}
        <div className="bg-white rounded-xl border border-slate-200 p-6 mb-6">
          <h2 className="font-semibold text-slate-900 mb-4">Mensagens automáticas</h2>
          <div className="space-y-4">
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1">Boas-vindas</label>
              <textarea value={welcomeMessage} onChange={e => setWelcomeMessage(e.target.value)} rows={2}
                placeholder="Olá! Como posso ajudar?" className="w-full px-4 py-2.5 border border-slate-300 rounded-lg resize-none" />
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1">Fallback</label>
              <textarea value={fallbackMessage} onChange={e => setFallbackMessage(e.target.value)} rows={2}
                placeholder="Não entendi. Digite atendente." className="w-full px-4 py-2.5 border border-slate-300 rounded-lg resize-none" />
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1">Quando não há atendente</label>
              <textarea value={offlineMessage} onChange={e => setOfflineMessage(e.target.value)} rows={2}
                placeholder="No momento não há atendentes disponíveis." className="w-full px-4 py-2.5 border border-slate-300 rounded-lg resize-none" />
            </div>
          </div>
        </div>

        {/* Limits */}
        <div className="bg-white rounded-xl border border-slate-200 p-6 mb-6">
          <h2 className="font-semibold text-slate-900 mb-4">Limites</h2>
          <div>
            <label className="block text-sm font-medium text-slate-700 mb-1">Max tokens por resposta</label>
            <input type="number" value={maxTokens} onChange={e => setMaxTokens(Number(e.target.value))}
              min={50} max={2000} className="w-40 px-4 py-2.5 border border-slate-300 rounded-lg" />
          </div>
        </div>

        <button onClick={() => saveMutation.mutate()} disabled={saveMutation.isPending}
          className="flex items-center gap-2 px-6 py-3 bg-emerald-500 hover:bg-emerald-600 text-white rounded-lg font-medium disabled:opacity-50">
          {saveMutation.isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
          Salvar tudo
        </button>
      </div>
    </div>
  )
}

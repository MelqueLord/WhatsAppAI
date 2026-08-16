import { useState, useEffect } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Bot, Save, Loader2, CheckCircle2, MessageSquare, Zap, Hand, Settings, Lock } from 'lucide-react'
import { useAuth } from '../../lib/auth'

interface BotConfig {
  configured: boolean
  mode: string
  welcomeMessage: string | null
  offlineMessage: string | null
  fallbackMessage: string | null
  maxTokensPerResponse: number
  enabled: boolean
  version: number
}

export function BotConfigPage() {
  const queryClient = useQueryClient()
  const { user } = useAuth()
  const aiEnabled = user?.aiEnabled === true
  const [mode, setMode] = useState('Manual')
  const [welcomeMessage, setWelcomeMessage] = useState('')
  const [offlineMessage, setOfflineMessage] = useState('')
  const [fallbackMessage, setFallbackMessage] = useState('')
  const [maxTokens, setMaxTokens] = useState(500)
  const [success, setSuccess] = useState(false)

  const { data: config, isLoading } = useQuery({
    queryKey: ['bot-config'],
    queryFn: async () => {
      const res = await fetch('/api/bot-config', { credentials: 'include' })
      return res.json() as Promise<BotConfig>
    },
  })

  useEffect(() => {
    if (config) {
      setMode(config.mode)
      setWelcomeMessage(config.welcomeMessage || '')
      setOfflineMessage(config.offlineMessage || '')
      setFallbackMessage(config.fallbackMessage || '')
      setMaxTokens(config.maxTokensPerResponse)
    }
  }, [config])

  const saveMutation = useMutation({
    mutationFn: async () => {
      const res = await fetch('/api/bot-config', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({
          mode,
          welcomeMessage,
          offlineMessage,
          fallbackMessage,
          maxTokensPerResponse: maxTokens,
        }),
      })
      if (!res.ok) throw new Error('Erro ao salvar')
      return res.json()
    },
    onSuccess: () => {
      setSuccess(true)
      queryClient.invalidateQueries({ queryKey: ['bot-config'] })
      setTimeout(() => setSuccess(false), 3000)
    },
  })

  const toggleMutation = useMutation({
    mutationFn: async (enabled: boolean) => {
      const res = await fetch('/api/bot-config/toggle', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({ enabled }),
      })
      if (!res.ok) throw new Error('Erro')
      return res.json()
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['bot-config'] }),
  })

  if (isLoading) {
    return (
      <div className="h-full flex items-center justify-center">
        <Loader2 className="w-8 h-8 text-emerald-500 animate-spin" />
      </div>
    )
  }

  const modes = [
    { value: 'Manual', label: 'Manual', icon: Hand, description: 'Sem automação. Operadores respondem tudo.' },
    { value: 'SimpleAutoReply', label: 'Resposta Automática', icon: MessageSquare, description: 'Respostas pré-definidas sem IA. Mínimo de tokens.' },
    { value: 'AiPowered', label: 'IA Completa', icon: Zap, description: 'IA responde automaticamente com base no conhecimento.', requiresPlan: true },
  ].filter(m => !m.requiresPlan || aiEnabled)

  return (
    <div className="h-full overflow-y-auto">
      <div className="max-w-3xl mx-auto px-6 py-8">
        <div className="flex items-center justify-between mb-8">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-lg bg-indigo-50 text-indigo-600 flex items-center justify-center">
              <Bot className="w-5 h-5" />
            </div>
            <div>
              <h1 className="text-xl font-bold text-slate-900">Configuração do Bot</h1>
              <p className="text-sm text-slate-500">Configure como o bot responde aos clientes</p>
            </div>
          </div>
          <button
            onClick={() => toggleMutation.mutate(!config?.enabled)}
            className={`px-4 py-2 rounded-lg font-medium text-sm transition-colors ${
              config?.enabled
                ? 'bg-emerald-100 text-emerald-700 hover:bg-emerald-200'
                : 'bg-slate-100 text-slate-700 hover:bg-slate-200'
            }`}
          >
            {config?.enabled ? 'Ativo' : 'Inativo'}
          </button>
        </div>

        {/* Mode Selection */}
        <div className="bg-white rounded-xl border border-slate-200 p-6 mb-6">
          <h2 className="font-semibold text-slate-900 mb-4 flex items-center gap-2">
            <Settings className="w-4 h-4" /> Modo de Operação
          </h2>
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
            {modes.map((m) => (
              <button
                key={m.value}
                onClick={() => setMode(m.value)}
                className={`p-4 rounded-xl border-2 text-left transition-all ${
                  mode === m.value
                    ? 'border-emerald-500 bg-emerald-50'
                    : 'border-slate-200 hover:border-slate-300'
                }`}
              >
                <div className="flex items-center gap-2 mb-2">
                  <m.icon className={`w-5 h-5 ${mode === m.value ? 'text-emerald-600' : 'text-slate-400'}`} />
                  <span className={`font-medium text-sm ${mode === m.value ? 'text-emerald-700' : 'text-slate-700'}`}>
                    {m.label}
                  </span>
                </div>
                <p className="text-xs text-slate-500">{m.description}</p>
              </button>
            ))}
          </div>
        </div>

        {/* Token Limit (for AI mode) */}
        {mode === 'AiPowered' && (
          <div className="bg-white rounded-xl border border-slate-200 p-6 mb-6">
            <h2 className="font-semibold text-slate-900 mb-4">Limite de Tokens</h2>
            <p className="text-sm text-slate-500 mb-4">
              Controle o custo limitando tokens por resposta. Menos tokens = respostas mais curtas e econômicas.
            </p>
            <div className="flex items-center gap-4">
              <input
                type="range"
                min="50"
                max="2000"
                step="50"
                value={maxTokens}
                onChange={(e) => setMaxTokens(Number(e.target.value))}
                className="flex-1"
              />
              <div className="w-24 text-center">
                <span className="text-2xl font-bold text-slate-900">{maxTokens}</span>
                <p className="text-xs text-slate-500">tokens</p>
              </div>
            </div>
            <div className="flex justify-between mt-2 text-xs text-slate-400">
              <span>Econômico (50)</span>
              <span>Padrão (500)</span>
              <span>Completo (2000)</span>
            </div>
          </div>
        )}

        {/* Messages */}
        <div className="bg-white rounded-xl border border-slate-200 p-6 mb-6">
          <h2 className="font-semibold text-slate-900 mb-4">Mensagens Automáticas</h2>
          <div className="space-y-4">
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1">Mensagem de Boas-vindas</label>
              <textarea
                value={welcomeMessage}
                onChange={(e) => setWelcomeMessage(e.target.value)}
                rows={2}
                placeholder="Olá! Bem-vindo à nossa empresa. Como posso ajudar?"
                className="w-full px-4 py-2.5 border border-slate-300 rounded-lg focus:ring-2 focus:ring-emerald-500 focus:border-transparent resize-none"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1">Mensagem Fora do Horário</label>
              <textarea
                value={offlineMessage}
                onChange={(e) => setOfflineMessage(e.target.value)}
                rows={2}
                placeholder="No momento estamos fora do horário de atendimento..."
                className="w-full px-4 py-2.5 border border-slate-300 rounded-lg focus:ring-2 focus:ring-emerald-500 focus:border-transparent resize-none"
              />
            </div>
            {mode !== 'Manual' && (
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1">Mensagem de Fallback</label>
                <textarea
                  value={fallbackMessage}
                  onChange={(e) => setFallbackMessage(e.target.value)}
                  rows={2}
                  placeholder="Desculpe, não entendi. Pode reformular?"
                  className="w-full px-4 py-2.5 border border-slate-300 rounded-lg focus:ring-2 focus:ring-emerald-500 focus:border-transparent resize-none"
                />
              </div>
            )}
          </div>
        </div>

        {/* Save Button */}
        <div className="flex items-center gap-3">
          <button
            onClick={() => saveMutation.mutate()}
            disabled={saveMutation.isPending}
            className="flex items-center gap-2 px-6 py-3 bg-emerald-500 hover:bg-emerald-600 text-white rounded-lg font-medium transition-colors disabled:opacity-50"
          >
            {saveMutation.isPending ? (
              <Loader2 className="w-4 h-4 animate-spin" />
            ) : (
              <Save className="w-4 h-4" />
            )}
            Salvar Configuração
          </button>
          {success && (
            <span className="flex items-center gap-1 text-emerald-600 text-sm">
              <CheckCircle2 className="w-4 h-4" /> Salvo com sucesso!
            </span>
          )}
        </div>
      </div>
    </div>
  )
}

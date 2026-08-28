import { fetchWithCsrf } from '../../../lib/api'
import { useEffect, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../../../lib/auth'
import { Section, MessageField } from '../../bot/BotConfigPage'
import {
  ArrowRightLeft,
  Bot,
  CheckCircle2,
  GitBranch,
  HelpCircle,
  Image,
  Info,
  Loader2,
  Lock,
  MessageSquare,
  Power,
  Save,
  UserCheck,
  WifiOff,
  XCircle,
  Zap,
} from 'lucide-react'

interface ProviderInfo {
  id: string
  name: string
  models: { id: string; name: string }[]
}

interface GuidelineRule {
  code: string
  description: string
}

interface AiConfigResponse {
  configured: boolean
  provider?: string
  modelId?: string
  systemPrompt?: string | null
  maxTokensPerResponse?: number
  confidenceThreshold?: number
  routingQueueIds?: string[]
  routingTagIds?: string[]
  guidelines?: {
    behavior?: GuidelineRule[]
    security?: GuidelineRule[]
    handoff?: GuidelineRule[]
  }
  aiActive?: boolean
  version?: number
}

interface BotConfigResponse {
  configured: boolean
  mode: string
  welcomeMessage?: string | null
  returningMessage?: string | null
  offlineMessage?: string | null
  fallbackMessage?: string | null
  handoffMessage?: string | null
  queueTransferMessage?: string | null
  mediaMessage?: string | null
  maxTokensPerResponse?: number
  enabled?: boolean
  version?: number
  flowSteps?: unknown[]
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
  const [systemPrompt, setSystemPrompt] = useState('')
  const [businessType, setBusinessType] = useState('')
  const [maxTokens, setMaxTokens] = useState(180)
  const [confidenceThreshold, setConfidenceThreshold] = useState(0.5)
  const [routingQueueIds, setRoutingQueueIds] = useState<string[]>([])
  const [routingTagIds, setRoutingTagIds] = useState<string[]>([])
  const [loadedVersion, setLoadedVersion] = useState<number | null>(null)
  const [loadedBotVersion, setLoadedBotVersion] = useState<number | null>(null)
  const [mode, setMode] = useState<string | null>(null)
  const [welcomeMessage, setWelcomeMessage] = useState<string | undefined>(undefined)
  const [returningMessage, setReturningMessage] = useState<string | undefined>(undefined)
  const [offlineMessage, setOfflineMessage] = useState<string | undefined>(undefined)
  const [fallbackMessage, setFallbackMessage] = useState<string | undefined>(undefined)
  const [mediaMessage, setMediaMessage] = useState<string | undefined>(undefined)
  const [handoffMessage, setHandoffMessage] = useState<string | undefined>(undefined)
  const [queueTransferMessage, setQueueTransferMessage] = useState<string | undefined>(undefined)
  const [success, setSuccess] = useState<string | null>(null)
  const [testResult, setTestResult] = useState<{ success: boolean; error?: string } | null>(null)

  const { data: providers = [] } = useQuery({
    queryKey: ['ai-providers'],
    queryFn: async () => {
      const response = await fetchWithCsrf('/api/integrations/ai/providers')
      return response.json() as Promise<ProviderInfo[]>
    },
    enabled: aiEnabled,
  })

  const { data: config, isLoading: isAiLoading } = useQuery({
    queryKey: ['ai-config'],
    queryFn: async () => {
      const response = await fetchWithCsrf('/api/integrations/ai')
      return response.json() as Promise<AiConfigResponse>
    },
    enabled: aiEnabled,
  })

  const { data: botConfig, isLoading: isBotLoading } = useQuery({
    queryKey: ['bot-config'],
    queryFn: async () => {
      const response = await fetchWithCsrf('/api/bot-config')
      return response.json() as Promise<BotConfigResponse>
    },
    enabled: aiEnabled,
  })

  const { data: queues = [] } = useQuery({
    queryKey: ['service-queues'],
    queryFn: async () => {
      const response = await fetchWithCsrf('/api/service-queues')
      return response.json() as Promise<Array<{ id: string; name: string; description?: string; isActive: boolean }>>
    },
    enabled: aiEnabled,
  })

  const { data: tags = [] } = useQuery({
    queryKey: ['client-tags'],
    queryFn: async () => {
      const response = await fetchWithCsrf('/api/client-tags')
      return response.json() as Promise<Array<{ id: string; name: string; description?: string; color?: string; isActive: boolean }>>
    },
    enabled: aiEnabled,
  })

  useEffect(() => {
    if (!config || loadedVersion === (config.version ?? 0)) return
    setLoadedVersion(config.version ?? 0)
    const storedPrompt = config.systemPrompt || ''
    const businessLine = storedPrompt.match(/^Tipo de negócio: (.+)\n\n/)
    setBusinessType(businessLine?.[1] || '')
    setSystemPrompt(businessLine ? storedPrompt.slice(businessLine[0].length) : storedPrompt)
    setMaxTokens(config.maxTokensPerResponse || 180)
    setConfidenceThreshold(config.confidenceThreshold ?? 0.5)
    setRoutingQueueIds(config.routingQueueIds || [])
    setRoutingTagIds(config.routingTagIds || [])
  }, [config, loadedVersion])

  useEffect(() => {
    if (!botConfig || loadedBotVersion === (botConfig.version ?? 0)) return
    setLoadedBotVersion(botConfig.version ?? 0)
    setMode(botConfig.mode || 'Manual')
    setWelcomeMessage(botConfig.welcomeMessage ?? undefined)
    setReturningMessage(botConfig.returningMessage ?? undefined)
    setOfflineMessage(botConfig.offlineMessage ?? undefined)
    setFallbackMessage(botConfig.fallbackMessage ?? undefined)
    setMediaMessage(botConfig.mediaMessage ?? undefined)
    setHandoffMessage(botConfig.handoffMessage ?? undefined)
    setQueueTransferMessage(botConfig.queueTransferMessage ?? undefined)
  }, [botConfig, loadedBotVersion])

  const provider = selectedProvider ?? config?.provider ?? providers[0]?.id ?? 'openai'
  const modelId = selectedModel ?? config?.modelId ?? ''
  const models = providers.find((item) => item.id === provider)?.models ?? []
  const currentMode = mode ?? botConfig?.mode ?? 'Manual'
  const isAiActive = config?.aiActive === true
  const value = (local: string | undefined, remote: string | null | undefined) => local ?? remote ?? ''
  const botMessages = () => ({
    welcomeMessage: value(welcomeMessage, botConfig?.welcomeMessage),
    returningMessage: value(returningMessage, botConfig?.returningMessage),
    offlineMessage: value(offlineMessage, botConfig?.offlineMessage),
    fallbackMessage: value(fallbackMessage, botConfig?.fallbackMessage),
    handoffMessage: value(handoffMessage, botConfig?.handoffMessage),
    queueTransferMessage: value(queueTransferMessage, botConfig?.queueTransferMessage),
    mediaMessage: value(mediaMessage, botConfig?.mediaMessage),
  })

  const saveProviderMutation = useMutation({
    mutationFn: async () => {
      const response = await fetchWithCsrf('/api/integrations/ai', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({ provider, modelId, apiKey: apiKey || undefined }),
      })
      if (!response.ok) throw new Error((await response.json().catch(() => null))?.error || 'Erro ao salvar configuração de IA')
      return response.json()
    },
    onSuccess: () => {
      setApiKey('')
      setSuccess('provider')
      queryClient.invalidateQueries({ queryKey: ['ai-config'] })
    },
  })

  const toggleMutation = useMutation({
    mutationFn: async (enabled: boolean) => {
      const response = await fetchWithCsrf('/api/integrations/ai/toggle', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({ enabled }),
      })
      if (!response.ok) throw new Error((await response.json().catch(() => null))?.error || 'Erro ao alterar IA')
      return response.json()
    },
    onSuccess: () => {
      setSuccess('mode')
      queryClient.invalidateQueries({ queryKey: ['ai-config'] })
      queryClient.invalidateQueries({ queryKey: ['bot-config'] })
    },
  })

  const modeMutation = useMutation({
    mutationFn: async (nextMode: string) => {
      const response = await fetchWithCsrf(botConfig?.configured ? '/api/bot-config/mode' : '/api/bot-config', {
        method: botConfig?.configured ? 'PUT' : 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify(botConfig?.configured ? { mode: nextMode } : {
          ...botMessages(),
          mode: nextMode,
          maxTokensPerResponse: botConfig?.maxTokensPerResponse ?? 500,
          flowSteps: botConfig?.flowSteps ?? [],
        }),
      })
      if (!response.ok) throw new Error((await response.json().catch(() => null))?.error || 'Erro ao alterar modo')
      return response.json()
    },
    onSuccess: (_, nextMode) => {
      setMode(nextMode)
      setSuccess('mode')
      queryClient.invalidateQueries({ queryKey: ['ai-config'] })
      queryClient.invalidateQueries({ queryKey: ['bot-config'] })
    },
  })

  const testMutation = useMutation({
    mutationFn: async () => {
      const response = await fetchWithCsrf('/api/integrations/ai/test-connection', { method: 'POST', credentials: 'include' })
      return (await response.json()) as { success: boolean; error?: string }
    },
    onSuccess: (result) => setTestResult(result),
  })

  const saveInstructionsMutation = useMutation({
    mutationFn: async () => {
      const response = await fetchWithCsrf('/api/integrations/ai/instructions', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json', 'If-Match': String(loadedVersion) },
        credentials: 'include',
        body: JSON.stringify({
          systemPrompt: `Tipo de negócio: ${businessType || 'Não informado'}\n\n${systemPrompt}`.slice(0, 4000),
          maxTokensPerResponse: maxTokens,
          confidenceThreshold,
          routingQueueIds,
          routingTagIds,
        }),
      })
      if (!response.ok) throw new Error((await response.json().catch(() => null))?.error || 'Não foi possível salvar as diretrizes.')
      return response.json()
    },
    onSuccess: () => {
      setSuccess('instructions')
      queryClient.invalidateQueries({ queryKey: ['ai-config'] })
    },
  })

  const saveMessagesMutation = useMutation({
    mutationFn: async () => {
      const body = botMessages()
      const response = await fetchWithCsrf(botConfig?.configured ? '/api/bot-config/messages' : '/api/bot-config', {
        method: botConfig?.configured ? 'PUT' : 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify(botConfig?.configured ? body : {
          ...body,
          mode: currentMode,
          maxTokensPerResponse: botConfig?.maxTokensPerResponse ?? 500,
          flowSteps: botConfig?.flowSteps ?? [],
        }),
      })
      if (!response.ok) throw new Error((await response.json().catch(() => null))?.error || 'Erro ao salvar mensagens automáticas')
      return response.json()
    },
    onSuccess: () => {
      setSuccess('messages')
      queryClient.invalidateQueries({ queryKey: ['bot-config'] })
    },
  })

  if (!aiEnabled) {
    return <div className="h-full flex items-center justify-center"><div className="text-center"><Lock className="w-12 h-12 text-slate-300 mx-auto mb-4" /><h2 className="text-lg font-semibold text-slate-700">IA não disponível</h2><p className="text-sm text-slate-500 mt-1">Seu plano não inclui IA.</p></div></div>
  }

  if (isAiLoading || isBotLoading) {
    return <div className="h-full flex items-center justify-center"><Loader2 className="w-8 h-8 text-emerald-500 animate-spin" /></div>
  }

  const guidelineGroups = [
    ['Comportamento', config?.guidelines?.behavior],
    ['Segurança', config?.guidelines?.security],
    ['Handoff humano', config?.guidelines?.handoff],
  ] as const

  return (
    <div className="h-full overflow-y-auto bg-slate-50">
      <div className="max-w-3xl mx-auto px-6 py-8 space-y-6">
        <div className="flex items-start justify-between gap-4"><div className="flex items-center gap-3"><div className="w-10 h-10 rounded-lg bg-violet-50 text-violet-600 flex items-center justify-center"><Bot className="w-5 h-5" /></div><div><h1 className="text-xl font-bold text-slate-900">Atendimento com IA</h1><p className="text-sm text-slate-500">Diretrizes da IA e configuração completa do atendimento automatizado.</p></div></div><button onClick={() => toggleMutation.mutate(!isAiActive)} disabled={toggleMutation.isPending || !config?.configured} className={`flex items-center gap-2 px-4 py-2 rounded-lg font-medium text-sm disabled:opacity-50 ${isAiActive ? 'bg-violet-100 text-violet-700' : 'bg-slate-100 text-slate-700'}`}>{toggleMutation.isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Power className="w-4 h-4" />}{isAiActive ? 'IA ativa' : 'Ativar IA'}</button></div>

        <Section title="Provedor de IA" description="Escolha o provedor, modelo e credencial usados pelo atendimento.">
          <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3 mb-4">{providers.map((item) => <button key={item.id} onClick={() => { setSelectedProvider(item.id); setSelectedModel('') }} className={`px-4 py-3 rounded-lg border-2 font-medium text-sm ${provider === item.id ? `${providerColors[item.id] ?? 'bg-slate-100 text-slate-700 border-slate-300'} border-current ring-2 ring-offset-1` : 'border-slate-200 text-slate-600 hover:border-slate-300'}`}>{item.name}</button>)}</div>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4"><div><label className="block text-sm font-medium text-slate-700 mb-1">Modelo</label><select value={modelId} onChange={(event) => setSelectedModel(event.target.value)} className="w-full px-4 py-2.5 border border-slate-300 rounded-lg"><option value="">Selecione...</option>{models.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select></div><div><label className="block text-sm font-medium text-slate-700 mb-1">API Key</label><input type="password" value={apiKey} onChange={(event) => setApiKey(event.target.value)} placeholder={config?.configured ? '••••••••' : 'Cole sua chave...'} className="w-full px-4 py-2.5 border border-slate-300 rounded-lg" /><p className="text-xs text-slate-500 mt-1">Criptografada e nunca exibida novamente.</p></div></div>
          <div className="flex gap-3 mt-4"><button onClick={() => saveProviderMutation.mutate()} disabled={saveProviderMutation.isPending || !modelId} className="flex items-center gap-2 px-5 py-2.5 bg-emerald-500 hover:bg-emerald-600 text-white rounded-lg font-medium disabled:opacity-50">{saveProviderMutation.isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}Salvar</button><button onClick={() => testMutation.mutate()} disabled={testMutation.isPending || !config?.configured} className="flex items-center gap-2 px-5 py-2.5 bg-slate-100 hover:bg-slate-200 text-slate-700 rounded-lg font-medium disabled:opacity-50">{testMutation.isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Zap className="w-4 h-4" />}Testar conexão</button></div>
          {success === 'provider' && <p className="mt-4 text-sm text-emerald-600 flex items-center gap-1"><CheckCircle2 className="w-4 h-4" /> Configuração salva com sucesso</p>}{testResult && <p className={`mt-2 text-sm flex items-center gap-1 ${testResult.success ? 'text-emerald-600' : 'text-red-600'}`}>{testResult.success ? <CheckCircle2 className="w-4 h-4" /> : <XCircle className="w-4 h-4" />}{testResult.success ? 'Conexão validada.' : testResult.error || 'Falha na conexão'}</p>}
        </Section>

        <Section title="Modo de operação" description="O modo define quando o bot pode responder automaticamente.">
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">{[['Manual', 'Somente atendimento humano.'], ['SimpleAutoReply', 'Respostas automáticas simples.'], ['AiPowered', 'Respostas com contexto e IA.']].map(([option, description]) => <label key={option} className={`flex items-start gap-3 p-3 border rounded-lg cursor-pointer ${currentMode === option ? 'border-emerald-400 bg-emerald-50' : 'border-slate-200'}`}><input type="radio" name="ai-mode" checked={currentMode === option} onChange={() => modeMutation.mutate(option)} disabled={modeMutation.isPending || (option === 'AiPowered' && !config?.configured)} className="mt-1" /><span><span className="block text-sm font-medium text-slate-800">{option === 'AiPowered' ? 'IA' : option}</span><span className="block text-xs text-slate-500 mt-1">{description}</span></span></label>)}</div>
          {modeMutation.isError && <p className="mt-3 text-sm text-red-600">{(modeMutation.error as Error).message}</p>}{success === 'mode' && <p className="mt-3 text-sm text-emerald-600">Modo atualizado.</p>}
        </Section>

        <Section title="Regras estruturadas" description="Aplicadas pelo sistema, sem depender das instruções livres."><div className="grid gap-4 md:grid-cols-3">{guidelineGroups.map(([title, rules]) => <div key={title}><h3 className="text-xs font-semibold text-violet-900">{title}</h3><ul className="mt-1 space-y-1 text-xs text-slate-600">{(rules || []).map((rule) => <li key={rule.code}>{rule.description}</li>)}</ul></div>)}</div></Section>

        <Section title="Diretrizes do negócio" description="Instruções complementares para o contexto específico da empresa."><div className="space-y-5"><div><label className="block text-sm font-medium text-slate-700 mb-1">Tipo de negócio *</label><select value={businessType} onChange={(event) => setBusinessType(event.target.value)} className="w-full px-4 py-2.5 border border-slate-300 rounded-lg"><option value="">Selecione o segmento</option><option>Clínica e saúde</option><option>Restaurante e alimentação</option><option>Comércio e varejo</option><option>Serviços profissionais</option><option>Educação</option><option>Imobiliária</option><option>Outro</option></select></div><div><label className="block text-sm font-medium text-slate-700 mb-1">Instruções de atendimento</label><textarea value={systemPrompt} onChange={(event) => setSystemPrompt(event.target.value)} rows={8} maxLength={3900} placeholder="Ex.: Responda em português, seja breve e confirme informações antes de prometer prazos." className="w-full px-4 py-3 border border-slate-300 rounded-lg resize-y" /><p className="text-xs text-slate-500 mt-1">{systemPrompt.length}/3900 caracteres.</p></div><div><label className="block text-sm font-medium text-slate-700 mb-1">Limiar de confiança</label><input type="number" value={confidenceThreshold} onChange={(event) => setConfidenceThreshold(Math.min(1, Math.max(0, Number(event.target.value))))} min={0} max={1} step={0.05} className="w-36 px-4 py-2.5 border border-slate-300 rounded-lg" /><p className="text-xs text-slate-500 mt-1">Abaixo deste valor, a conversa é encaminhada para atendimento humano.</p></div></div><div className="flex items-center gap-3 mt-5"><button onClick={() => saveInstructionsMutation.mutate()} disabled={saveInstructionsMutation.isPending || loadedVersion === null || !config?.configured || !businessType} className="flex items-center gap-2 px-5 py-2.5 bg-emerald-500 hover:bg-emerald-600 text-white rounded-lg font-medium disabled:opacity-50">{saveInstructionsMutation.isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}Salvar diretrizes</button>{success === 'instructions' && <span className="text-sm text-emerald-600">Diretrizes salvas.</span>}</div></Section>

        <Section title="Filas e tags para IA" description="Somente filas e tags ativas selecionadas entram no contexto e nas decisões da IA."><div className="space-y-5"><div><label className="block text-sm font-medium text-slate-700 mb-2">Filas para encaminhamento automático</label>{queues.filter((queue) => queue.isActive).length === 0 ? <p className="text-sm text-slate-500">Cadastre e ative uma fila no menu Filas.</p> : <div className="space-y-2">{queues.filter((queue) => queue.isActive).map((queue) => <label key={queue.id} className="flex items-start gap-3 p-3 border border-slate-200 rounded-lg cursor-pointer"><input type="checkbox" checked={routingQueueIds.includes(queue.id)} onChange={() => setRoutingQueueIds((current) => current.includes(queue.id) ? current.filter((id) => id !== queue.id) : [...current, queue.id])} className="mt-1" /><span><span className="block text-sm font-medium text-slate-800">{queue.name}</span>{queue.description && <span className="block text-xs text-slate-500">{queue.description}</span>}</span></label>)}</div>}</div><div><label className="block text-sm font-medium text-slate-700 mb-2">Tags para categorização automática</label>{tags.filter((tag) => tag.isActive).length === 0 ? <p className="text-sm text-slate-500">Cadastre e ative uma tag no menu Tags.</p> : <div className="space-y-2">{tags.filter((tag) => tag.isActive).map((tag) => <label key={tag.id} className="flex items-start gap-3 p-3 border border-slate-200 rounded-lg cursor-pointer"><input type="checkbox" checked={routingTagIds.includes(tag.id)} onChange={() => setRoutingTagIds((current) => current.includes(tag.id) ? current.filter((id) => id !== tag.id) : [...current, tag.id])} className="mt-1" /><span className="flex-1"><span className="flex items-center gap-2 text-sm font-medium text-slate-800"><span className="w-3 h-3 rounded-full" style={{ backgroundColor: tag.color || '#64748b' }} />{tag.name}</span>{tag.description && <span className="block text-xs text-slate-500 mt-1">{tag.description}</span>}</span></label>)}</div>}</div></div><button onClick={() => saveInstructionsMutation.mutate()} disabled={saveInstructionsMutation.isPending || loadedVersion === null || !config?.configured} className="mt-5 flex items-center gap-2 px-5 py-2.5 bg-slate-100 hover:bg-slate-200 text-slate-700 rounded-lg font-medium disabled:opacity-50">{saveInstructionsMutation.isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}Salvar filas e tags</button></Section>

        <Section title="Mensagens automáticas" description="Mensagens reutilizadas pelo fluxo de atendimento, fallback e handoff."><div className="space-y-5"><MessageField icon={MessageSquare} label="Primeiro contato" hint="Enviada quando um novo cliente envia a primeira mensagem." value={value(welcomeMessage, botConfig?.welcomeMessage)} onChange={setWelcomeMessage} placeholder="Olá! Bem-vindo(a). Como posso ajudar?" /><MessageField icon={UserCheck} label="Cliente recorrente" hint="Enviada quando um cliente que já conversou anteriormente envia nova mensagem." value={value(returningMessage, botConfig?.returningMessage)} onChange={setReturningMessage} placeholder="Olá! Que bom ter você de volta. No que posso ajudar?" /><MessageField icon={WifiOff} label="Fora do horário / sem atendente" hint="Enviada quando não há operadores disponíveis para atender." value={value(offlineMessage, botConfig?.offlineMessage)} onChange={setOfflineMessage} placeholder="No momento estamos fora do horário de atendimento." /><MessageField icon={HelpCircle} label="Resposta padrão (fallback)" hint="Enviada quando não existe resposta segura ou a intenção não é identificada." value={value(fallbackMessage, botConfig?.fallbackMessage)} onChange={setFallbackMessage} placeholder="Não consegui identificar uma resposta segura. Vou encaminhar você a um atendente." /><MessageField icon={Image} label="Recebimento de mídia" hint="Enviada quando o cliente envia uma imagem, vídeo ou arquivo." value={value(mediaMessage, botConfig?.mediaMessage)} onChange={setMediaMessage} placeholder="Recebi seu arquivo! Um atendente irá analisá-lo." /><MessageField icon={ArrowRightLeft} label="Transferência para atendente" hint="Enviada ao encaminhar o cliente para um operador humano." value={value(handoffMessage, botConfig?.handoffMessage)} onChange={setHandoffMessage} placeholder="Estou transferindo você para um atendente." /><MessageField icon={GitBranch} label="Transferência para fila" hint="Enviada ao mover o cliente para uma fila especializada." value={value(queueTransferMessage, botConfig?.queueTransferMessage)} onChange={setQueueTransferMessage} placeholder="Estou encaminhando para a fila especializada." /></div><div className="flex items-center gap-3 mt-5"><button onClick={() => saveMessagesMutation.mutate()} disabled={saveMessagesMutation.isPending} className="flex items-center gap-2 px-5 py-2.5 bg-emerald-500 hover:bg-emerald-600 text-white rounded-lg font-medium disabled:opacity-50">{saveMessagesMutation.isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}Salvar mensagens</button>{success === 'messages' && <span className="text-sm text-emerald-600">Mensagens salvas.</span>}{saveMessagesMutation.isError && <span className="text-sm text-red-600">{(saveMessagesMutation.error as Error).message}</span>}</div></Section>

        <div className="flex items-center gap-2 text-xs text-slate-500 pb-4"><Info className="w-4 h-4" /> As configurações de IA e do bot permanecem em seus endpoints próprios, mas são editadas nesta única tela.</div>
      </div>
    </div>
  )
}

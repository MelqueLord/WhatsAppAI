import { fetchWithCsrf } from '../../lib/api'
import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../../lib/auth'
import {
  Bot, CheckCircle2, Loader2, Plus, Save, Trash2,
  MessageSquare, UserCheck, WifiOff, HelpCircle,
  Image, ArrowRightLeft, GitBranch, ChevronDown, ChevronUp,
  ToggleLeft, ToggleRight, Info,
} from 'lucide-react'

interface FlowStep {
  id: string
  title: string
  keywords: string
  response: string
}

interface BusinessHoursDay {
  dayOfWeek: number
  enabled: boolean
  open: string
  close: string
}

interface BotConfig {
  configured: boolean
  mode: string
  welcomeMessage: string | null
  returningMessage?: string | null
  offlineMessage?: string | null
  fallbackMessage: string | null
  mediaMessage?: string | null
  handoffMessage?: string | null
  queueTransferMessage?: string | null
  enabled: boolean
  version?: number
  flowSteps?: FlowStep[]
  businessHoursEnabled?: boolean
  timeZoneId?: string
  businessHours?: BusinessHoursDay[]
}

const newStep = (): FlowStep => ({ id: `step-${Date.now()}`, title: '', keywords: '', response: '' })
const defaultBusinessHours = (): BusinessHoursDay[] => Array.from({ length: 7 }, (_, dayOfWeek) => ({ dayOfWeek, enabled: false, open: '09:00', close: '18:00' }))
const dayNames = ['Domingo', 'Segunda-feira', 'Terça-feira', 'Quarta-feira', 'Quinta-feira', 'Sexta-feira', 'Sábado']

// ── Helpers ──────────────────────────────────────────────────────────────────

export function Section({ title, description, children }: { title: string; description?: string; children: React.ReactNode }) {
  return (
    <div className="bg-white rounded-xl border border-slate-200 overflow-hidden">
      <div className="px-6 py-4 border-b border-slate-100">
        <h2 className="text-sm font-semibold text-slate-800">{title}</h2>
        {description && <p className="text-xs text-slate-500 mt-0.5">{description}</p>}
      </div>
      <div className="p-6">{children}</div>
    </div>
  )
}

export function MessageField({
  icon: Icon,
  label,
  hint,
  value,
  onChange,
  placeholder,
}: {
  icon: React.ElementType
  label: string
  hint: string
  value: string
  onChange: (v: string) => void
  placeholder: string
}) {
  return (
    <div>
      <div className="flex items-center gap-2 mb-1.5">
        <Icon className="w-4 h-4 text-slate-400 flex-shrink-0" />
        <label className="text-sm font-medium text-slate-700">{label}</label>
        <span className="group relative ml-auto cursor-default">
          <Info className="w-3.5 h-3.5 text-slate-300 hover:text-slate-500" />
          <span className="pointer-events-none absolute bottom-full right-0 mb-1.5 w-52 rounded-lg bg-slate-800 px-3 py-2 text-xs text-slate-200 opacity-0 group-hover:opacity-100 transition-opacity z-10 shadow-lg">
            {hint}
          </span>
        </span>
      </div>
      <textarea
        rows={2}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder={placeholder}
        className="w-full px-3.5 py-2.5 border border-slate-200 rounded-lg text-sm text-slate-800 placeholder-slate-400 resize-none focus:outline-none focus:ring-2 focus:ring-emerald-400 focus:border-transparent transition-all"
      />
    </div>
  )
}

// ── Component ─────────────────────────────────────────────────────────────────

export function BotConfigPage() {
  const queryClient = useQueryClient()
  const { user } = useAuth()
  const [welcomeMessage, setWelcomeMessage] = useState<string | undefined>(undefined)
  const [returningMessage, setReturningMessage] = useState<string | undefined>(undefined)
  const [offlineMessage, setOfflineMessage] = useState<string | undefined>(undefined)
  const [fallbackMessage, setFallbackMessage] = useState<string | undefined>(undefined)
  const [mediaMessage, setMediaMessage] = useState<string | undefined>(undefined)
  const [handoffMessage, setHandoffMessage] = useState<string | undefined>(undefined)
  const [queueTransferMessage, setQueueTransferMessage] = useState<string | undefined>(undefined)
  const [flowSteps, setFlowSteps] = useState<FlowStep[] | undefined>(undefined)
  const [businessHoursEnabled, setBusinessHoursEnabled] = useState(false)
  const [timeZoneId, setTimeZoneId] = useState('America/Sao_Paulo')
  const [businessHours, setBusinessHours] = useState<BusinessHoursDay[]>(defaultBusinessHours)
  const [success, setSuccess] = useState(false)
  const [expandedStep, setExpandedStep] = useState<string | null>(null)
  const [previewInput, setPreviewInput] = useState('')
  const { data: config, isLoading, isError, error } = useQuery({
    queryKey: ['bot-config'],
    queryFn: async () => {
      const res = await fetchWithCsrf('/api/bot-config')
      if (!res.ok) {
        const body = await res.json().catch(() => null) as { error?: string } | null
        throw new Error(body?.error || 'Não foi possível carregar a configuração do BOT.')
      }
      return res.json() as Promise<BotConfig>
    },
  })

  useEffect(() => {
    if (!config) return
    const timer = window.setTimeout(() => {
      setBusinessHoursEnabled(config.businessHoursEnabled === true)
      setTimeZoneId(config.timeZoneId ?? 'America/Sao_Paulo')
      setBusinessHours(config.businessHours?.length === 7 ? config.businessHours : defaultBusinessHours())
      setFlowSteps(config.flowSteps ?? [])
    }, 0)
    return () => window.clearTimeout(timer)
  }, [config])

  const version = config?.version ?? 0

  const toggleMutation = useMutation({
    mutationFn: async (enabled: boolean) => {
      const res = await fetchWithCsrf('/api/bot-config/toggle', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'If-Match': String(version) },
        credentials: 'include',
        body: JSON.stringify({ enabled, mode: enabled ? 'SimpleAutoReply' : undefined }),
      })
      if (!res.ok) {
        const body = await res.json().catch(() => null) as { error?: string } | null
        throw new Error(body?.error || (res.status === 409 ? 'A configuração foi alterada por outro usuário.' : 'Erro ao alterar status'))
      }
      return res.json()
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['bot-config'] }),
  })

  const saveMutation = useMutation({
    mutationFn: async () => {
      const steps = (flowSteps ?? config?.flowSteps ?? []).filter(
        (s) => s.title.trim() && s.response.trim()
      )
      const res = await fetchWithCsrf('/api/bot-config', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'If-Match': String(version) },
        credentials: 'include',
        body: JSON.stringify({
          mode: config?.mode ?? 'SimpleAutoReply',
          welcomeMessage: welcomeMessage ?? config?.welcomeMessage ?? '',
          returningMessage: returningMessage ?? config?.returningMessage ?? '',
          offlineMessage: offlineMessage ?? config?.offlineMessage ?? '',
          fallbackMessage: fallbackMessage ?? config?.fallbackMessage ?? '',
          mediaMessage: mediaMessage ?? config?.mediaMessage ?? '',
          handoffMessage: handoffMessage ?? config?.handoffMessage ?? '',
          queueTransferMessage: queueTransferMessage ?? config?.queueTransferMessage ?? '',
          flowSteps: steps,
          businessHoursEnabled,
          timeZoneId,
          businessHours,
        }),
      })
      if (!res.ok) {
        const body = await res.json().catch(() => null) as { error?: string } | null
        throw new Error(body?.error || (res.status === 409 ? 'A configuração foi alterada por outro usuário. Recarregue a tela.' : 'Erro ao salvar'))
      }
      return res.json()
    },
    onSuccess: () => {
      setSuccess(true)
      queryClient.invalidateQueries({ queryKey: ['bot-config'] })
      setTimeout(() => setSuccess(false), 3000)
    },
  })

  const updateStep = (id: string, patch: Partial<FlowStep>) =>
    setFlowSteps((prev) => (prev ?? effectiveSteps).map((s) => (s.id === id ? { ...s, ...patch } : s)))

  const removeStep = (id: string) =>
    setFlowSteps((prev) => (prev ?? effectiveSteps).filter((s) => s.id !== id))

  const addStep = () => {
    const s = newStep()
    setFlowSteps((prev) => [...(prev ?? effectiveSteps), s])
    setExpandedStep(s.id)
  }

  const updateBusinessDay = (dayOfWeek: number, patch: Partial<BusinessHoursDay>) =>
    setBusinessHours((days) => days.map((day) => day.dayOfWeek === dayOfWeek ? { ...day, ...patch } : day))

  if (isLoading)
    return (
      <div className="h-full flex items-center justify-center">
        <Loader2 className="w-8 h-8 text-emerald-500 animate-spin" />
      </div>
    )

  if (isError)
    return <div className="h-full flex items-center justify-center text-sm text-red-600">{(error as Error).message}</div>

  const effectiveSteps = config?.flowSteps?.length ? config.flowSteps : []
  const steps = flowSteps ?? effectiveSteps
  const isBotActive = config?.enabled === true && config.mode === 'SimpleAutoReply'

  const val = (local: string | undefined, remote: string | null | undefined) =>
    local ?? remote ?? ''
  const previewStep = previewInput.trim() && steps.find((step) =>
    step.keywords.split(',').some((keyword) => keyword.trim() && previewInput.toLowerCase().includes(keyword.trim().toLowerCase()))
  )

  return (
    <div className="h-full overflow-y-auto bg-slate-50">
      <div className="max-w-3xl mx-auto px-6 py-8 space-y-6">

        {/* ── Header ── */}
        <div className="flex items-start justify-between gap-4">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-lg bg-emerald-50 text-emerald-600 flex items-center justify-center flex-shrink-0">
              <Bot className="w-5 h-5" />
            </div>
            <div>
              <h1 className="text-lg font-bold text-slate-900">Fluxo do Bot</h1>
              <p className="text-sm text-slate-500">Configure as respostas automáticas do WhatsApp</p>
            </div>
          </div>

          {/* Toggle on/off */}
          <button
            onClick={() => toggleMutation.mutate(!isBotActive)}
            disabled={toggleMutation.isPending || !config?.configured}
            title={!config?.configured ? 'Salve a configuração antes de ativar' : undefined}
            className={`flex items-center gap-2 px-4 py-2 rounded-lg border text-sm font-medium transition-colors
              ${isBotActive
                ? 'bg-emerald-50 border-emerald-200 text-emerald-700 hover:bg-emerald-100'
                : 'bg-white border-slate-200 text-slate-600 hover:bg-slate-50'
              } disabled:opacity-50 disabled:cursor-not-allowed`}
          >
            {toggleMutation.isPending
              ? <Loader2 className="w-4 h-4 animate-spin" />
              : isBotActive
                ? <ToggleRight className="w-5 h-5 text-emerald-500" />
                : <ToggleLeft className="w-5 h-5 text-slate-400" />
            }
            {isBotActive ? 'Bot ativo' : 'Bot inativo'}
          </button>
        </div>

        {/* ── Status banner when inactive ── */}
        {!isBotActive && config?.configured && (
          <div className="flex items-center gap-3 px-4 py-3 bg-amber-50 border border-amber-200 rounded-lg text-sm text-amber-800">
            <Info className="w-4 h-4 flex-shrink-0 text-amber-500" />
            O bot está <strong>inativo</strong>. As mensagens automáticas não serão enviadas.
          </div>
        )}
        {user?.aiEnabled && (
          <div className="flex items-center gap-3 px-4 py-3 bg-violet-50 border border-violet-200 rounded-lg text-sm text-violet-800">
            <Info className="w-4 h-4 flex-shrink-0" />
            O BOT e a IA são mutuamente exclusivos: ativar o BOT desativa a IA, e ativar a IA desativa o BOT.
          </div>
        )}

        <Section
          title="Horário de atendimento"
          description="Defina quando o BOT pode responder. Fora desse período, será usada a mensagem de fora do horário."
        >
          <div className="flex flex-wrap items-center justify-between gap-4 mb-5">
            <label className="flex items-center gap-2 text-sm text-slate-700">
              <input type="checkbox" checked={businessHoursEnabled} onChange={(e) => setBusinessHoursEnabled(e.target.checked)} />
              Ativar horário de atendimento
            </label>
            <label className="text-sm font-medium text-slate-700">Fuso horário
              <select value={timeZoneId} onChange={(e) => setTimeZoneId(e.target.value)} className="ml-2 px-3 py-2 border border-slate-200 rounded-lg">
                <option value="America/Sao_Paulo">Brasília (UTC−03:00)</option>
                <option value="America/New_York">Nova York (UTC−05:00)</option>
                <option value="Europe/Lisbon">Lisboa (UTC±00:00)</option>
                <option value="UTC">UTC</option>
              </select>
            </label>
          </div>
          <div className={`space-y-2 ${!businessHoursEnabled ? 'opacity-50 pointer-events-none' : ''}`}>
            {businessHours.map((day) => (
              <div key={day.dayOfWeek} className="grid grid-cols-[1fr_auto_auto_auto] items-center gap-3 text-sm">
                <label className="flex items-center gap-2 text-slate-700"><input type="checkbox" checked={day.enabled} onChange={(e) => updateBusinessDay(day.dayOfWeek, { enabled: e.target.checked })} />{dayNames[day.dayOfWeek]}</label>
                <input type="time" value={day.open} onChange={(e) => updateBusinessDay(day.dayOfWeek, { open: e.target.value })} className="px-2 py-1.5 border border-slate-200 rounded-lg" />
                <span className="text-slate-400">até</span>
                <input type="time" value={day.close} onChange={(e) => updateBusinessDay(day.dayOfWeek, { close: e.target.value })} className="px-2 py-1.5 border border-slate-200 rounded-lg" />
              </div>
            ))}
          </div>
          <p className="mt-4 text-xs text-slate-500">Salve esta configuração junto com as mensagens. Se nenhum dia estiver ativo, o BOT não responderá enquanto o horário estiver habilitado.</p>
        </Section>

        {/* ── 1. Saudações ── */}
        <Section
          title="Saudações"
          description="Mensagens enviadas quando um cliente inicia ou retoma o contato."
        >
          <div className="space-y-5">
            <MessageField
              icon={MessageSquare}
              label="Primeiro contato"
              hint="Enviada quando um novo cliente envia a primeira mensagem."
              value={val(welcomeMessage, config?.welcomeMessage)}
              onChange={setWelcomeMessage}
              placeholder="Olá! Bem-vindo(a). Como posso ajudar?"
            />
            <MessageField
              icon={UserCheck}
              label="Cliente recorrente"
              hint="Enviada quando um cliente que já conversou anteriormente envia nova mensagem."
              value={val(returningMessage, config?.returningMessage)}
              onChange={setReturningMessage}
              placeholder="Olá! Que bom ter você de volta. No que posso ajudar?"
            />
          </div>
        </Section>

        <Section title="Pré-visualização segura" description="Teste localmente uma palavra-chave sem enviar mensagem nem alterar a conversa.">
          <div className="flex gap-3">
            <input value={previewInput} onChange={(e) => setPreviewInput(e.target.value)} placeholder="Digite uma mensagem de exemplo" className="flex-1 px-3.5 py-2.5 border border-slate-200 rounded-lg text-sm" />
          </div>
          {previewInput.trim() && <div className="mt-3 rounded-lg bg-slate-50 border border-slate-200 p-3 text-sm">{previewStep ? <><span className="font-medium text-slate-700">Resposta prevista:</span> {previewStep.response}</> : <span className="text-slate-500">Nenhuma opção corresponde; o fallback configurado será usado.</span>}</div>}
        </Section>

        {/* ── 2. Situações especiais ── */}
        <Section
          title="Situações especiais"
          description="Respostas automáticas para situações fora do fluxo normal."
        >
          <div className="space-y-5">
            <MessageField
              icon={WifiOff}
              label="Fora do horário / sem atendente"
              hint="Enviada quando não há operadores disponíveis para atender."
              value={val(offlineMessage, config?.offlineMessage)}
              onChange={setOfflineMessage}
              placeholder="No momento estamos fora do horário de atendimento. Retornaremos em breve!"
            />
            <MessageField
              icon={HelpCircle}
              label="Resposta padrão (fallback)"
              hint="Enviada quando o bot não consegue identificar a intenção do cliente."
              value={val(fallbackMessage, config?.fallbackMessage)}
              onChange={setFallbackMessage}
              placeholder="Não entendi sua mensagem. Pode reformular ou aguardar um atendente?"
            />
            <MessageField
              icon={Image}
              label="Recebimento de mídia"
              hint="Enviada quando o cliente envia uma imagem, vídeo ou arquivo."
              value={val(mediaMessage, config?.mediaMessage)}
              onChange={setMediaMessage}
              placeholder="Recebi seu arquivo! Um atendente irá analisá-lo em breve."
            />
          </div>
        </Section>

        {/* ── 3. Transferências ── */}
        <Section
          title="Transferências"
          description="Mensagens enviadas ao transferir o atendimento para um operador ou fila."
        >
          <div className="space-y-5">
            <MessageField
              icon={ArrowRightLeft}
              label="Transferência para atendente"
              hint="Enviada ao encaminhar o cliente para um operador humano."
              value={val(handoffMessage, config?.handoffMessage)}
              onChange={setHandoffMessage}
              placeholder="Estou transferindo você para um atendente. Aguarde um momento!"
            />
            <MessageField
              icon={GitBranch}
              label="Transferência para fila"
              hint="Enviada ao mover o cliente para uma fila de atendimento especializada."
              value={val(queueTransferMessage, config?.queueTransferMessage)}
              onChange={setQueueTransferMessage}
              placeholder="Estou encaminhando para a fila especializada. Em breve um atendente irá te chamar."
            />
          </div>
        </Section>

        {/* ── 4. Menu / Perguntas e respostas ── */}
        <Section
          title="Menu de opções"
          description='Palavras-chave que ativam respostas automáticas específicas. Ex: cliente digita "boleto" e recebe as instruções.'
        >
          {steps.length === 0 ? (
            <div className="text-center py-10">
              <div className="w-12 h-12 rounded-full bg-slate-100 flex items-center justify-center mx-auto mb-3">
                <Bot className="w-6 h-6 text-slate-400" />
              </div>
              <p className="text-sm font-medium text-slate-600 mb-1">Nenhuma opção configurada</p>
              <p className="text-xs text-slate-400 mb-4">Adicione itens para criar um menu automático de atendimento.</p>
              <button
                onClick={addStep}
                className="inline-flex items-center gap-2 px-4 py-2 bg-emerald-500 hover:bg-emerald-600 text-white rounded-lg text-sm font-medium transition-colors"
              >
                <Plus className="w-4 h-4" /> Adicionar primeira opção
              </button>
            </div>
          ) : (
            <div className="space-y-3">
              {steps.map((step, index) => {
                const isOpen = expandedStep === step.id
                return (
                  <div
                    key={step.id}
                    className="border border-slate-200 rounded-lg overflow-hidden"
                  >
                    {/* Step header */}
                    <div
                      role="button"
                      tabIndex={0}
                      onClick={() => setExpandedStep(isOpen ? null : step.id)}
                      onKeyDown={(e) => {
                        if (e.key === 'Enter' || e.key === ' ') {
                          e.preventDefault()
                          setExpandedStep(isOpen ? null : step.id)
                        }
                      }}
                      className="w-full flex items-center gap-3 px-4 py-3 bg-white hover:bg-slate-50 transition-colors text-left"
                    >
                      <span className="w-6 h-6 rounded-full bg-emerald-100 text-emerald-700 text-xs font-bold flex items-center justify-center flex-shrink-0">
                        {index + 1}
                      </span>
                      <span className="flex-1 text-sm font-medium text-slate-700 truncate">
                        {step.title || <span className="text-slate-400 font-normal">Nova opção</span>}
                      </span>
                      {step.keywords && (
                        <span className="hidden sm:flex items-center gap-1 flex-wrap max-w-xs">
                          {step.keywords.split(',').slice(0, 3).map((kw) => (
                            <span key={kw} className="px-2 py-0.5 bg-slate-100 text-slate-500 rounded text-xs truncate max-w-[80px]">
                              {kw.trim()}
                            </span>
                          ))}
                        </span>
                      )}
                      <div className="flex items-center gap-1 flex-shrink-0">
                        <button
                          type="button"
                          onClick={(e) => { e.stopPropagation(); removeStep(step.id) }}
                          className="p-1.5 rounded text-slate-300 hover:text-red-500 hover:bg-red-50 transition-colors"
                          title="Remover opção"
                        >
                          <Trash2 className="w-4 h-4" />
                        </button>
                        {isOpen
                          ? <ChevronUp className="w-4 h-4 text-slate-400" />
                          : <ChevronDown className="w-4 h-4 text-slate-400" />
                        }
                      </div>
                    </div>

                    {/* Step body */}
                    {isOpen && (
                      <div className="px-4 pb-4 pt-1 bg-slate-50 border-t border-slate-100 space-y-3">
                        <div>
                          <label className="block text-xs font-medium text-slate-600 mb-1">Título da opção</label>
                          <input
                            value={step.title}
                            onChange={(e) => updateStep(step.id, { title: e.target.value })}
                            placeholder="Ex: Segunda via de boleto"
                            className="w-full px-3.5 py-2.5 border border-slate-200 rounded-lg text-sm text-slate-800 placeholder-slate-400 bg-white focus:outline-none focus:ring-2 focus:ring-emerald-400 focus:border-transparent transition-all"
                          />
                        </div>
                        <div>
                          <label className="block text-xs font-medium text-slate-600 mb-1">
                            Palavras-chave <span className="text-slate-400 font-normal">(separe por vírgula)</span>
                          </label>
                          <input
                            value={step.keywords}
                            onChange={(e) => updateStep(step.id, { keywords: e.target.value })}
                            placeholder="boleto, segunda via, pagamento"
                            className="w-full px-3.5 py-2.5 border border-slate-200 rounded-lg text-sm text-slate-800 placeholder-slate-400 bg-white focus:outline-none focus:ring-2 focus:ring-emerald-400 focus:border-transparent transition-all"
                          />
                          <p className="mt-1 text-xs text-slate-400">Quando o cliente digitar uma dessas palavras, a resposta abaixo será enviada.</p>
                        </div>
                        <div>
                          <label className="block text-xs font-medium text-slate-600 mb-1">Resposta automática</label>
                          <textarea
                            rows={3}
                            value={step.response}
                            onChange={(e) => updateStep(step.id, { response: e.target.value })}
                            placeholder="Para obter a segunda via do boleto, acesse o link..."
                            className="w-full px-3.5 py-2.5 border border-slate-200 rounded-lg text-sm text-slate-800 placeholder-slate-400 bg-white resize-none focus:outline-none focus:ring-2 focus:ring-emerald-400 focus:border-transparent transition-all"
                          />
                        </div>
                      </div>
                    )}
                  </div>
                )
              })}

              <button
                type="button"
                onClick={addStep}
                className="w-full flex items-center justify-center gap-2 px-4 py-2.5 border border-dashed border-slate-300 rounded-lg text-sm text-slate-500 hover:border-emerald-400 hover:text-emerald-600 hover:bg-emerald-50 transition-colors"
              >
                <Plus className="w-4 h-4" /> Adicionar opção
              </button>
            </div>
          )}
        </Section>

        {/* ── Save bar ── */}
        <div className="flex items-center justify-between gap-4 py-4 border-t border-slate-200 sticky bottom-0 bg-slate-50">
          <div className="text-xs text-slate-400">
            As alterações só são aplicadas após salvar.
          </div>
          <div className="flex items-center gap-3">
            {success && (
              <span className="flex items-center gap-1.5 text-emerald-600 text-sm">
                <CheckCircle2 className="w-4 h-4" /> Salvo com sucesso
              </span>
            )}
            {saveMutation.isError && (
              <span className="text-red-600 text-sm">
                {(saveMutation.error as Error).message || 'Erro ao salvar'}
              </span>
            )}
            <button
              onClick={() => saveMutation.mutate()}
              disabled={saveMutation.isPending}
              className="flex items-center gap-2 px-5 py-2.5 bg-emerald-500 hover:bg-emerald-600 text-white rounded-lg text-sm font-medium disabled:opacity-50 transition-colors"
            >
              {saveMutation.isPending
                ? <Loader2 className="w-4 h-4 animate-spin" />
                : <Save className="w-4 h-4" />
              }
              Salvar configuração
            </button>
          </div>
        </div>

      </div>
    </div>
  )
}

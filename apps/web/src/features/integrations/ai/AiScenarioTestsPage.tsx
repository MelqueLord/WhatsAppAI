import { useMutation } from '@tanstack/react-query'
import {
  CalendarClock,
  CircleDollarSign,
  CircleHelp,
  FlaskConical,
  Headphones,
  Loader2,
  MessageCircleWarning,
  PenLine,
  Play,
} from 'lucide-react'
import { useState } from 'react'
import { fetchWithCsrf } from '../../../lib/api'
import { formatAiDecision, formatAiReason } from '../../../lib/utils'

interface AiScenarioResult {
  decision: string
  text?: string | null
  confidence: number
  handoffReason?: string | null
  fallbackReason?: string | null
  sources?: Array<{ type: string; name: string; detail: string }>
}

const scenarios = [
  {
    id: 'welcome',
    name: 'Boas-vindas',
    description: 'Confere se a IA inicia o atendimento com o perfil e o tom da sua empresa.',
    message: 'Oi, é meu primeiro contato.',
    icon: MessageCircleWarning,
  },
  {
    id: 'company-purpose',
    name: 'Sobre a empresa',
    description: 'Testa se a IA explica o negócio mesmo quando a pergunta usa outras palavras.',
    message: 'Pode me explicar como vocês podem me ajudar?',
    icon: CircleHelp,
  },
  {
    id: 'pricing',
    name: 'Preço',
    description: 'Confere se a IA usa somente valores cadastrados no conhecimento.',
    message: 'Qual é o preço do principal serviço da empresa?',
    icon: CircleDollarSign,
  },
  {
    id: 'complaint',
    name: 'Reclamação',
    description: 'Valida uma resposta segura e o encaminhamento quando necessário.',
    message: 'Estou insatisfeito com o atendimento e quero registrar uma reclamação.',
    icon: MessageCircleWarning,
  },
  {
    id: 'scheduling',
    name: 'Agendamento',
    description: 'Testa horários, orientações e limites do atendimento automático.',
    message: 'Gostaria de agendar um atendimento. Quais horários estão disponíveis?',
    icon: CalendarClock,
  },
  {
    id: 'human',
    name: 'Atendimento humano',
    description: 'Confere se o pedido para falar com uma pessoa é respeitado.',
    message: 'Quero falar com uma pessoa do atendimento.',
    icon: Headphones,
  },
  {
    id: 'out-of-scope',
    name: 'Fora do escopo',
    description: 'Verifica se a IA evita inventar informações que a empresa não cadastrou.',
    message: 'Vocês podem me orientar sobre um assunto que não faz parte dos serviços da empresa?',
    icon: CircleHelp,
  },
  {
    id: 'custom',
    name: 'Personalizado',
    description: 'Digite uma situação real específica do seu negócio.',
    message: '',
    icon: PenLine,
  },
] as const

export function AiScenarioTestsPage() {
  const [selectedScenarioId, setSelectedScenarioId] = useState<string>(scenarios[0].id)
  const [message, setMessage] = useState<string>(scenarios[0].message)

  const simulation = useMutation<AiScenarioResult, Error, string>({
    mutationFn: async (scenarioMessage) => {
      const response = await fetchWithCsrf('/api/integrations/ai/simulate', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({ message: scenarioMessage }),
      })
      const payload = await response.json().catch(() => null)
      if (!response.ok) {
        throw new Error(payload?.error || 'Não foi possível executar o teste de IA.')
      }
      return payload as AiScenarioResult
    },
  })

  const selectScenario = (scenario: (typeof scenarios)[number]) => {
    setSelectedScenarioId(scenario.id)
    setMessage(scenario.message)
    simulation.reset()
  }

  const selectedScenario = scenarios.find((scenario) => scenario.id === selectedScenarioId) ?? scenarios[0]
  const result = simulation.data

  return (
    <div className="h-full overflow-y-auto bg-slate-50">
      <div className="max-w-5xl mx-auto px-4 sm:px-6 py-6 sm:py-8 space-y-6">
        <header className="flex items-start gap-3">
          <div className="w-10 h-10 rounded-lg bg-cyan-50 text-cyan-700 flex items-center justify-center">
            <FlaskConical className="w-5 h-5" />
          </div>
          <div>
            <h1 className="text-xl font-bold text-slate-900">Teste de IA por cenário</h1>
            <p className="text-sm text-slate-500">Veja como a IA atenderia situações comuns usando a configuração atual da sua empresa.</p>
          </div>
        </header>

        <div className="rounded-xl border border-cyan-100 bg-cyan-50 p-4 text-sm text-cyan-950">
          O teste não envia mensagem ao WhatsApp e não consome a franquia de respostas. Como consulta o provedor de IA, pode gerar um pequeno consumo de tokens e custo técnico.
        </div>

        <section className="space-y-3" aria-labelledby="scenario-heading">
          <div>
            <h2 id="scenario-heading" className="font-semibold text-slate-900">Escolha uma situação</h2>
            <p className="text-xs text-slate-500 mt-1">Os cenários ajudam a conferir fatos, tom de voz e encaminhamento humano.</p>
          </div>
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {scenarios.map((scenario) => {
              const Icon = scenario.icon
              const selected = scenario.id === selectedScenarioId
              return (
                <button
                  key={scenario.id}
                  type="button"
                  onClick={() => selectScenario(scenario)}
                  className={`text-left rounded-xl border p-4 transition-colors ${selected ? 'border-cyan-500 bg-cyan-50 ring-1 ring-cyan-500' : 'border-slate-200 bg-white hover:border-cyan-200'}`}
                >
                  <span className={`w-9 h-9 rounded-lg flex items-center justify-center mb-3 ${selected ? 'bg-cyan-600 text-white' : 'bg-slate-100 text-slate-600'}`}>
                    <Icon className="w-4 h-4" />
                  </span>
                  <span className="block text-sm font-semibold text-slate-900">{scenario.name}</span>
                  <span className="block text-xs text-slate-500 mt-1 leading-relaxed">{scenario.description}</span>
                </button>
              )
            })}
          </div>
        </section>

        <section className="bg-white rounded-xl border border-slate-200 p-5 sm:p-6 space-y-4">
          <div>
            <h2 className="font-semibold text-slate-900">Mensagem do cenário: {selectedScenario.name}</h2>
            <p className="text-xs text-slate-500 mt-1">Você pode ajustar a mensagem antes de executar o teste.</p>
          </div>
          <label htmlFor="scenario-message" className="sr-only">Mensagem para testar</label>
          <textarea
            id="scenario-message"
            value={message}
            onChange={(event) => {
              setMessage(event.target.value)
              simulation.reset()
            }}
            rows={4}
            maxLength={500}
            placeholder="Digite uma mensagem que um cliente enviaria"
            className="w-full px-4 py-3 border border-slate-300 rounded-lg resize-y focus:outline-none focus:ring-2 focus:ring-cyan-500 focus:border-transparent"
          />
          <div className="flex flex-wrap items-center justify-between gap-3">
            <span className="text-xs text-slate-400">{message.length}/500 caracteres</span>
            <button
              type="button"
              onClick={() => simulation.mutate(message.trim())}
              disabled={simulation.isPending || !message.trim()}
              className="flex items-center gap-2 px-5 py-2.5 bg-cyan-600 hover:bg-cyan-700 text-white rounded-lg text-sm font-medium disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {simulation.isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Play className="w-4 h-4" />}
              {simulation.isPending ? 'Testando...' : 'Executar teste'}
            </button>
          </div>
          {simulation.isError && (
            <p role="alert" className="rounded-lg bg-red-50 px-4 py-3 text-sm text-red-700">{simulation.error.message}</p>
          )}
        </section>

        {result && (
          <section className="bg-white rounded-xl border border-slate-200 overflow-hidden" aria-live="polite">
            <div className="px-5 sm:px-6 py-4 border-b border-slate-100 bg-slate-50">
              <h2 className="font-semibold text-slate-900">Resultado do teste</h2>
              <p className="text-xs text-slate-500 mt-1">Esta seria a decisão da IA com a configuração atual.</p>
            </div>
            <div className="p-5 sm:p-6 space-y-4">
              <div className="grid gap-3 sm:grid-cols-2">
                <div className="rounded-lg bg-slate-50 p-4">
                  <p className="text-xs font-medium uppercase tracking-wide text-slate-500">Decisão</p>
                  <p className="mt-1 font-semibold text-slate-900">{formatAiDecision(result.decision)}</p>
                </div>
                <div className="rounded-lg bg-slate-50 p-4">
                  <p className="text-xs font-medium uppercase tracking-wide text-slate-500">Confiança</p>
                  <p className="mt-1 font-semibold text-slate-900">{(result.confidence * 100).toLocaleString('pt-BR', { maximumFractionDigits: 1 })}%</p>
                </div>
              </div>
              {result.text && (
                <div className="rounded-xl border border-emerald-100 bg-emerald-50 p-4">
                  <p className="text-xs font-semibold uppercase tracking-wide text-emerald-700">Resposta que seria enviada</p>
                  <p className="mt-2 text-sm text-emerald-950 whitespace-pre-wrap">{result.text}</p>
                </div>
              )}
              {result.handoffReason && (
                <p className="text-sm text-slate-700"><strong>Motivo do encaminhamento:</strong> {formatAiReason(result.handoffReason)}</p>
              )}
              {result.fallbackReason && (
                <p className="text-sm text-amber-700"><strong>Observação:</strong> {result.fallbackReason}</p>
              )}
              {result.sources && result.sources.length > 0 && (
                <div className="border-t border-slate-100 pt-4"><p className="text-xs font-semibold uppercase tracking-wide text-slate-500">Dados usados no teste</p><div className="mt-2 space-y-2">{result.sources.map((source, index) => <div key={`${source.type}-${source.name}-${index}`} className="rounded-lg bg-slate-50 px-3 py-2"><p className="text-sm font-medium text-slate-800">{source.name}</p><p className="text-xs text-slate-500">{source.detail}</p></div>)}</div></div>
              )}
            </div>
          </section>
        )}
      </div>
    </div>
  )
}

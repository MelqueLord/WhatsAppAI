import { useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { ArrowLeft, ArrowRight, Check, Loader2, Send } from 'lucide-react'
import { Link } from 'react-router-dom'
import atenzLogo from '../../assets/atenz-logo-a.png'
import { api, type AiOnboardingAnswers } from '../../lib/api'
import { aiGoals, businessActivities, departments, handoffConditions, informationSources, outsideHoursBehaviors, restrictionAnswers, restrictionTypes, serviceDays, serviceHours, whatsappSituations } from './aiOnboardingOptions'
const brlFormatter = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })

const initialAnswers: AiOnboardingAnswers = {
  companyName: '', businessActivity: '', productsOrServices: '', averageTicket: '', aiGoals: [],
  handoffConditions: [], serviceDays: '', serviceHours: '', customServiceHours: '',
  outsideHoursBehavior: '', departments: [], otherDepartment: '', whatsappSituation: '',
  previousRestriction: '', restrictionType: '', informationSources: [], informationNotes: '', aiInstructions: '',
}

export function AiOnboardingFormPage() {
  const token = window.location.hash.slice(1)
  const [step, setStep] = useState(0)
  const [answers, setAnswers] = useState(initialAnswers)
  const [validationError, setValidationError] = useState('')

  const formQuery = useQuery({
    queryKey: ['public-ai-onboarding', token],
    queryFn: () => api.aiOnboarding.get(token),
    enabled: Boolean(token),
    retry: false,
  })

  const submitMutation = useMutation({
    mutationFn: (payload: AiOnboardingAnswers) => api.aiOnboarding.submit(token, payload),
  })

  if (formQuery.isLoading) return <FullScreenMessage icon={<Loader2 className="h-8 w-8 animate-spin text-cyan-400" />} title="Carregando formulário..." />
  if (formQuery.isError || !formQuery.data) return <FullScreenMessage title="Formulário não encontrado" text="Confira se o endereço recebido está completo ou solicite um novo link à ATENZ." />
  if (!formQuery.data.available) return <FullScreenMessage title="Formulário indisponível" text={formQuery.data.status === 'Submitted' ? 'Este formulário já foi enviado. Obrigado pelas informações.' : 'Este link expirou ou foi substituído. Solicite um novo link à ATENZ.'} />
  if (submitMutation.isSuccess) return <FullScreenMessage icon={<div className="flex h-16 w-16 items-center justify-center rounded-full bg-emerald-500/15"><Check className="h-8 w-8 text-emerald-400" /></div>} title="Informações enviadas" text="Recebemos as informações da sua empresa. A equipe ATENZ continuará a configuração da sua IA." />

  const update = <K extends keyof AiOnboardingAnswers>(key: K, value: AiOnboardingAnswers[K]) => {
    setAnswers((current) => ({ ...current, [key]: value }))
    setValidationError('')
  }

  const toggle = (key: 'aiGoals' | 'handoffConditions' | 'departments' | 'informationSources', value: string) => {
    const selected = answers[key]
    update(key, selected.includes(value) ? selected.filter((item) => item !== value) : [...selected, value])
  }

  const advance = () => {
    const error = validateStep(step, answers)
    if (error) {
      setValidationError(error)
      return
    }
    setStep((current) => Math.min(6, current + 1))
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }

  const submit = () => {
    const error = validateStep(6, answers)
    if (error) {
      setValidationError(error)
      return
    }
    submitMutation.mutate(answers)
  }

  return (
    <div className="min-h-screen bg-[#071326] text-white">
      <header className="border-b border-white/10 bg-[#091a31]/90">
        <div className="mx-auto flex max-w-4xl items-center gap-3 px-5 py-4">
          <img src={atenzLogo} alt="ATENZ" className="h-12 w-12 object-contain" />
          <div>
            <p className="font-semibold tracking-wide">ATENZ</p>
            <p className="text-xs text-cyan-200/70">Configuração inicial da IA</p>
          </div>
        </div>
      </header>

      <main className="mx-auto max-w-4xl px-5 py-8 sm:py-12">
        {step > 0 && (
          <div className="mb-8">
            <div className="mb-2 flex items-center justify-between text-xs text-slate-400">
              <span>Etapa {step} de 6</span><span>{Math.round(step / 6 * 100)}%</span>
            </div>
            <div className="h-1.5 overflow-hidden rounded-full bg-white/10"><div className="h-full rounded-full bg-gradient-to-r from-cyan-400 to-blue-500 transition-all" style={{ width: `${step / 6 * 100}%` }} /></div>
          </div>
        )}

        <section className="rounded-3xl border border-white/10 bg-[#0b1b31] p-6 shadow-2xl shadow-black/20 sm:p-10">
          {step === 0 && <Intro tenantName={formQuery.data.tenantName} />}
          {step === 1 && <CompanyStep answers={answers} update={update} toggle={toggle} />}
          {step === 2 && <MultiQuestion title="2. Regras de atendimento" label="Quando a IA deve encaminhar o atendimento para uma pessoa?" options={handoffConditions} selected={answers.handoffConditions} onToggle={(value) => toggle('handoffConditions', value)} />}
          {step === 3 && <HoursStep answers={answers} update={update} />}
          {step === 4 && <DepartmentsStep answers={answers} update={update} toggle={toggle} />}
          {step === 5 && <WhatsAppStep answers={answers} update={update} />}
          {step === 6 && <InformationStep answers={answers} update={update} toggle={toggle} />}

          {(validationError || submitMutation.isError) && <p role="alert" className="mt-6 rounded-xl border border-red-400/30 bg-red-500/10 px-4 py-3 text-sm text-red-200">{validationError || (submitMutation.error as Error).message}</p>}

          <div className="mt-8 flex items-center justify-between gap-3">
            {step === 0 ? <Link to="/" className="text-sm text-slate-400 hover:text-white">Voltar ao site</Link> : <button type="button" onClick={() => setStep((current) => current - 1)} className="inline-flex items-center gap-2 rounded-xl px-4 py-2.5 text-sm text-slate-300 hover:bg-white/5"><ArrowLeft className="h-4 w-4" /> Voltar</button>}
            {step < 6 ? <button type="button" onClick={advance} className="inline-flex items-center gap-2 rounded-xl bg-blue-600 px-5 py-3 font-medium shadow-lg shadow-blue-950/30 hover:bg-blue-500">Avançar <ArrowRight className="h-4 w-4" /></button> : <button type="button" onClick={submit} disabled={submitMutation.isPending} className="inline-flex items-center gap-2 rounded-xl bg-emerald-500 px-5 py-3 font-semibold text-[#052014] hover:bg-emerald-400 disabled:opacity-60">{submitMutation.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />} Enviar</button>}
          </div>
        </section>
      </main>
    </div>
  )
}

function Intro({ tenantName }: { tenantName: string }) {
  return <div className="py-6 sm:py-12"><p className="mb-3 text-sm font-medium uppercase tracking-[0.2em] text-cyan-300">{tenantName}</p><h1 className="text-3xl font-bold sm:text-5xl">Configure a sua IA</h1><p className="mt-6 max-w-2xl text-lg leading-relaxed text-slate-300">Responda às perguntas de acordo com a rotina da sua empresa. Algumas perguntas permitem escolher mais de uma opção.</p><p className="mt-4 text-sm text-slate-400">Suas respostas serão enviadas com segurança para a equipe administrativa da ATENZ.</p></div>
}

type Update = <K extends keyof AiOnboardingAnswers>(key: K, value: AiOnboardingAnswers[K]) => void
type Toggle = (key: 'aiGoals' | 'handoffConditions' | 'departments' | 'informationSources', value: string) => void

function CompanyStep({ answers, update, toggle }: { answers: AiOnboardingAnswers; update: Update; toggle: Toggle }) {
  return <div><StepTitle>1. Sobre sua empresa</StepTitle><TextInput label="Qual o nome da sua empresa?" value={answers.companyName} onChange={(value) => update('companyName', value)} maxLength={200} /><SingleQuestion label="Qual a principal atividade da sua empresa?" options={businessActivities} selected={answers.businessActivity} onSelect={(value) => update('businessActivity', value)} /><TextArea label="Qual serviço ou produto você oferece?" value={answers.productsOrServices} onChange={(value) => update('productsOrServices', value)} maxLength={4000} /><CurrencyInput label="Qual o valor médio das vendas ou serviços (ticket médio)?" value={answers.averageTicket} onChange={(value) => update('averageTicket', value)} /><MultiQuestion label="O que você deseja que a IA faça no atendimento?" options={aiGoals} selected={answers.aiGoals} onToggle={(value) => toggle('aiGoals', value)} compact /></div>
}

function HoursStep({ answers, update }: { answers: AiOnboardingAnswers; update: Update }) {
  return <div><StepTitle>3. Horários de atendimento</StepTitle><SingleQuestion label="Em quais dias sua empresa atende?" options={serviceDays} selected={answers.serviceDays} onSelect={(value) => update('serviceDays', value)} /><SingleQuestion label="Qual é o horário de atendimento da sua equipe?" options={serviceHours} selected={answers.serviceHours} onSelect={(value) => update('serviceHours', value)} />{answers.serviceHours === 'Outro horário' && <TextArea label="Descreva o horário" value={answers.customServiceHours ?? ''} onChange={(value) => update('customServiceHours', value)} maxLength={500} />}<SingleQuestion label="Como a IA deve agir fora do horário de atendimento?" options={outsideHoursBehaviors} selected={answers.outsideHoursBehavior} onSelect={(value) => update('outsideHoursBehavior', value)} /></div>
}

function DepartmentsStep({ answers, update, toggle }: { answers: AiOnboardingAnswers; update: Update; toggle: Toggle }) {
  return <div><StepTitle>4. Equipe e departamentos</StepTitle><MultiQuestion label="Quais áreas ou setores existem na sua empresa?" options={departments} selected={answers.departments} onToggle={(value) => toggle('departments', value)} />{answers.departments.includes('Outros') && <TextInput label="Quais outros setores?" value={answers.otherDepartment ?? ''} onChange={(value) => update('otherDepartment', value)} maxLength={300} />}</div>
}

function WhatsAppStep({ answers, update }: { answers: AiOnboardingAnswers; update: Update }) {
  return <div><StepTitle>5. Situação do WhatsApp</StepTitle><SingleQuestion label="Qual a situação atual do seu número de WhatsApp?" options={whatsappSituations} selected={answers.whatsappSituation} onSelect={(value) => update('whatsappSituation', value)} /><SingleQuestion label="O número já sofreu bloqueio ou restrição do WhatsApp?" options={restrictionAnswers} selected={answers.previousRestriction} onSelect={(value) => update('previousRestriction', value)} />{answers.previousRestriction === 'Sim, já aconteceu' && <SingleQuestion label="Qual foi o problema?" options={restrictionTypes} selected={answers.restrictionType ?? ''} onSelect={(value) => update('restrictionType', value)} />}</div>
}

function InformationStep({ answers, update, toggle }: { answers: AiOnboardingAnswers; update: Update; toggle: Toggle }) {
  return <div><StepTitle>6. Informações da empresa</StepTitle><MultiQuestion label="Onde estão as informações sobre seus produtos e serviços?" options={informationSources} selected={answers.informationSources} onToggle={(value) => toggle('informationSources', value)} /><TextArea label="Observações sobre os materiais (opcional)" value={answers.informationNotes ?? ''} onChange={(value) => update('informationNotes', value)} maxLength={2000} placeholder="Informe links ou orientações para a equipe localizar os materiais." required={false} /><TextArea label="Existe alguma orientação importante que a IA deve seguir?" value={answers.aiInstructions} onChange={(value) => update('aiInstructions', value)} maxLength={4000} placeholder="Ex.: não atendemos aos domingos; não confirmar preço sem consultar o estoque." /><div className="mt-6 rounded-2xl border border-cyan-400/20 bg-cyan-400/5 p-4"><h3 className="font-semibold text-cyan-100">Conclusão</h3><p className="mt-2 text-sm leading-relaxed text-slate-300">As respostas serão utilizadas para preparar a configuração inicial da sua IA. Se precisarmos de informações adicionais, a equipe entrará em contato.</p></div></div>
}

function StepTitle({ children }: { children: React.ReactNode }) { return <h1 className="mb-8 text-2xl font-bold sm:text-3xl">{children}</h1> }

function SingleQuestion({ label, options, selected, onSelect }: { label: string; options: string[]; selected: string; onSelect: (value: string) => void }) {
  return <fieldset className="mb-7"><legend className="mb-3 font-semibold text-slate-100">{label} <Required /></legend><div className="grid gap-2 sm:grid-cols-2">{options.map((option) => <label key={option} className={`flex cursor-pointer items-start gap-3 rounded-xl border px-4 py-3 text-sm transition ${selected === option ? 'border-cyan-400 bg-cyan-400/10 text-white' : 'border-white/10 bg-white/[0.03] text-slate-300 hover:border-white/20'}`}><input type="radio" className="mt-0.5 accent-cyan-400" checked={selected === option} onChange={() => onSelect(option)} /><span>{option}</span></label>)}</div></fieldset>
}

function MultiQuestion({ title, label, options, selected, onToggle, compact = false }: { title?: string; label: string; options: string[]; selected: string[]; onToggle: (value: string) => void; compact?: boolean }) {
  return <div>{title && <StepTitle>{title}</StepTitle>}<fieldset className="mb-7"><legend className="mb-3 font-semibold text-slate-100">{label} <Required /></legend><div className={`grid gap-2 ${compact ? 'sm:grid-cols-2' : ''}`}>{options.map((option) => <label key={option} className={`flex cursor-pointer items-start gap-3 rounded-xl border px-4 py-3 text-sm transition ${selected.includes(option) ? 'border-cyan-400 bg-cyan-400/10 text-white' : 'border-white/10 bg-white/[0.03] text-slate-300 hover:border-white/20'}`}><input type="checkbox" className="mt-0.5 accent-cyan-400" checked={selected.includes(option)} onChange={() => onToggle(option)} /><span>{option}</span></label>)}</div></fieldset></div>
}

function TextInput({ label, value, onChange, maxLength, placeholder }: { label: string; value: string; onChange: (value: string) => void; maxLength: number; placeholder?: string }) {
  return <label className="mb-7 block"><span className="mb-2 block font-semibold text-slate-100">{label} <Required /></span><input value={value} onChange={(event) => onChange(event.target.value)} maxLength={maxLength} placeholder={placeholder} className="w-full rounded-xl border border-white/10 bg-white/[0.05] px-4 py-3 text-white outline-none placeholder:text-slate-500 focus:border-cyan-400 focus:ring-2 focus:ring-cyan-400/20" /></label>
}

function CurrencyInput({ label, value, onChange }: { label: string; value: string; onChange: (value: string) => void }) {
  const handleChange = (rawValue: string) => {
    const digits = rawValue.replace(/\D/g, '').slice(0, 15)
    onChange(digits ? brlFormatter.format(Number(digits) / 100) : '')
  }

  return <label className="mb-7 block"><span className="mb-2 block font-semibold text-slate-100">{label} <Required /></span><input value={value} onChange={(event) => handleChange(event.target.value)} inputMode="numeric" placeholder="R$ 0,00" className="w-full rounded-xl border border-white/10 bg-white/[0.05] px-4 py-3 text-white outline-none placeholder:text-slate-500 focus:border-cyan-400 focus:ring-2 focus:ring-cyan-400/20" /></label>
}

function TextArea({ label, value, onChange, maxLength, placeholder, required = true }: { label: string; value: string; onChange: (value: string) => void; maxLength: number; placeholder?: string; required?: boolean }) {
  return <label className="mb-7 block"><span className="mb-2 block font-semibold text-slate-100">{label} {required && <Required />}</span><textarea value={value} onChange={(event) => onChange(event.target.value)} maxLength={maxLength} rows={4} placeholder={placeholder} className="w-full resize-y rounded-xl border border-white/10 bg-white/[0.05] px-4 py-3 text-white outline-none placeholder:text-slate-500 focus:border-cyan-400 focus:ring-2 focus:ring-cyan-400/20" /><span className="mt-1 block text-right text-xs text-slate-500">{value.length}/{maxLength}</span></label>
}

function Required() { return <span className="text-cyan-300" aria-label="obrigatório">*</span> }

function FullScreenMessage({ icon, title, text }: { icon?: React.ReactNode; title: string; text?: string }) {
  return <div className="flex min-h-screen items-center justify-center bg-[#071326] px-5 text-white"><div className="max-w-lg text-center"><img src={atenzLogo} alt="ATENZ" className="mx-auto mb-8 h-20 w-20 object-contain" />{icon && <div className="mb-5 flex justify-center">{icon}</div>}<h1 className="text-3xl font-bold">{title}</h1>{text && <p className="mt-4 leading-relaxed text-slate-300">{text}</p>}<Link to="/" className="mt-8 inline-flex rounded-xl border border-white/15 px-5 py-3 text-sm text-slate-200 hover:bg-white/5">Ir para o site da ATENZ</Link></div></div>
}

function validateStep(step: number, value: AiOnboardingAnswers): string {
  if (step === 0) return ''
  if (step === 1 && (!value.companyName.trim() || !value.businessActivity || !value.productsOrServices.trim() || !value.averageTicket.trim() || value.aiGoals.length === 0)) return 'Preencha todos os campos obrigatórios desta etapa.'
  if (step === 2 && value.handoffConditions.length === 0) return 'Selecione ao menos uma regra de encaminhamento.'
  if (step === 3 && (!value.serviceDays || !value.serviceHours || !value.outsideHoursBehavior || (value.serviceHours === 'Outro horário' && !value.customServiceHours?.trim()))) return 'Preencha todos os campos obrigatórios desta etapa.'
  if (step === 4 && (value.departments.length === 0 || (value.departments.includes('Outros') && !value.otherDepartment?.trim()))) return 'Informe os setores da empresa.'
  if (step === 5 && (!value.whatsappSituation || !value.previousRestriction || (value.previousRestriction === 'Sim, já aconteceu' && !value.restrictionType))) return 'Preencha todos os campos obrigatórios desta etapa.'
  if (step === 6 && (value.informationSources.length === 0 || !value.aiInstructions.trim())) return 'Preencha todos os campos obrigatórios desta etapa.'
  return ''
}

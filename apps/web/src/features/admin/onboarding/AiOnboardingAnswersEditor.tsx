import { useState } from 'react'
import { Loader2, Save } from 'lucide-react'
import type { AiOnboardingAnswers } from '../../../lib/api'
import {
  aiGoals,
  businessActivities,
  departments,
  handoffConditions,
  informationSources,
  outsideHoursBehaviors,
  restrictionAnswers,
  restrictionTypes,
  serviceDays,
  serviceHours,
  whatsappSituations,
} from '../../onboarding/aiOnboardingOptions'

export function AiOnboardingAnswersEditor({ initialValue, saving, error, onCancel, onSave }: {
  initialValue: AiOnboardingAnswers
  saving: boolean
  error?: string
  onCancel: () => void
  onSave: (answers: AiOnboardingAnswers) => void
}) {
  const [value, setValue] = useState<AiOnboardingAnswers>(initialValue)
  const set = <K extends keyof AiOnboardingAnswers>(key: K, next: AiOnboardingAnswers[K]) =>
    setValue((current) => ({ ...current, [key]: next }))
  const toggle = (key: 'aiGoals' | 'handoffConditions' | 'departments' | 'informationSources', option: string) => {
    const selected = value[key]
    set(key, selected.includes(option) ? selected.filter((item) => item !== option) : [...selected, option])
  }

  return <div className="space-y-6">
    <Section title="Empresa">
      <Input label="Nome da empresa" value={value.companyName} maxLength={200} onChange={(next) => set('companyName', next)} />
      <Select label="Atividade principal" value={value.businessActivity} options={businessActivities} onChange={(next) => set('businessActivity', next)} />
      <TextArea label="Produtos e serviços" value={value.productsOrServices} maxLength={4000} onChange={(next) => set('productsOrServices', next)} />
      <Input label="Ticket médio" value={value.averageTicket} maxLength={100} onChange={(next) => set('averageTicket', next)} />
      <Checks label="Objetivos da IA" options={aiGoals} selected={value.aiGoals} onToggle={(option) => toggle('aiGoals', option)} />
    </Section>
    <Section title="Atendimento e encaminhamento">
      <Checks label="Encaminhamento humano" options={handoffConditions} selected={value.handoffConditions} onToggle={(option) => toggle('handoffConditions', option)} />
      <Select label="Dias de atendimento" value={value.serviceDays} options={serviceDays} onChange={(next) => set('serviceDays', next)} />
      <Select label="Horário" value={value.serviceHours} options={serviceHours} onChange={(next) => set('serviceHours', next)} />
      {value.serviceHours === 'Outro horário' && <TextArea label="Detalhes do horário" value={value.customServiceHours ?? ''} maxLength={500} onChange={(next) => set('customServiceHours', next)} />}
      <Select label="Comportamento fora do expediente" value={value.outsideHoursBehavior} options={outsideHoursBehaviors} onChange={(next) => set('outsideHoursBehavior', next)} />
    </Section>
    <Section title="Equipe e WhatsApp">
      <Checks label="Setores" options={departments} selected={value.departments} onToggle={(option) => toggle('departments', option)} />
      {value.departments.includes('Outros') && <Input label="Outros setores" value={value.otherDepartment ?? ''} maxLength={300} onChange={(next) => set('otherDepartment', next)} />}
      <Select label="Situação do WhatsApp" value={value.whatsappSituation} options={whatsappSituations} onChange={(next) => set('whatsappSituation', next)} />
      <Select label="Bloqueios anteriores" value={value.previousRestriction} options={restrictionAnswers} onChange={(next) => set('previousRestriction', next)} />
      {value.previousRestriction === 'Sim, já aconteceu' && <Select label="Problema ocorrido" value={value.restrictionType ?? ''} options={restrictionTypes} onChange={(next) => set('restrictionType', next)} />}
    </Section>
    <Section title="Informações para a IA">
      <Checks label="Fontes de informação" options={informationSources} selected={value.informationSources} onToggle={(option) => toggle('informationSources', option)} />
      <TextArea label="Observações dos materiais" value={value.informationNotes ?? ''} maxLength={2000} onChange={(next) => set('informationNotes', next)} />
      <TextArea label="Orientações para a IA" value={value.aiInstructions} maxLength={4000} onChange={(next) => set('aiInstructions', next)} />
    </Section>
    {error && <p className="rounded-xl border border-red-400/30 bg-red-500/10 px-4 py-3 text-sm text-red-200">{error}</p>}
    <div className="sticky bottom-0 flex justify-end gap-3 border-t border-white/10 bg-[#0b1b31] py-4">
      <button type="button" onClick={onCancel} disabled={saving} className="rounded-lg border border-white/15 px-4 py-2 text-sm hover:bg-white/5">Cancelar</button>
      <button type="button" onClick={() => onSave(value)} disabled={saving} className="inline-flex items-center gap-2 rounded-lg bg-blue-600 px-4 py-2 text-sm font-semibold hover:bg-blue-500 disabled:opacity-50">{saving ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />} Salvar alterações</button>
    </div>
  </div>
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return <section className="rounded-xl border border-white/10 p-4"><h3 className="mb-4 font-semibold text-cyan-200">{title}</h3><div className="grid gap-4 sm:grid-cols-2">{children}</div></section>
}

function Input({ label, value, maxLength, onChange }: { label: string; value: string; maxLength: number; onChange: (value: string) => void }) {
  return <label className="block"><span className="mb-1.5 block text-sm text-slate-300">{label}</span><input value={value} maxLength={maxLength} onChange={(event) => onChange(event.target.value)} className="w-full rounded-lg border border-white/10 bg-[#071326] px-3 py-2 text-sm outline-none focus:border-cyan-400" /></label>
}

function TextArea({ label, value, maxLength, onChange }: { label: string; value: string; maxLength: number; onChange: (value: string) => void }) {
  return <label className="block sm:col-span-2"><span className="mb-1.5 block text-sm text-slate-300">{label}</span><textarea value={value} maxLength={maxLength} rows={3} onChange={(event) => onChange(event.target.value)} className="w-full rounded-lg border border-white/10 bg-[#071326] px-3 py-2 text-sm outline-none focus:border-cyan-400" /></label>
}

function Select({ label, value, options, onChange }: { label: string; value: string; options: string[]; onChange: (value: string) => void }) {
  return <label className="block"><span className="mb-1.5 block text-sm text-slate-300">{label}</span><select value={value} onChange={(event) => onChange(event.target.value)} className="w-full rounded-lg border border-white/10 bg-[#071326] px-3 py-2 text-sm outline-none focus:border-cyan-400"><option value="">Selecione</option>{options.map((option) => <option key={option}>{option}</option>)}</select></label>
}

function Checks({ label, options, selected, onToggle }: { label: string; options: string[]; selected: string[]; onToggle: (option: string) => void }) {
  return <fieldset className="sm:col-span-2"><legend className="mb-2 text-sm text-slate-300">{label}</legend><div className="grid gap-2 sm:grid-cols-2">{options.map((option) => <label key={option} className="flex items-start gap-2 rounded-lg border border-white/10 bg-white/[0.025] p-2.5 text-xs"><input type="checkbox" checked={selected.includes(option)} onChange={() => onToggle(option)} className="mt-0.5 accent-cyan-400" /><span>{option}</span></label>)}</div></fieldset>
}

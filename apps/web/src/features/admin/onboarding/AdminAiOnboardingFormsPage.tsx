import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Check, Clipboard, ExternalLink, FileText, Link2, Loader2, RefreshCw, X } from 'lucide-react'
import { api, type AiOnboardingAnswers, type AiOnboardingFormSummary } from '../../../lib/api'

const statusLabels: Record<string, { label: string; className: string }> = {
  Pending: { label: 'Aguardando resposta', className: 'bg-amber-500/15 text-amber-300' },
  Submitted: { label: 'Respondido', className: 'bg-emerald-500/15 text-emerald-300' },
  Revoked: { label: 'Substituído', className: 'bg-slate-500/15 text-slate-300' },
  Expired: { label: 'Expirado', className: 'bg-red-500/15 text-red-300' },
}

export function AdminAiOnboardingFormsPage() {
  const queryClient = useQueryClient()
  const [tenantId, setTenantId] = useState('')
  const [generatedLink, setGeneratedLink] = useState('')
  const [copied, setCopied] = useState(false)
  const [selectedForm, setSelectedForm] = useState<AiOnboardingFormSummary | null>(null)

  const formsQuery = useQuery({ queryKey: ['admin-ai-onboarding-forms'], queryFn: api.admin.aiOnboardingForms.list })
  const tenantsQuery = useQuery({ queryKey: ['admin-tenants'], queryFn: api.admin.tenants.list })
  const detailQuery = useQuery({
    queryKey: ['admin-ai-onboarding-form', selectedForm?.id],
    queryFn: () => api.admin.aiOnboardingForms.get(selectedForm!.id),
    enabled: Boolean(selectedForm),
  })

  const createMutation = useMutation({
    mutationFn: () => api.admin.aiOnboardingForms.create(tenantId),
    onSuccess: (result) => {
      setGeneratedLink(`${window.location.origin}/configurar-ia#${result.token}`)
      setCopied(false)
      queryClient.invalidateQueries({ queryKey: ['admin-ai-onboarding-forms'] })
    },
  })

  const copyLink = async () => {
    await navigator.clipboard.writeText(generatedLink)
    setCopied(true)
  }

  return (
    <div className="min-h-full p-4 text-white sm:p-6 lg:p-8">
      <div className="mx-auto max-w-7xl">
        <div className="mb-8">
          <p className="text-sm font-medium uppercase tracking-[0.18em] text-cyan-300">Administração</p>
          <h1 className="mt-2 text-2xl font-bold sm:text-3xl">Formulários de configuração da IA</h1>
          <p className="mt-2 max-w-3xl text-sm text-slate-400">Gere um link seguro para cada empresa e consulte as respostas enviadas. Um novo link substitui qualquer link pendente anterior da mesma empresa.</p>
        </div>

        <section className="mb-8 rounded-2xl border border-white/10 bg-[#0b1b31]/90 p-5 sm:p-6">
          <div className="flex items-center gap-3"><div className="rounded-xl bg-cyan-500/10 p-2.5"><Link2 className="h-5 w-5 text-cyan-300" /></div><div><h2 className="font-semibold">Gerar link do formulário</h2><p className="text-xs text-slate-400">O link fica válido por 30 dias e aceita um único envio.</p></div></div>
          <div className="mt-5 flex flex-col gap-3 sm:flex-row">
            <select aria-label="Empresa" value={tenantId} onChange={(event) => { setTenantId(event.target.value); setGeneratedLink('') }} className="min-w-0 flex-1 rounded-xl border border-white/10 bg-[#071326] px-4 py-3 text-sm text-white outline-none focus:border-cyan-400">
              <option value="">Selecione uma empresa</option>
              {(tenantsQuery.data ?? []).filter((tenant) => tenant.status !== 'Closed').map((tenant) => <option key={tenant.id} value={tenant.id}>{tenant.name}</option>)}
            </select>
            <button type="button" disabled={!tenantId || createMutation.isPending} onClick={() => createMutation.mutate()} className="inline-flex items-center justify-center gap-2 rounded-xl bg-blue-600 px-5 py-3 text-sm font-semibold hover:bg-blue-500 disabled:cursor-not-allowed disabled:opacity-50">{createMutation.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Link2 className="h-4 w-4" />} Gerar novo link</button>
          </div>
          {createMutation.isError && <p className="mt-3 text-sm text-red-300">{(createMutation.error as Error).message}</p>}
          {generatedLink && <div className="mt-4 rounded-xl border border-emerald-400/20 bg-emerald-400/5 p-4"><p className="mb-2 text-xs font-medium uppercase tracking-wide text-emerald-300">Link criado — copie agora</p><div className="flex flex-col gap-2 sm:flex-row"><input aria-label="Link gerado" readOnly value={generatedLink} className="min-w-0 flex-1 rounded-lg border border-white/10 bg-black/20 px-3 py-2 text-sm text-slate-200" /><button type="button" onClick={copyLink} className="inline-flex items-center justify-center gap-2 rounded-lg border border-white/15 px-4 py-2 text-sm hover:bg-white/5">{copied ? <Check className="h-4 w-4 text-emerald-300" /> : <Clipboard className="h-4 w-4" />}{copied ? 'Copiado' : 'Copiar'}</button><a href={generatedLink} target="_blank" rel="noreferrer" className="inline-flex items-center justify-center gap-2 rounded-lg border border-white/15 px-4 py-2 text-sm hover:bg-white/5"><ExternalLink className="h-4 w-4" /> Abrir</a></div><p className="mt-2 text-xs text-slate-400">Por segurança, o link completo aparece somente agora. Gere outro caso precise substituí-lo.</p></div>}
        </section>

        <section className="overflow-hidden rounded-2xl border border-white/10 bg-[#0b1b31]/90">
          <div className="flex items-center justify-between border-b border-white/10 px-5 py-4"><div><h2 className="font-semibold">Histórico de formulários</h2><p className="text-xs text-slate-400">Somente administradores da plataforma podem acessar esta lista.</p></div><button type="button" onClick={() => formsQuery.refetch()} aria-label="Atualizar" className="rounded-lg p-2 text-slate-400 hover:bg-white/5 hover:text-white"><RefreshCw className={`h-4 w-4 ${formsQuery.isFetching ? 'animate-spin' : ''}`} /></button></div>
          {formsQuery.isLoading ? <div className="flex justify-center py-16"><Loader2 className="h-6 w-6 animate-spin text-cyan-300" /></div> : formsQuery.isError ? <p className="p-6 text-sm text-red-300">Não foi possível carregar os formulários.</p> : (formsQuery.data?.length ?? 0) === 0 ? <div className="px-6 py-16 text-center"><FileText className="mx-auto h-10 w-10 text-slate-600" /><p className="mt-3 text-sm text-slate-400">Nenhum formulário foi gerado.</p></div> : <div className="overflow-x-auto"><table className="min-w-full"><thead><tr className="border-b border-white/10 text-left text-xs uppercase tracking-wide text-slate-500"><th className="px-5 py-3">Empresa</th><th className="px-5 py-3">Status</th><th className="px-5 py-3">Criado</th><th className="px-5 py-3">Respondido</th><th className="px-5 py-3 text-right">Ação</th></tr></thead><tbody className="divide-y divide-white/5">{formsQuery.data?.map((form) => { const status = statusLabels[form.status] ?? statusLabels.Pending; return <tr key={form.id} className="hover:bg-white/[0.025]"><td className="px-5 py-4"><p className="font-medium text-slate-100">{form.companyName || form.tenantName}</p><p className="text-xs text-slate-500">{form.tenantName}</p></td><td className="px-5 py-4"><span className={`rounded-full px-2.5 py-1 text-xs font-medium ${status.className}`}>{status.label}</span></td><td className="whitespace-nowrap px-5 py-4 text-sm text-slate-400">{formatDate(form.createdAt)}</td><td className="whitespace-nowrap px-5 py-4 text-sm text-slate-400">{form.submittedAt ? formatDate(form.submittedAt) : '—'}</td><td className="px-5 py-4 text-right"><button type="button" disabled={form.status !== 'Submitted'} onClick={() => setSelectedForm(form)} className="rounded-lg border border-white/10 px-3 py-2 text-xs font-medium text-cyan-200 hover:bg-white/5 disabled:cursor-not-allowed disabled:opacity-35">Ver respostas</button></td></tr>})}</tbody></table></div>}
        </section>
      </div>

      {selectedForm && <DetailDialog loading={detailQuery.isLoading} answers={detailQuery.data?.answers} tenantName={selectedForm.tenantName} onClose={() => setSelectedForm(null)} />}
    </div>
  )
}

function DetailDialog({ loading, answers, tenantName, onClose }: { loading: boolean; answers?: AiOnboardingAnswers; tenantName: string; onClose: () => void }) {
  return <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4 backdrop-blur-sm"><div role="dialog" aria-modal="true" aria-label="Respostas do formulário" className="max-h-[92vh] w-full max-w-4xl overflow-hidden rounded-2xl border border-white/10 bg-[#0b1b31] shadow-2xl"><div className="flex items-center justify-between border-b border-white/10 px-6 py-4"><div><h2 className="text-lg font-semibold">Respostas do formulário</h2><p className="text-sm text-slate-400">{tenantName}</p></div><button type="button" onClick={onClose} aria-label="Fechar" className="rounded-lg p-2 text-slate-400 hover:bg-white/5 hover:text-white"><X className="h-5 w-5" /></button></div><div className="max-h-[calc(92vh-80px)] overflow-y-auto p-6">{loading ? <div className="flex justify-center py-16"><Loader2 className="h-6 w-6 animate-spin text-cyan-300" /></div> : answers ? <AnswersView answers={answers} /> : <p className="text-sm text-red-300">Não foi possível carregar as respostas.</p>}</div></div></div>
}

function AnswersView({ answers }: { answers: AiOnboardingAnswers }) {
  const fields: Array<[string, string | string[] | undefined]> = [
    ['Nome da empresa', answers.companyName], ['Atividade principal', answers.businessActivity],
    ['Produtos e serviços', answers.productsOrServices], ['Ticket médio', answers.averageTicket],
    ['Objetivos da IA', answers.aiGoals], ['Encaminhamento humano', answers.handoffConditions],
    ['Dias de atendimento', answers.serviceDays], ['Horário', answers.serviceHours],
    ['Detalhes do horário', answers.customServiceHours], ['Fora do expediente', answers.outsideHoursBehavior],
    ['Setores', answers.departments], ['Outros setores', answers.otherDepartment],
    ['Situação do WhatsApp', answers.whatsappSituation], ['Bloqueios anteriores', answers.previousRestriction],
    ['Problema ocorrido', answers.restrictionType], ['Fontes de informação', answers.informationSources],
    ['Observações dos materiais', answers.informationNotes], ['Orientações para a IA', answers.aiInstructions],
  ]
  return <dl className="grid gap-4 sm:grid-cols-2">{fields.filter(([, value]) => Array.isArray(value) ? value.length > 0 : Boolean(value)).map(([label, value]) => <div key={label} className="rounded-xl border border-white/10 bg-white/[0.025] p-4"><dt className="text-xs font-semibold uppercase tracking-wide text-cyan-300/80">{label}</dt><dd className="mt-2 whitespace-pre-wrap text-sm leading-relaxed text-slate-200">{Array.isArray(value) ? value.join('\n') : value}</dd></div>)}</dl>
}

function formatDate(value: string) { return new Date(value).toLocaleString('pt-BR') }

import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { RefreshCw, Send, FileText } from 'lucide-react'
import { api } from '../../../../lib/api'

function parameterCount(body: string) {
  return [...new Set([...body.matchAll(/\{\{([1-9]\d*)\}\}/g)].map(match => Number(match[1])))].length
}

export function WhatsAppTemplatesPage() {
  const queryClient = useQueryClient()
  const [line, setLine] = useState(1)
  const [name, setName] = useState('')
  const [language, setLanguage] = useState('pt_BR')
  const [category, setCategory] = useState<'UTILITY' | 'MARKETING'>('UTILITY')
  const [bodyText, setBodyText] = useState('')
  const [examples, setExamples] = useState<string[]>([])
  const [footerText, setFooterText] = useState('')
  const [message, setMessage] = useState<string | null>(null)
  const count = useMemo(() => parameterCount(bodyText), [bodyText])
  const { data: lines = [] } = useQuery({ queryKey: ['whatsapp-lines'], queryFn: api.whatsapp.getLines })
  const { data, isLoading, error } = useQuery({
    queryKey: ['whatsapp-templates', line],
    queryFn: () => api.whatsapp.listTemplates(line),
    refetchInterval: query => query.state.data?.templates.some(template => template.reviewStatus === 'PENDING') ? 5000 : false,
  })
  const create = useMutation({
    mutationFn: () => api.whatsapp.createTemplate(line, crypto.randomUUID(), {
      name, language, category, bodyText, bodyExamples: examples, footerText: footerText || undefined,
    }),
    onSuccess: () => {
      setMessage('Template enviado para análise da Meta.')
      setName(''); setBodyText(''); setExamples([]); setFooterText('')
      queryClient.invalidateQueries({ queryKey: ['whatsapp-templates', line] })
    },
  })
  const sync = useMutation({
    mutationFn: () => api.whatsapp.syncTemplates(line),
    onSuccess: () => { setMessage('Catálogo sincronizado.'); queryClient.invalidateQueries({ queryKey: ['whatsapp-templates', line] }) },
  })
  const officialLines = lines.filter(item => item.connectionType === 'OfficialApi' && item.isActive)

  return <div className="mx-auto max-w-6xl space-y-6 p-4 sm:p-6">
    <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-center">
      <div><h1 className="text-2xl font-bold text-slate-900">Templates do WhatsApp</h1><p className="mt-1 text-sm text-slate-600">Crie modelos textuais e acompanhe a análise da Meta.</p></div>
      <button onClick={() => sync.mutate()} disabled={sync.isPending || officialLines.length === 0} className="inline-flex items-center justify-center gap-2 rounded-lg border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 disabled:opacity-50"><RefreshCw className="h-4 w-4" /> Sincronizar</button>
    </div>
    {message && <p role="status" className="rounded-lg bg-emerald-50 p-3 text-sm text-emerald-800">{message}</p>}
    <section className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
      <div className="mb-4 flex items-center gap-2"><FileText className="h-5 w-5 text-emerald-600" /><h2 className="font-semibold text-slate-900">Novo template</h2></div>
      {officialLines.length === 0 ? <p className="text-sm text-slate-600">Configure uma linha oficial ativa antes de cadastrar templates.</p> : <form className="grid gap-4 md:grid-cols-2" onSubmit={event => { event.preventDefault(); setMessage(null); create.mutate() }}>
        <label className="text-sm font-medium text-slate-700">Linha oficial<select value={line} onChange={event => setLine(Number(event.target.value))} className="mt-1 w-full rounded-lg border border-slate-300 p-2">{officialLines.map(item => <option key={item.lineNumber} value={item.lineNumber}>Linha {item.lineNumber}</option>)}</select></label>
        <label className="text-sm font-medium text-slate-700">Nome<input required pattern="[a-z0-9_]+" value={name} onChange={event => setName(event.target.value)} className="mt-1 w-full rounded-lg border border-slate-300 p-2" placeholder="atualizacao_atendimento" /></label>
        <label className="text-sm font-medium text-slate-700">Idioma<input required value={language} onChange={event => setLanguage(event.target.value)} className="mt-1 w-full rounded-lg border border-slate-300 p-2" /></label>
        <label className="text-sm font-medium text-slate-700">Categoria<select value={category} onChange={event => setCategory(event.target.value as 'UTILITY' | 'MARKETING')} className="mt-1 w-full rounded-lg border border-slate-300 p-2"><option value="UTILITY">Utilidade</option><option value="MARKETING">Marketing</option></select></label>
        <label className="md:col-span-2 text-sm font-medium text-slate-700">Corpo<textarea required maxLength={1024} value={bodyText} onChange={event => { setBodyText(event.target.value); const next = parameterCount(event.target.value); setExamples(current => Array.from({ length: next }, (_, index) => current[index] ?? '')) }} className="mt-1 min-h-28 w-full rounded-lg border border-slate-300 p-2" placeholder="Olá, {{1}}. Seu protocolo {{2}} foi atualizado." /><span className="text-xs text-slate-500">{bodyText.length}/1024 · {count} variável(is)</span></label>
        {examples.map((example, index) => <label key={index} className="text-sm font-medium text-slate-700">Exemplo para {`{{${index + 1}}}`}<input required value={example} onChange={event => setExamples(current => current.map((item, itemIndex) => itemIndex === index ? event.target.value : item))} className="mt-1 w-full rounded-lg border border-slate-300 p-2" /></label>)}
        <label className="md:col-span-2 text-sm font-medium text-slate-700">Rodapé opcional<input maxLength={60} value={footerText} onChange={event => setFooterText(event.target.value)} className="mt-1 w-full rounded-lg border border-slate-300 p-2" /></label>
        {create.error && <p role="alert" className="md:col-span-2 text-sm text-red-700">{create.error.message}</p>}
        <button type="submit" disabled={create.isPending} className="inline-flex w-fit items-center gap-2 rounded-lg bg-emerald-600 px-4 py-2 text-sm font-semibold text-white disabled:opacity-50"><Send className="h-4 w-4" /> Enviar para análise</button>
      </form>}
    </section>
    <section className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm"><h2 className="font-semibold text-slate-900">Catálogo da linha {line}</h2>{isLoading ? <p className="mt-4 text-sm text-slate-600">Carregando…</p> : error ? <p role="alert" className="mt-4 text-sm text-red-700">{error.message}</p> : <div className="mt-4 overflow-x-auto"><table className="w-full text-left text-sm"><thead className="border-b text-xs uppercase text-slate-500"><tr><th className="p-2">Template</th><th className="p-2">Categoria</th><th className="p-2">Estado</th><th className="p-2">Compatibilidade</th></tr></thead><tbody>{data?.templates.map(template => <tr key={template.id} className="border-b last:border-0"><td className="p-2"><p className="font-medium text-slate-800">{template.name}</p><p className="text-xs text-slate-500">{template.language}</p></td><td className="p-2">{template.effectiveCategory}</td><td className="p-2">{template.reviewStatus}{template.rejectionReason && <p className="mt-1 text-xs text-red-700">{template.rejectionReason}</p>}</td><td className="p-2 text-xs">Inbox: {template.isInboxCompatible ? 'compatível' : 'indisponível'}<br />Disparo: {template.isBroadcastCompatible ? 'compatível' : 'indisponível'}</td></tr>)}{data?.templates.length === 0 && <tr><td colSpan={4} className="p-6 text-center text-slate-500">Nenhum template encontrado.</td></tr>}</tbody></table></div>}</section>
  </div>
}

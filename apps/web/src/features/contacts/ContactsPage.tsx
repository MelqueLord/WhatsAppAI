import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Users, Plus, Search, X, Loader2, MessageSquare, Pencil, Upload } from 'lucide-react'
import { useNavigate } from 'react-router-dom'
import { api } from '../../lib/api'
import { useAuth } from '../../lib/auth'

interface Contact {
  id: string
  phoneNumber: string
  name?: string
  lastMessageAt?: string
  createdAt: string
}

function maskBrazilPhone(value: string) {
  const digits = value.replace(/\D/g, '').slice(0, 11)
  if (digits.length <= 2) return digits
  if (digits.length <= 7) return `(${digits.slice(0, 2)}) ${digits.slice(2)}`
  return `(${digits.slice(0, 2)}) ${digits.slice(2, 7)}-${digits.slice(7)}`
}

function normalizeBrazilPhone(value: string) {
  const digits = value.replace(/\D/g, '')
  return digits.startsWith('55') ? digits : `55${digits}`
}

export function ContactsPage() {
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const { isTenantOwner } = useAuth()
  const [search, setSearch] = useState('')
  const [showCreateForm, setShowCreateForm] = useState(false)
  const [showImportForm, setShowImportForm] = useState(false)
  const [importFile, setImportFile] = useState<File | null>(null)
  const [phoneNumber, setPhoneNumber] = useState('')
  const [editTarget, setEditTarget] = useState<Contact | null>(null)
  const [memoryKey, setMemoryKey] = useState('')
  const [memoryValue, setMemoryValue] = useState('')

  const { data: contacts, isLoading } = useQuery({
    queryKey: ['contacts'],
    queryFn: () => api.contacts.list(undefined, 100),
  })

  const { data: contactMemory, isLoading: isLoadingMemory, isError: isMemoryError } = useQuery({
    queryKey: ['contact-memory', editTarget?.id],
    queryFn: () => api.contacts.memory.list(editTarget!.id),
    enabled: !!editTarget,
  })

  const createMutation = useMutation({
    mutationFn: (data: { phoneNumber: string; name?: string; startConversation?: boolean }) =>
      api.contacts.create(data),
    onSuccess: (data) => {
      queryClient.setQueryData<Contact[]>(['contacts'], (current = []) => [
        data,
        ...current.filter((contact) => contact.id !== data.id),
      ])
      queryClient.invalidateQueries({ queryKey: ['contacts'] })
      setShowCreateForm(false)
      setPhoneNumber('')
      if (data?.conversationId) {
        navigate('/inbox', { state: { conversationId: data.conversationId } })
      }
    },
  })

  const updateMutation = useMutation({
    mutationFn: ({ id, data }: { id: string; data: { phoneNumber?: string; name?: string } }) =>
      api.contacts.update(id, data),
    onSuccess: (updated) => {
      queryClient.setQueryData<Contact[]>(['contacts'], (current = []) =>
        current.map((c) => (c.id === updated.id ? updated : c))
      )
      setEditTarget(null)
    },
  })

  const saveMemoryMutation = useMutation({
    mutationFn: () => api.contacts.memory.save(editTarget!.id, {
      key: memoryKey,
      value: memoryValue,
    }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['contact-memory', editTarget?.id] })
      setMemoryKey('')
      setMemoryValue('')
    },
  })

  const removeMemoryMutation = useMutation({
    mutationFn: (memoryId: string) => api.contacts.memory.remove(editTarget!.id, memoryId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['contact-memory', editTarget?.id] })
    },
  })

  const importMutation = useMutation({
    mutationFn: (file: File) => api.contacts.import(file),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['contacts'] })
    },
  })

  const closeImportForm = () => {
    setShowImportForm(false)
    setImportFile(null)
    importMutation.reset()
  }

  const startConversationMutation = useMutation({
    mutationFn: (contactId: string) => api.contacts.startConversation(contactId),
    onSuccess: (data) => {
      if (data?.conversationId) {
        navigate('/inbox', { state: { conversationId: data.conversationId } })
      }
    },
  })

  const handleCreate = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    const formData = new FormData(e.currentTarget)
    createMutation.mutate({
      phoneNumber: normalizeBrazilPhone(phoneNumber),
      name: (formData.get('name') as string) || undefined,
      startConversation: formData.get('startConversation') === 'on',
    })
  }

  const filteredContacts = (contacts ?? []).filter((c) =>
    (c.name ?? '').toLowerCase().includes(search.toLowerCase()) ||
    c.phoneNumber.includes(search)
  )

  return (
    <div className="h-full flex flex-col bg-slate-50">
      <div className="bg-white border-b border-slate-200 px-4 sm:px-6 py-4">
        <div className="flex items-center justify-between gap-3">
          <div className="min-w-0">
            <h1 className="text-xl font-semibold text-slate-800">Contatos</h1>
            <p className="text-sm text-slate-500 mt-0.5">Gerencie seus contatos</p>
          </div>
          <div className="flex items-center gap-2">
            {isTenantOwner && (
              <button
                onClick={() => setShowImportForm(true)}
                className="flex items-center gap-2 px-3 sm:px-4 py-2.5 border border-slate-200 text-slate-700 rounded-xl hover:bg-slate-50 transition-colors whitespace-nowrap"
              >
                <Upload className="w-4 h-4" /> <span className="hidden sm:inline">Importar</span>
              </button>
            )}
            <button
              onClick={() => setShowCreateForm(true)}
              className="flex items-center gap-2 px-3 sm:px-4 py-2.5 bg-emerald-500 text-white rounded-xl hover:bg-emerald-600 transition-colors whitespace-nowrap"
            >
              <Plus className="w-4 h-4" /> <span className="hidden sm:inline">Novo Contato</span>
            </button>
          </div>
        </div>
      </div>

      <div className="flex-1 overflow-auto p-4 sm:p-6">
        <div className="mb-4">
          <div className="relative">
            <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 w-4 h-4 text-slate-400" />
            <input
              type="text"
              placeholder="Buscar contatos..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="w-full pl-10 pr-4 py-2.5 bg-white border border-slate-200 rounded-xl text-sm focus:ring-2 focus:ring-emerald-500 focus:border-transparent"
            />
          </div>
        </div>

        {isLoading ? (
          <div className="flex items-center justify-center py-12">
            <Loader2 className="w-6 h-6 text-emerald-500 animate-spin" />
          </div>
        ) : (
          <div className="bg-white rounded-xl border border-slate-200 overflow-hidden">
            <div className="overflow-x-auto">
              <table className="min-w-full">
                <thead>
                  <tr className="bg-slate-50 border-b border-slate-200">
                    <th className="px-4 sm:px-6 py-3 text-left text-xs font-semibold text-slate-500 uppercase">Nome</th>
                    <th className="px-4 sm:px-6 py-3 text-left text-xs font-semibold text-slate-500 uppercase">Telefone</th>
                    <th className="hidden sm:table-cell px-6 py-3 text-left text-xs font-semibold text-slate-500 uppercase">Última msg</th>
                    <th className="px-4 sm:px-6 py-3 text-right text-xs font-semibold text-slate-500 uppercase">Ações</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {filteredContacts.length === 0 ? (
                    <tr>
                      <td colSpan={4} className="px-6 py-12 text-center text-slate-500">
                        Nenhum contato cadastrado.
                      </td>
                    </tr>
                  ) : (
                    filteredContacts.map((contact) => (
                      <tr key={contact.id} className="hover:bg-slate-50">
                        <td className="px-4 sm:px-6 py-4">
                          <div className="flex items-center gap-3">
                            <div className="w-8 h-8 sm:w-9 sm:h-9 rounded-full bg-emerald-100 flex items-center justify-center flex-shrink-0">
                              <Users className="w-4 h-4 text-emerald-600" />
                            </div>
                            <span className="font-medium text-slate-800 text-sm truncate max-w-[100px] sm:max-w-none">{contact.name || 'Sem nome'}</span>
                          </div>
                        </td>
                        <td className="px-4 sm:px-6 py-4 text-sm text-slate-500 whitespace-nowrap">{contact.phoneNumber}</td>
                        <td className="hidden sm:table-cell px-6 py-4 text-sm text-slate-500">
                          {contact.lastMessageAt ? new Date(contact.lastMessageAt).toLocaleDateString('pt-BR') : '—'}
                        </td>
                        <td className="px-4 sm:px-6 py-4 text-right">
                          <div className="flex items-center justify-end gap-1">
                            <button
                              onClick={() => {
                                setEditTarget(contact)
                                setMemoryKey('')
                                setMemoryValue('')
                                saveMemoryMutation.reset()
                                removeMemoryMutation.reset()
                              }}
                              className="inline-flex items-center gap-1.5 px-3 py-1.5 text-sm text-slate-600 hover:bg-slate-100 rounded-lg"
                              title="Editar contato"
                            >
                              <Pencil className="w-4 h-4" />
                              <span className="hidden sm:inline">Editar</span>
                            </button>
                            <button
                              onClick={() => startConversationMutation.mutate(contact.id)}
                              className="inline-flex items-center gap-1.5 px-3 py-1.5 text-sm text-emerald-600 hover:bg-emerald-50 rounded-lg whitespace-nowrap"
                            >
                              <MessageSquare className="w-4 h-4" />
                              <span className="hidden sm:inline">Conversar</span>
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </div>
        )}
      </div>

      {showImportForm && (
        <div className="fixed inset-0 bg-black/50 backdrop-blur-sm flex items-center justify-center z-50 p-4">
          <div className="bg-white rounded-2xl p-6 w-full max-w-lg shadow-2xl">
            <div className="flex items-center justify-between mb-5">
              <div>
                <h2 className="text-lg font-semibold text-slate-800">Importar contatos</h2>
                <p className="text-sm text-slate-500 mt-1">CSV ou XLSX com as colunas nome e contato.</p>
              </div>
              <button onClick={closeImportForm} className="p-2 hover:bg-slate-100 rounded-lg">
                <X className="w-5 h-5 text-slate-400" />
              </button>
            </div>

            <div className="mb-4 rounded-xl bg-slate-50 border border-slate-200 p-3 text-sm text-slate-600">
              O contato deve ter de 8 a 15 dígitos, incluindo o código do país. Máximo de 5.000 contatos e 2 MB.{` `}
              <a
                href={'data:text/csv;charset=utf-8,nome%2Ccontato%0ACliente%20Exemplo%2C5511999999999'}
                download="modelo-contatos.csv"
                className="text-emerald-600 font-medium hover:underline"
              >
                Baixar modelo
              </a>
            </div>

            {importMutation.isError && (
              <div className="mb-4 p-3 bg-red-50 border border-red-200 rounded-xl text-sm text-red-700">
                {(importMutation.error as Error).message}
              </div>
            )}

            {importMutation.data && (
              <div className="mb-4 p-4 bg-emerald-50 border border-emerald-200 rounded-xl text-sm text-slate-700">
                <p className="font-medium text-emerald-800">Importação concluída</p>
                <p className="mt-1">
                  {importMutation.data.imported} importados, {importMutation.data.skipped} ignorados e {importMutation.data.invalid} inválidos.
                </p>
                {importMutation.data.errors.length > 0 && (
                  <ul className="mt-2 max-h-32 overflow-auto list-disc pl-5 text-red-700">
                    {importMutation.data.errors.slice(0, 20).map((error) => (
                      <li key={`${error.row}-${error.code}`}>Linha {error.row}: {error.message}</li>
                    ))}
                  </ul>
                )}
              </div>
            )}

            <form
              onSubmit={(event) => {
                event.preventDefault()
                if (importFile) importMutation.mutate(importFile)
              }}
              className="space-y-4"
            >
              <div>
                <label htmlFor="contacts-import-file" className="block text-sm font-medium text-slate-700 mb-1.5">Arquivo *</label>
                <input
                  id="contacts-import-file"
                  type="file"
                  accept=".csv,.xlsx"
                  required
                  onChange={(event) => setImportFile(event.target.files?.[0] ?? null)}
                  className="block w-full text-sm text-slate-600 file:mr-3 file:rounded-lg file:border-0 file:bg-slate-100 file:px-3 file:py-2 file:text-slate-700"
                />
              </div>
              <div className="flex justify-end gap-3 pt-2">
                <button type="button" onClick={closeImportForm} className="px-4 py-2.5 border border-slate-200 rounded-xl text-sm">
                  Fechar
                </button>
                <button
                  type="submit"
                  disabled={!importFile || importMutation.isPending}
                  className="flex items-center gap-2 px-4 py-2.5 bg-emerald-500 text-white rounded-xl text-sm disabled:opacity-50"
                >
                  {importMutation.isPending ? <><Loader2 className="w-4 h-4 animate-spin" /> Importando...</> : 'Importar contatos'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Edit contact modal */}
      {editTarget && (
        <div className="fixed inset-0 bg-black/50 backdrop-blur-sm flex items-center justify-center z-50 p-4">
          <div className="bg-white rounded-2xl p-6 w-full max-w-md max-h-[90vh] overflow-y-auto shadow-2xl">
            <div className="flex items-center justify-between mb-6">
              <h2 className="text-lg font-semibold text-slate-800">Editar Contato</h2>
              <button onClick={() => setEditTarget(null)} className="p-2 hover:bg-slate-100 rounded-lg">
                <X className="w-5 h-5 text-slate-400" />
              </button>
            </div>

            {updateMutation.isError && (
              <div className="mb-4 p-3 bg-red-50 border border-red-200 rounded-xl text-sm text-red-700">
                {(updateMutation.error as Error).message}
              </div>
            )}

            <form
              onSubmit={(e) => {
                e.preventDefault()
                const fd = new FormData(e.currentTarget)
                updateMutation.mutate({
                  id: editTarget.id,
                  data: {
                    phoneNumber: (fd.get('phoneNumber') as string).replace(/\D/g, ''),
                    name: (fd.get('name') as string) || undefined,
                  },
                })
              }}
              className="space-y-4"
            >
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1.5">Telefone</label>
                <input
                  type="text"
                  name="phoneNumber"
                  value={editTarget.phoneNumber}
                  onChange={(e) => setEditTarget({ ...editTarget, phoneNumber: e.target.value.replace(/\D/g, '') })}
                  className="w-full px-4 py-2.5 border border-slate-200 rounded-xl text-sm focus:ring-2 focus:ring-emerald-500"
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1.5">Nome</label>
                <input
                  name="name"
                  type="text"
                  defaultValue={editTarget.name ?? ''}
                  placeholder="Nome do contato"
                  className="w-full px-4 py-2.5 border border-slate-200 rounded-xl text-sm focus:ring-2 focus:ring-emerald-500"
                />
              </div>
              <section className="border-t border-slate-100 pt-4 space-y-3">
                <div>
                  <h3 className="text-sm font-semibold text-slate-800">Memória do cliente</h3>
                  <p className="text-xs text-slate-500 mt-1">
                    Salve apenas fatos confirmados pelo cliente. A IA usará isso para personalizar o atendimento.
                  </p>
                </div>

                {isMemoryError ? (
                  <p className="text-xs text-red-600">Não foi possível carregar a memória deste contato.</p>
                ) : isLoadingMemory ? (
                  <div className="text-xs text-slate-500 flex items-center gap-2">
                    <Loader2 className="w-3.5 h-3.5 animate-spin" /> Carregando memória...
                  </div>
                ) : contactMemory?.consentGranted ? (
                  <>
                    <div className="space-y-2">
                      {contactMemory.items.length === 0 ? (
                        <p className="text-xs text-slate-500">Nenhuma memória confirmada para este contato.</p>
                      ) : contactMemory.items.map((memory) => (
                        <div key={memory.id} className="flex items-start justify-between gap-3 rounded-xl bg-slate-50 px-3 py-2">
                          <div className="min-w-0">
                            <p className="text-xs font-medium text-slate-700 truncate">{memory.key}</p>
                            <p className="text-sm text-slate-600 break-words">{memory.value}</p>
                            <p className="text-[11px] text-slate-400 mt-1">
                              Válida até {new Date(memory.expiresAt).toLocaleDateString('pt-BR')}
                            </p>
                          </div>
                          <button
                            type="button"
                            onClick={() => removeMemoryMutation.mutate(memory.id)}
                            disabled={removeMemoryMutation.isPending}
                            className="shrink-0 text-xs text-red-600 hover:text-red-700 disabled:opacity-50"
                          >
                            Remover
                          </button>
                        </div>
                      ))}
                    </div>
                    <div className="space-y-2 rounded-xl border border-emerald-100 bg-emerald-50/50 p-3">
                      <input
                        value={memoryKey}
                        onChange={(e) => setMemoryKey(e.target.value)}
                        maxLength={80}
                        placeholder="Nome (ex.: preferência)"
                        className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm bg-white focus:ring-2 focus:ring-emerald-500"
                      />
                      <textarea
                        value={memoryValue}
                        onChange={(e) => setMemoryValue(e.target.value)}
                        maxLength={160}
                        rows={2}
                        placeholder="Fato confirmado pelo cliente"
                        className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm bg-white resize-none focus:ring-2 focus:ring-emerald-500"
                      />
                      {saveMemoryMutation.isError && (
                        <p className="text-xs text-red-600">{(saveMemoryMutation.error as Error).message}</p>
                      )}
                      <button
                        type="button"
                        onClick={() => saveMemoryMutation.mutate()}
                        disabled={!memoryKey.trim() || !memoryValue.trim() || saveMemoryMutation.isPending}
                        className="w-full px-3 py-2 bg-emerald-600 text-white rounded-lg text-sm disabled:opacity-50"
                      >
                        {saveMemoryMutation.isPending ? 'Salvando...' : 'Salvar memória confirmada'}
                      </button>
                    </div>
                  </>
                ) : (
                  <div className="rounded-xl border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800">
                    Este contato ainda não autorizou o atendimento automatizado. Ele precisa responder <strong>SIM</strong> antes de salvar memória.
                  </div>
                )}
              </section>
              <div className="flex justify-end gap-3 pt-2">
                <button type="button" onClick={() => setEditTarget(null)} className="px-4 py-2.5 border border-slate-200 rounded-xl text-sm">
                  Cancelar
                </button>
                <button type="submit" disabled={updateMutation.isPending} className="flex items-center gap-2 px-4 py-2.5 bg-emerald-500 text-white rounded-xl text-sm disabled:opacity-50">
                  {updateMutation.isPending ? <><Loader2 className="w-4 h-4 animate-spin" /> Salvando...</> : 'Salvar'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Create contact modal */}
      {showCreateForm && (
        <div className="fixed inset-0 bg-black/50 backdrop-blur-sm flex items-center justify-center z-50 p-4">
          <div className="bg-white rounded-2xl p-6 w-full max-w-md shadow-2xl">
            <div className="flex items-center justify-between mb-6">
              <h2 className="text-lg font-semibold text-slate-800">Novo Contato</h2>
              <button onClick={() => setShowCreateForm(false)} className="p-2 hover:bg-slate-100 rounded-lg">
                <X className="w-5 h-5 text-slate-400" />
              </button>
            </div>

            {createMutation.isError && (
              <div className="mb-4 p-3 bg-red-50 border border-red-200 rounded-xl text-sm text-red-700">
                {(createMutation.error as Error).message}
              </div>
            )}

            <form onSubmit={handleCreate} className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1.5">Telefone *</label>
                <input
                  name="phoneNumber"
                  type="text"
                  required
                  value={phoneNumber}
                  onChange={(e) => setPhoneNumber(maskBrazilPhone(e.target.value))}
                  placeholder="(11) 99999-9999"
                  className="w-full px-4 py-2.5 border border-slate-200 rounded-xl text-sm focus:ring-2 focus:ring-emerald-500"
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1.5">Nome</label>
                <input
                  name="name"
                  type="text"
                  placeholder="Nome do contato"
                  className="w-full px-4 py-2.5 border border-slate-200 rounded-xl text-sm focus:ring-2 focus:ring-emerald-500"
                />
              </div>
              <div className="flex items-center gap-2">
                <input type="checkbox" name="startConversation" id="startConversation" className="rounded" />
                <label htmlFor="startConversation" className="text-sm text-slate-700">
                  Iniciar conversa após salvar
                </label>
              </div>
              <div className="flex justify-end gap-3 pt-2">
                <button type="button" onClick={() => setShowCreateForm(false)} className="px-4 py-2.5 border border-slate-200 rounded-xl text-sm">
                  Cancelar
                </button>
                <button type="submit" disabled={createMutation.isPending} className="flex items-center gap-2 px-4 py-2.5 bg-emerald-500 text-white rounded-xl text-sm disabled:opacity-50">
                  {createMutation.isPending ? <><Loader2 className="w-4 h-4 animate-spin" /> Salvando...</> : 'Salvar Contato'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}

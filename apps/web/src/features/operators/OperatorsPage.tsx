import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  Users,
  Plus,
  Search,
  CheckCircle2,
  XCircle,
  Clock,
  Copy,
  X,
  Loader2,
  AlertCircle,
  Check,
} from 'lucide-react'
import { useAuth } from '../../lib/auth'
import { api, type Operator, type LineAssignment } from '../../lib/api'

export function OperatorsPage() {
  const { user } = useAuth()
  const queryClient = useQueryClient()
  const [showCreateForm, setShowCreateForm] = useState(false)
  const [showResetForm, setShowResetForm] = useState(false)
  const [resetTarget, setResetTarget] = useState<Operator | null>(null)
  const [createdCredentials, setCreatedCredentials] = useState<{ email: string; password: string } | null>(null)
  const [copied, setCopied] = useState(false)
  const [search, setSearch] = useState('')

  const { data: operators, isLoading, error } = useQuery({
    queryKey: ['operators'],
    queryFn: api.operators.list,
  })

  const createMutation = useMutation({
    mutationFn: (data: { email: string; displayName?: string; password: string }) => api.operators.create(data),
    onSuccess: (data) => {
      setCreatedCredentials({ email: data.email, password: data.temporaryPassword })
      setShowCreateForm(false)
      queryClient.setQueryData<Operator[]>(['operators'], (current = []) => [
        {
          id: data.membershipId,
          userId: data.membershipId,
          email: data.email,
          displayName: data.displayName,
          status: 'Active',
          createdAt: new Date().toISOString(),
        },
        ...current,
      ])
      queryClient.invalidateQueries({ queryKey: ['operators'] })
    },
  })

  const deactivateMutation = useMutation({
    mutationFn: (operatorId: string) => api.operators.deactivate(operatorId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['operators'] }),
  })

  const reactivateMutation = useMutation({
    mutationFn: (operatorId: string) => api.operators.reactivate(operatorId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['operators'] }),
  })

  const resetPasswordMutation = useMutation({
    mutationFn: ({ operatorId, newPassword }: { operatorId: string; newPassword: string }) =>
      api.operators.resetPassword(operatorId, newPassword),
    onSuccess: (data) => {
      setCreatedCredentials({ email: data.email, password: data.temporaryPassword })
      queryClient.invalidateQueries({ queryKey: ['operators'] })
    },
  })

  const assignLinesMutation = useMutation({
    mutationFn: ({ operatorId, lines }: { operatorId: string; lines: LineAssignment[] }) =>
      api.operators.assignLines(operatorId, lines),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['operators'] }),
  })

  const handleCreate = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    const formData = new FormData(e.currentTarget)
    createMutation.mutate({
      email: formData.get('email') as string,
      displayName: (formData.get('displayName') as string) || undefined,
      password: formData.get('password') as string,
    })
  }

  const copyCredentials = () => {
    if (createdCredentials) {
      const text = `Email: ${createdCredentials.email}\nSenha temporária: ${createdCredentials.password}`
      navigator.clipboard.writeText(text)
      setCopied(true)
      setTimeout(() => setCopied(false), 2000)
    }
  }

  const filteredOperators = (operators ?? []).filter((op) =>
    op.email.toLowerCase().includes(search.toLowerCase()) ||
    (op.displayName ?? '').toLowerCase().includes(search.toLowerCase())
  )
  const activeOperatorCount = (operators ?? []).filter((operator) => operator.status !== 'Inactive').length
  const operatorLimit = user?.operatorLimit ?? 0
  const limitReached = operatorLimit > 0 && activeOperatorCount >= operatorLimit
  const lineOptions = [
    ...Array.from({ length: user?.officialApiLineCount ?? 0 }, (_, index) => ({ type: 'OfficialApi', number: index + 1, label: `API oficial ${index + 1}` })),
    ...Array.from({ length: user?.qrCodeLineCount ?? 0 }, (_, index) => ({ type: 'QrCode', number: index + 1, label: `QR Code ${index + 1}` })),
  ]

  const getStatusBadge = (status: string) => {
    switch (status) {
      case 'Active':
        return (
          <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-emerald-100 text-emerald-700">
            <CheckCircle2 className="w-3 h-3" /> Ativo
          </span>
        )
      case 'Inactive':
        return (
          <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-red-100 text-red-700">
            <XCircle className="w-3 h-3" /> Inativo
          </span>
        )
      case 'Pending':
        return (
          <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-amber-100 text-amber-700">
            <Clock className="w-3 h-3" /> Pendente
          </span>
        )
      default:
        return (
          <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-slate-100 text-slate-700">
            {status}
          </span>
        )
    }
  }

  return (
    <div className="h-full flex flex-col bg-slate-50">
      <div className="bg-white border-b border-slate-200 px-4 sm:px-6 py-4">
        <div className="flex items-center justify-between gap-3">
          <div className="min-w-0">
            <h1 className="text-xl font-semibold text-slate-800">Operadores</h1>
            <p className="text-sm text-slate-500 mt-0.5">Gerencie os operadores de atendimento</p>
          </div>
          <button
            onClick={() => {
              createMutation.reset()
              setShowCreateForm(true)
            }}
            disabled={limitReached}
            title={limitReached ? 'Limite de operadores atingido' : 'Novo operador'}
            className="flex items-center gap-2 px-3 sm:px-4 py-2.5 bg-emerald-500 text-white rounded-xl hover:bg-emerald-600 transition-colors shadow-sm disabled:opacity-50 disabled:cursor-not-allowed whitespace-nowrap"
          >
            <Plus className="w-4 h-4" /> <span className="hidden sm:inline">Novo Operador</span>
          </button>
        </div>
        <p className="mt-2 text-sm text-slate-500">
          Operadores cadastrados: <strong>{activeOperatorCount}</strong> / {operatorLimit || 'Ilimitado'}
        </p>
      </div>

      <div className="flex-1 overflow-auto p-4 sm:p-6">
        {createdCredentials && (
          <div className="mb-4 p-4 bg-emerald-50 border border-emerald-200 rounded-xl">
            <div className="flex items-start gap-3">
              <CheckCircle2 className="w-5 h-5 text-emerald-500 flex-shrink-0 mt-0.5" />
              <div className="flex-1">
                <p className="font-medium text-emerald-800">Operador criado com sucesso!</p>
                <p className="text-sm text-emerald-600 mt-1">
                  Copie as credenciais abaixo e envie ao operador. Ele deverá alterar a senha no primeiro acesso.
                </p>
                <div className="mt-3 p-3 bg-white border border-emerald-200 rounded-lg">
                  <p className="text-sm text-slate-700"><strong>Email:</strong> {createdCredentials.email}</p>
                  <p className="text-sm text-slate-700"><strong>Senha temporária:</strong> {createdCredentials.password}</p>
                </div>
                <button
                  onClick={copyCredentials}
                  className="mt-2 flex items-center gap-1.5 px-3 py-1.5 bg-emerald-500 text-white rounded-lg hover:bg-emerald-600 transition-colors text-sm"
                >
                  <Copy className="w-4 h-4" /> {copied ? 'Copiado!' : 'Copiar credenciais'}
                </button>
              </div>
              <button
                onClick={() => setCreatedCredentials(null)}
                className="p-1 hover:bg-emerald-100 rounded-lg"
              >
                <X className="w-4 h-4 text-emerald-500" />
              </button>
            </div>
          </div>
        )}

        {error && (
          <div className="mb-4 p-4 bg-red-50 border border-red-200 rounded-xl flex items-center gap-3">
            <AlertCircle className="w-5 h-5 text-red-500 flex-shrink-0" />
            <div>
              <p className="font-medium text-red-800">Erro ao carregar operadores</p>
              <p className="text-sm text-red-600">{(error as Error).message}</p>
            </div>
          </div>
        )}

        <div className="mb-4">
          <div className="relative">
            <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 w-4 h-4 text-slate-400" />
            <input
              type="text"
              placeholder="Buscar operadores..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="w-full pl-10 pr-4 py-2.5 bg-white border border-slate-200 rounded-xl text-sm focus:ring-2 focus:ring-emerald-500 focus:border-transparent"
            />
          </div>
        </div>

        {isLoading ? (
          <div className="flex items-center justify-center py-12">
            <Loader2 className="w-6 h-6 text-emerald-500 animate-spin" />
            <span className="ml-2 text-slate-500">Carregando operadores...</span>
          </div>
        ) : (
          <div className="bg-white rounded-xl border border-slate-200 overflow-hidden shadow-sm">
            <div className="overflow-x-auto">
              <table className="min-w-full">
                <thead>
                  <tr className="bg-slate-50 border-b border-slate-200">
                    <th className="px-4 sm:px-6 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">Nome</th>
                    <th className="hidden sm:table-cell px-6 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">Email</th>
                    <th className="px-4 sm:px-6 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">Status</th>
                    <th className="hidden md:table-cell px-6 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">Linha</th>
                    <th className="hidden md:table-cell px-6 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">Criado em</th>
                    <th className="px-4 sm:px-6 py-3 text-right text-xs font-semibold text-slate-500 uppercase tracking-wider">Ações</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {filteredOperators.length === 0 ? (
                    <tr>
                      <td colSpan={6} className="px-6 py-12 text-center text-slate-500">
                        {search ? 'Nenhum operador encontrado com esse filtro.' : 'Nenhum operador cadastrado.'}
                      </td>
                    </tr>
                  ) : (
                    filteredOperators.map((operator) => (
                      <tr key={operator.id} className="hover:bg-slate-50 transition-colors">
                        <td className="px-4 sm:px-6 py-4">
                          <div className="flex items-center gap-3">
                            <div className="w-8 h-8 sm:w-9 sm:h-9 rounded-lg bg-blue-100 flex items-center justify-center flex-shrink-0">
                              <Users className="w-4 h-4 text-blue-600" />
                            </div>
                            <div className="min-w-0">
                              <span className="font-medium text-slate-800 text-sm block truncate max-w-[120px] sm:max-w-none">{operator.displayName || operator.email}</span>
                              <span className="sm:hidden text-xs text-slate-400 truncate block max-w-[120px]">{operator.email}</span>
                            </div>
                          </div>
                        </td>
                        <td className="hidden sm:table-cell px-6 py-4 text-sm text-slate-500">{operator.email}</td>
                        <td className="px-4 sm:px-6 py-4">{getStatusBadge(operator.status)}</td>
                        <td className="hidden md:table-cell px-6 py-4">
                          <LineMultiSelect
                            operator={operator}
                            lineOptions={lineOptions}
                            isLoading={assignLinesMutation.isPending}
                            onAssign={(lines) => assignLinesMutation.mutate({ operatorId: operator.id, lines })}
                          />
                        </td>
                        <td className="hidden md:table-cell px-6 py-4 text-sm text-slate-500">
                          {new Date(operator.createdAt).toLocaleDateString('pt-BR')}
                        </td>
                        <td className="px-4 sm:px-6 py-4 text-right">
                          <div className="flex items-center justify-end gap-2">
                            {operator.status === 'Active' && (
                              <button
                                onClick={() => { setResetTarget(operator); setShowResetForm(true) }}
                                className="text-xs sm:text-sm text-amber-600 hover:text-amber-700 font-medium whitespace-nowrap"
                              >
                                Resetar
                              </button>
                            )}
                            {operator.status === 'Active' ? (
                              <button
                                onClick={() => deactivateMutation.mutate(operator.id)}
                                disabled={deactivateMutation.isPending}
                                className="text-xs sm:text-sm text-red-600 hover:text-red-700 font-medium disabled:opacity-50 whitespace-nowrap"
                              >
                                Desativar
                              </button>
                            ) : operator.status === 'Inactive' ? (
                              <button
                                onClick={() => reactivateMutation.mutate(operator.id)}
                                disabled={reactivateMutation.isPending}
                                className="text-xs sm:text-sm text-emerald-600 hover:text-emerald-700 font-medium disabled:opacity-50 whitespace-nowrap"
                              >
                                {reactivateMutation.isPending ? '...' : 'Reativar'}
                              </button>
                            ) : null}
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

      {/* Reset password modal */}
      {showResetForm && resetTarget && (
        <div className="fixed inset-0 bg-black/50 backdrop-blur-sm flex items-center justify-center z-50 p-4">
          <div className="bg-white rounded-2xl p-6 w-full max-w-md shadow-2xl">
            <div className="flex items-center justify-between mb-6">
              <h2 className="text-lg font-semibold text-slate-800">Resetar Senha</h2>
              <button
                onClick={() => { setShowResetForm(false); setResetTarget(null) }}
                className="p-2 hover:bg-slate-100 rounded-lg"
              >
                <X className="w-5 h-5 text-slate-400" />
              </button>
            </div>

            <p className="text-sm text-slate-600 mb-4">
              Resetar senha de <strong>{resetTarget.displayName || resetTarget.email}</strong>.
              O operador deverá alterar a senha no próximo login.
            </p>

            <form onSubmit={(e) => {
              e.preventDefault()
              const formData = new FormData(e.currentTarget)
              resetPasswordMutation.mutate({
                operatorId: resetTarget.id,
                newPassword: formData.get('newPassword') as string,
              })
              setShowResetForm(false)
              setResetTarget(null)
            }} className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1.5">
                  Nova Senha Temporária *
                </label>
                <input
                  name="newPassword"
                  type="password"
                  required
                  minLength={8}
                  className="w-full px-4 py-2.5 border border-slate-200 rounded-xl text-sm focus:ring-2 focus:ring-emerald-500 focus:border-transparent"
                  placeholder="Mínimo 8 caracteres"
                />
              </div>
              <div className="flex justify-end gap-3">
                <button
                  type="button"
                  onClick={() => { setShowResetForm(false); setResetTarget(null) }}
                  className="px-4 py-2.5 border border-slate-200 rounded-xl text-sm hover:bg-slate-50"
                >
                  Cancelar
                </button>
                <button
                  type="submit"
                  disabled={resetPasswordMutation.isPending}
                  className="flex items-center gap-2 px-4 py-2.5 bg-amber-500 text-white rounded-xl text-sm hover:bg-amber-600 disabled:opacity-50"
                >
                  {resetPasswordMutation.isPending ? (
                    <><Loader2 className="w-4 h-4 animate-spin" /> Resetando...</>
                  ) : (
                    'Resetar Senha'
                  )}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Create operator modal */}
      {showCreateForm && (
        <div className="fixed inset-0 bg-black/50 backdrop-blur-sm flex items-center justify-center z-50 p-4">
          <div className="bg-white rounded-2xl p-6 w-full max-w-md shadow-2xl">
            <div className="flex items-center justify-between mb-6">
              <h2 className="text-lg font-semibold text-slate-800">Novo Operador</h2>
              <button
                onClick={() => setShowCreateForm(false)}
                className="p-2 hover:bg-slate-100 rounded-lg"
              >
                <X className="w-5 h-5 text-slate-400" />
              </button>
            </div>

            {createMutation.isError && (
              <div className="mb-4 p-3 bg-red-50 border border-red-200 rounded-xl flex items-center gap-2">
                <AlertCircle className="w-4 h-4 text-red-500 flex-shrink-0" />
                <p className="text-sm text-red-700">{(createMutation.error as Error).message}</p>
              </div>
            )}

            <form onSubmit={handleCreate} className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1.5">
                  Email *
                </label>
                <input
                  name="email"
                  type="email"
                  required
                  className="w-full px-4 py-2.5 border border-slate-200 rounded-xl text-sm focus:ring-2 focus:ring-emerald-500 focus:border-transparent"
                  placeholder="operador@empresa.com"
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1.5">
                  Nome
                </label>
                <input
                  name="displayName"
                  type="text"
                  className="w-full px-4 py-2.5 border border-slate-200 rounded-xl text-sm focus:ring-2 focus:ring-emerald-500 focus:border-transparent"
                  placeholder="Nome do operador (opcional)"
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1.5">
                  Senha Temporária *
                </label>
                <input
                  name="password"
                  type="password"
                  required
                  minLength={8}
                  className="w-full px-4 py-2.5 border border-slate-200 rounded-xl text-sm focus:ring-2 focus:ring-emerald-500 focus:border-transparent"
                  placeholder="Mínimo 8 caracteres"
                />
                <p className="text-xs text-slate-500 mt-1">
                  O operador deverá alterar esta senha no primeiro acesso.
                </p>
              </div>
              <div className="flex justify-end gap-3 pt-2">
                <button
                  type="button"
                  onClick={() => setShowCreateForm(false)}
                  className="px-4 py-2.5 border border-slate-200 rounded-xl text-sm hover:bg-slate-50 transition-colors"
                >
                  Cancelar
                </button>
                <button
                  type="submit"
                  disabled={createMutation.isPending}
                  className="flex items-center gap-2 px-4 py-2.5 bg-emerald-500 text-white rounded-xl text-sm hover:bg-emerald-600 transition-colors disabled:opacity-50"
                >
                  {createMutation.isPending ? (
                    <>
                      <Loader2 className="w-4 h-4 animate-spin" /> Criando...
                    </>
                  ) : (
                    'Criar Operador'
                  )}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}

interface LineOption {
  type: string
  number: number
  label: string
}

function LineMultiSelect({
  operator,
  lineOptions,
  isLoading,
  onAssign,
}: {
  operator: Operator
  lineOptions: LineOption[]
  isLoading: boolean
  onAssign: (lines: LineAssignment[]) => void
}) {
  const [isOpen, setIsOpen] = useState(false)
  // Local draft — only sent to server when user confirms
  const [draft, setDraft] = useState<LineAssignment[] | null>(null)
  const [btnRef, setBtnRef] = useState<HTMLButtonElement | null>(null)
  const [dropPos, setDropPos] = useState<{ top: number; left: number; width: number; above: boolean } | null>(null)

  const committed = operator.assignedLines ?? []
  const current = draft ?? committed

  const isAssigned = (type: string, number: number) =>
    current.some((l) => l.connectionType === type && l.lineNumber === number)

  const toggleLine = (type: string, number: number) => {
    const next = isAssigned(type, number)
      ? current.filter((l) => !(l.connectionType === type && l.lineNumber === number))
      : [...current, { connectionType: type, lineNumber: number }]
    setDraft(next)
  }

  const openDropdown = () => {
    if (btnRef) {
      const rect = btnRef.getBoundingClientRect()
      const dropdownHeight = Math.min(
        208 + 44, // max-h-52 (208px) + confirm bar (~44px)
        window.innerHeight * 0.6
      )
      const spaceBelow = window.innerHeight - rect.bottom - 8
      const above = spaceBelow < dropdownHeight
      setDropPos({
        top: above ? rect.top - dropdownHeight - 4 : rect.bottom + 4,
        left: rect.left,
        width: Math.max(rect.width, 180),
        above,
      })
    }
    setDraft(null) // reset draft to committed on open
    setIsOpen(true)
  }

  const confirm = () => {
    if (draft !== null) onAssign(draft)
    setIsOpen(false)
    setDraft(null)
  }

  const cancel = () => {
    setIsOpen(false)
    setDraft(null)
  }

  const displayText = committed.length === 0
    ? 'Sem atribuição'
    : committed.length === 1
      ? lineOptions.find((l) => l.type === committed[0].connectionType && l.number === committed[0].lineNumber)?.label ?? '1 linha'
      : `${committed.length} linhas`

  return (
    <div>
      <button
        ref={setBtnRef}
        type="button"
        onClick={openDropdown}
        disabled={isLoading}
        className="text-xs px-2 py-1.5 border border-slate-200 rounded-lg disabled:opacity-50 flex items-center gap-1 min-w-[110px] justify-between bg-white hover:bg-slate-50 transition-colors"
      >
        <span className="truncate">{displayText}</span>
        {isLoading
          ? <Loader2 className="w-3 h-3 animate-spin flex-shrink-0" />
          : <svg className="w-3 h-3 flex-shrink-0 text-slate-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
            </svg>
        }
      </button>

      {isOpen && dropPos && (
        <>
          {/* backdrop */}
          <div className="fixed inset-0 z-40" onClick={cancel} />

          {/* dropdown rendered at fixed position to escape table overflow */}
          <div
            className="fixed z-50 rounded-lg shadow-xl overflow-hidden"
            style={{
              top: dropPos.top,
              left: dropPos.left,
              minWidth: dropPos.width,
              backgroundColor: '#0b1222',
              border: '1px solid rgba(148,163,184,0.25)',
            }}
          >
            {lineOptions.length === 0 ? (
              <div className="px-3 py-3 text-xs" style={{ color: '#94a3b8' }}>Nenhuma linha disponível</div>
            ) : (
              <div className="max-h-52 overflow-y-auto">
                {lineOptions.map((line) => (
                  <button
                    key={`${line.type}:${line.number}`}
                    type="button"
                    onClick={() => toggleLine(line.type, line.number)}
                    className="w-full flex items-center gap-2.5 px-3 py-2.5 text-xs transition-colors text-left"
                    style={{ color: '#f1f5f9' }}
                    onMouseEnter={(e) => (e.currentTarget.style.backgroundColor = '#10223f')}
                    onMouseLeave={(e) => (e.currentTarget.style.backgroundColor = '')}
                  >
                    <div className={`w-4 h-4 rounded flex items-center justify-center flex-shrink-0 transition-colors ${
                      isAssigned(line.type, line.number)
                        ? 'bg-emerald-500'
                        : ''
                    }`}
                    style={isAssigned(line.type, line.number) ? {} : { border: '1px solid rgba(148,163,184,0.5)' }}
                    >
                      {isAssigned(line.type, line.number) && (
                        <Check className="w-3 h-3 text-white" />
                      )}
                    </div>
                    <span>{line.label}</span>
                  </button>
                ))}
              </div>
            )}

            {/* confirm / cancel bar */}
            <div className="flex items-center justify-end gap-2 px-3 py-2"
              style={{ borderTop: '1px solid rgba(148,163,184,0.2)', backgroundColor: '#0d1829' }}
            >
              <button
                type="button"
                onClick={cancel}
                className="px-2.5 py-1 text-xs rounded transition-colors"
                style={{ color: '#94a3b8' }}
              >
                Cancelar
              </button>
              <button
                type="button"
                onClick={confirm}
                disabled={draft === null}
                className="px-3 py-1 text-xs bg-emerald-500 hover:bg-emerald-600 text-white rounded font-medium transition-colors disabled:opacity-40"
              >
                Confirmar
              </button>
            </div>
          </div>
        </>
      )}
    </div>
  )
}

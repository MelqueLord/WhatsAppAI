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
} from 'lucide-react'
import { api } from '../../lib/api'

interface Operator {
  id: string
  userId: string
  email: string
  displayName?: string
  status: string
  createdAt: string
  deactivatedAt?: string
  reactivatedAt?: string
}

export function OperatorsPage() {
  const queryClient = useQueryClient()
  const [showCreateForm, setShowCreateForm] = useState(false)
  const [createdCredentials, setCreatedCredentials] = useState<{ email: string; password: string } | null>(null)
  const [copied, setCopied] = useState(false)
  const [search, setSearch] = useState('')

  const { data: operators, isLoading, error } = useQuery({
    queryKey: ['operators'],
    queryFn: async () => {
      const res = await fetch('/api/operators', { credentials: 'include' })
      if (!res.ok) throw new Error('Erro ao carregar operadores')
      return res.json() as Promise<Operator[]>
    },
  })

  const createMutation = useMutation({
    mutationFn: async (data: { email: string; displayName?: string; password: string }) => {
      const res = await fetch('/api/operators', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify(data),
      })
      if (!res.ok) {
        const err = await res.json()
        throw new Error(err.error || 'Erro ao criar operador')
      }
      return res.json()
    },
    onSuccess: (data) => {
      setCreatedCredentials({ email: data.email, password: data.temporaryPassword })
      setShowCreateForm(false)
      queryClient.invalidateQueries({ queryKey: ['operators'] })
    },
  })

  const deactivateMutation = useMutation({
    mutationFn: async (operatorId: string) => {
      const res = await fetch(`/api/operators/${operatorId}/deactivate`, {
        method: 'POST',
        credentials: 'include',
      })
      if (!res.ok) throw new Error('Erro ao desativar')
      return res.json()
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['operators'] }),
  })

  const reactivateMutation = useMutation({
    mutationFn: async (operatorId: string) => {
      const res = await fetch(`/api/operators/${operatorId}/reactivate`, {
        method: 'POST',
        credentials: 'include',
      })
      if (!res.ok) throw new Error('Erro ao reativar')
      return res.json()
    },
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
      <div className="bg-white border-b border-slate-200 px-6 py-4">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-xl font-semibold text-slate-800">Operadores</h1>
            <p className="text-sm text-slate-500 mt-0.5">Gerencie os operadores de atendimento</p>
          </div>
          <button
            onClick={() => {
              createMutation.reset()
              setShowCreateForm(true)
            }}
            className="flex items-center gap-2 px-4 py-2.5 bg-emerald-500 text-white rounded-xl hover:bg-emerald-600 transition-colors shadow-sm"
          >
            <Plus className="w-4 h-4" /> Novo Operador
          </button>
        </div>
      </div>

      <div className="flex-1 overflow-auto p-6">
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
            <table className="min-w-full">
              <thead>
                <tr className="bg-slate-50 border-b border-slate-200">
                  <th className="px-6 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">
                    Nome
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">
                    Email
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">
                    Status
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">
                    Criado em
                  </th>
                  <th className="px-6 py-3 text-right text-xs font-semibold text-slate-500 uppercase tracking-wider">
                    Ações
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {filteredOperators.length === 0 ? (
                  <tr>
                    <td colSpan={5} className="px-6 py-12 text-center text-slate-500">
                      {search ? 'Nenhum operador encontrado com esse filtro.' : 'Nenhum operador cadastrado.'}
                    </td>
                  </tr>
                ) : (
                  filteredOperators.map((operator) => (
                    <tr key={operator.id} className="hover:bg-slate-50 transition-colors">
                      <td className="px-6 py-4">
                        <div className="flex items-center gap-3">
                          <div className="w-9 h-9 rounded-lg bg-blue-100 flex items-center justify-center">
                            <Users className="w-4 h-4 text-blue-600" />
                          </div>
                          <span className="font-medium text-slate-800">{operator.displayName || operator.email}</span>
                        </div>
                      </td>
                      <td className="px-6 py-4 text-sm text-slate-500">{operator.email}</td>
                      <td className="px-6 py-4">{getStatusBadge(operator.status)}</td>
                      <td className="px-6 py-4 text-sm text-slate-500">
                        {new Date(operator.createdAt).toLocaleDateString('pt-BR')}
                      </td>
                      <td className="px-6 py-4 text-right">
                        {operator.status === 'Active' ? (
                          <button
                            onClick={() => deactivateMutation.mutate(operator.id)}
                            disabled={deactivateMutation.isPending}
                            className="text-sm text-red-600 hover:text-red-700 font-medium disabled:opacity-50"
                          >
                            Desativar
                          </button>
                        ) : operator.status === 'Inactive' ? (
                          <button
                            onClick={() => reactivateMutation.mutate(operator.id)}
                            disabled={reactivateMutation.isPending}
                            className="text-sm text-emerald-600 hover:text-emerald-700 font-medium disabled:opacity-50"
                          >
                            {reactivateMutation.isPending ? 'Reativando...' : 'Reativar'}
                          </button>
                        ) : null}
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Create operator modal */}
      {showCreateForm && (
        <div className="fixed inset-0 bg-black/50 backdrop-blur-sm flex items-center justify-center z-50">
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

import { useState } from 'react'
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
  AlertTriangle,
  RefreshCw,
} from 'lucide-react'

const MOCK_OPERATORS = [
  { id: '1', userId: 'u1', email: 'maria@empresa.com', displayName: 'Maria Santos', status: 'Active', createdAt: '2026-07-10T10:00:00Z' },
  { id: '2', userId: 'u2', email: 'joao@empresa.com', displayName: 'João Silva', status: 'Active', createdAt: '2026-07-15T14:30:00Z' },
  { id: '3', userId: 'u3', email: 'ana@empresa.com', displayName: 'Ana Oliveira', status: 'Pending', createdAt: '2026-08-12T09:00:00Z' },
  { id: '4', userId: 'u4', email: 'pedro@empresa.com', displayName: 'Pedro Costa', status: 'Inactive', createdAt: '2026-06-20T16:00:00Z' },
  { id: '5', userId: 'u5', email: 'carla@empresa.com', displayName: 'Carla Mendes', status: 'Active', createdAt: '2026-08-01T11:00:00Z' },
]

export function OperatorsPage() {
  const [showInviteForm, setShowInviteForm] = useState(false)
  const [activationLink, setActivationLink] = useState<string | null>(null)
  const [copied, setCopied] = useState(false)
  const [search, setSearch] = useState('')
  const [operators, setOperators] = useState(MOCK_OPERATORS)

  const copyLink = () => {
    if (activationLink) {
      navigator.clipboard.writeText(activationLink)
      setCopied(true)
      setTimeout(() => setCopied(false), 2000)
    }
  }

  const handleInvite = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    const formData = new FormData(e.currentTarget)
    const email = formData.get('email') as string
    const displayName = formData.get('displayName') as string
    setActivationLink(`https://app.whatsapp-ai.com/activate?invitation=${Date.now()}&token=mock`)
    setShowInviteForm(false)
    setOperators([...operators, { id: String(Date.now()), userId: 'new', email, displayName: displayName || email, status: 'Pending', createdAt: new Date().toISOString() }])
  }

  const handleDeactivate = (id: string) => {
    setOperators(operators.map(op => op.id === id ? { ...op, status: 'Inactive' } : op))
  }

  const handleReactivate = (id: string) => {
    setOperators(operators.map(op => op.id === id ? { ...op, status: 'Active' } : op))
  }

  const filteredOperators = operators.filter(
    (op) => op.email.toLowerCase().includes(search.toLowerCase()) || op.displayName?.toLowerCase().includes(search.toLowerCase())
  )

  const getStatusBadge = (status: string) => {
    switch (status) {
      case 'Active': return <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-emerald-100 text-emerald-700"><CheckCircle2 className="w-3 h-3" /> Ativo</span>
      case 'Pending': return <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-amber-100 text-amber-700"><Clock className="w-3 h-3" /> Pendente</span>
      case 'Inactive': return <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-red-100 text-red-700"><XCircle className="w-3 h-3" /> Inativo</span>
      default: return <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-slate-100 text-slate-700">{status}</span>
    }
  }

  return (
    <div className="h-full flex flex-col bg-slate-50">
      <div className="bg-white border-b border-slate-200 px-6 py-4">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-xl font-semibold text-slate-800">Operadores</h1>
            <p className="text-sm text-slate-500 mt-0.5">Gerencie os operadores do tenant</p>
          </div>
          <button onClick={() => setShowInviteForm(true)} className="flex items-center gap-2 px-4 py-2.5 bg-emerald-500 text-white rounded-xl hover:bg-emerald-600 transition-colors shadow-sm">
            <Plus className="w-4 h-4" /> Convidar Operador
          </button>
        </div>
      </div>

      <div className="flex-1 overflow-auto p-6">
        {activationLink && (
          <div className="mb-4 p-4 bg-emerald-50 border border-emerald-200 rounded-xl animate-fade-in">
            <div className="flex items-start gap-3">
              <CheckCircle2 className="w-5 h-5 text-emerald-500 flex-shrink-0 mt-0.5" />
              <div className="flex-1">
                <p className="font-medium text-emerald-800">Convite criado com sucesso!</p>
                <p className="text-sm text-emerald-600 mt-1">Link de ativação (copie agora):</p>
                <div className="flex items-center gap-2 mt-2">
                  <code className="flex-1 p-2.5 bg-white border border-emerald-200 rounded-lg text-xs text-slate-700 break-all">{activationLink}</code>
                  <button onClick={copyLink} className="flex items-center gap-1.5 px-3 py-2 bg-emerald-500 text-white rounded-lg hover:bg-emerald-600 transition-colors text-sm">
                    <Copy className="w-4 h-4" /> {copied ? 'Copiado!' : 'Copiar'}
                  </button>
                </div>
              </div>
              <button onClick={() => setActivationLink(null)} className="p-1 hover:bg-emerald-100 rounded-lg"><X className="w-4 h-4 text-emerald-500" /></button>
            </div>
          </div>
        )}

        <div className="mb-4">
          <div className="relative">
            <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 w-4 h-4 text-slate-400" />
            <input type="text" placeholder="Buscar operadores..." value={search} onChange={(e) => setSearch(e.target.value)} className="w-full pl-10 pr-4 py-2.5 bg-white border border-slate-200 rounded-xl text-sm focus:ring-2 focus:ring-emerald-500 focus:border-transparent" />
          </div>
        </div>

        <div className="bg-white rounded-xl border border-slate-200 overflow-hidden shadow-sm">
          <table className="min-w-full">
            <thead>
              <tr className="bg-slate-50 border-b border-slate-200">
                <th className="px-6 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">Operador</th>
                <th className="px-6 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">Status</th>
                <th className="px-6 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">Criado em</th>
                <th className="px-6 py-3 text-right text-xs font-semibold text-slate-500 uppercase tracking-wider">Ações</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {filteredOperators.map((op) => (
                <tr key={op.id} className="hover:bg-slate-50 transition-colors">
                  <td className="px-6 py-4">
                    <div className="flex items-center gap-3">
                      <div className="w-9 h-9 rounded-full bg-gradient-to-br from-blue-400 to-blue-600 flex items-center justify-center text-white font-semibold text-sm">
                        {op.displayName?.charAt(0)?.toUpperCase() || op.email.charAt(0).toUpperCase()}
                      </div>
                      <div>
                        <p className="font-medium text-slate-800">{op.displayName || op.email}</p>
                        <p className="text-xs text-slate-500">{op.email}</p>
                      </div>
                    </div>
                  </td>
                  <td className="px-6 py-4">{getStatusBadge(op.status)}</td>
                  <td className="px-6 py-4 text-sm text-slate-500">{new Date(op.createdAt).toLocaleDateString('pt-BR')}</td>
                  <td className="px-6 py-4 text-right">
                    <div className="flex items-center justify-end gap-2">
                      {op.status === 'Active' && <button onClick={() => handleDeactivate(op.id)} className="text-sm text-red-600 hover:text-red-700 font-medium">Desativar</button>}
                      {op.status === 'Inactive' && <button onClick={() => handleReactivate(op.id)} className="text-sm text-emerald-600 hover:text-emerald-700 font-medium">Reativar</button>}
                      {op.status === 'Pending' && <button className="flex items-center gap-1.5 text-sm text-blue-600 hover:text-blue-700 font-medium"><RefreshCw className="w-3.5 h-3.5" /> Reenviar</button>}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {showInviteForm && (
        <div className="fixed inset-0 bg-black/50 backdrop-blur-sm flex items-center justify-center z-50 animate-fade-in">
          <div className="bg-white rounded-2xl p-6 w-full max-w-md shadow-2xl animate-slide-in">
            <div className="flex items-center justify-between mb-6">
              <h2 className="text-lg font-semibold text-slate-800">Convidar Operador</h2>
              <button onClick={() => setShowInviteForm(false)} className="p-2 hover:bg-slate-100 rounded-lg"><X className="w-5 h-5 text-slate-400" /></button>
            </div>
            <form onSubmit={handleInvite} className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1.5">Email *</label>
                <input name="email" type="email" required className="w-full px-4 py-2.5 border border-slate-200 rounded-xl text-sm focus:ring-2 focus:ring-emerald-500 focus:border-transparent" placeholder="operador@email.com" />
              </div>
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1.5">Nome de Exibição</label>
                <input name="displayName" type="text" className="w-full px-4 py-2.5 border border-slate-200 rounded-xl text-sm focus:ring-2 focus:ring-emerald-500 focus:border-transparent" placeholder="Maria Santos" />
              </div>
              <div className="flex justify-end gap-3 pt-2">
                <button type="button" onClick={() => setShowInviteForm(false)} className="px-4 py-2.5 border border-slate-200 rounded-xl text-sm hover:bg-slate-50 transition-colors">Cancelar</button>
                <button type="submit" className="flex items-center gap-2 px-4 py-2.5 bg-emerald-500 text-white rounded-xl text-sm hover:bg-emerald-600 transition-colors">Enviar Convite</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}

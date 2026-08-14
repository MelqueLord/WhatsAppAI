import { useState } from 'react'
import {
  Building2,
  Plus,
  Search,
  CheckCircle2,
  XCircle,
  AlertTriangle,
  Copy,
  X,
} from 'lucide-react'

const MOCK_TENANTS = [
  { id: '1', name: 'Loja do João', status: 'Active', createdAt: '2026-06-01T10:00:00Z' },
  { id: '2', name: 'Restaurante Sabor Caseiro', status: 'Active', createdAt: '2026-06-15T14:30:00Z' },
  { id: '3', name: 'Clínica Saúde Total', status: 'Active', createdAt: '2026-07-01T09:00:00Z' },
  { id: '4', name: 'Pet Shop Amigo Fiél', status: 'Suspended', createdAt: '2026-07-10T16:00:00Z' },
  { id: '5', name: 'Academia Corpo em Forma', status: 'Active', createdAt: '2026-07-20T11:00:00Z' },
  { id: '6', name: 'Barbearia Estilo', status: 'Suspended', createdAt: '2026-08-01T08:00:00Z' },
]

export function AdminTenantsPage() {
  const [showCreateForm, setShowCreateForm] = useState(false)
  const [activationLink, setActivationLink] = useState<string | null>(null)
  const [copied, setCopied] = useState(false)
  const [search, setSearch] = useState('')
  const [tenants, setTenants] = useState(MOCK_TENANTS)

  const copyLink = () => {
    if (activationLink) {
      navigator.clipboard.writeText(activationLink)
      setCopied(true)
      setTimeout(() => setCopied(false), 2000)
    }
  }

  const handleCreate = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    const formData = new FormData(e.currentTarget)
    const name = formData.get('name') as string
    setActivationLink(`https://app.whatsapp-ai.com/activate?invitation=${Date.now()}&token=mock`)
    setShowCreateForm(false)
    setTenants([...tenants, { id: String(Date.now()), name, status: 'Active', createdAt: new Date().toISOString() }])
  }

  const handleSuspend = (id: string) => {
    setTenants(tenants.map(t => t.id === id ? { ...t, status: 'Suspended' } : t))
  }

  const handleReactivate = (id: string) => {
    setTenants(tenants.map(t => t.id === id ? { ...t, status: 'Active' } : t))
  }

  const filteredTenants = tenants.filter((t) => t.name.toLowerCase().includes(search.toLowerCase()))

  const getStatusBadge = (status: string) => {
    switch (status) {
      case 'Active': return <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-emerald-100 text-emerald-700"><CheckCircle2 className="w-3 h-3" /> Ativo</span>
      case 'Suspended': return <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-red-100 text-red-700"><XCircle className="w-3 h-3" /> Suspenso</span>
      default: return <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-slate-100 text-slate-700">{status}</span>
    }
  }

  return (
    <div className="h-full flex flex-col bg-slate-50">
      <div className="bg-white border-b border-slate-200 px-6 py-4">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-xl font-semibold text-slate-800">Tenants</h1>
            <p className="text-sm text-slate-500 mt-0.5">Gerencie os tenants da plataforma</p>
          </div>
          <button onClick={() => setShowCreateForm(true)} className="flex items-center gap-2 px-4 py-2.5 bg-emerald-500 text-white rounded-xl hover:bg-emerald-600 transition-colors shadow-sm">
            <Plus className="w-4 h-4" /> Novo Tenant
          </button>
        </div>
      </div>

      <div className="flex-1 overflow-auto p-6">
        {activationLink && (
          <div className="mb-4 p-4 bg-emerald-50 border border-emerald-200 rounded-xl animate-fade-in">
            <div className="flex items-start gap-3">
              <CheckCircle2 className="w-5 h-5 text-emerald-500 flex-shrink-0 mt-0.5" />
              <div className="flex-1">
                <p className="font-medium text-emerald-800">Tenant criado com sucesso!</p>
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
            <input type="text" placeholder="Buscar tenants..." value={search} onChange={(e) => setSearch(e.target.value)} className="w-full pl-10 pr-4 py-2.5 bg-white border border-slate-200 rounded-xl text-sm focus:ring-2 focus:ring-emerald-500 focus:border-transparent" />
          </div>
        </div>

        <div className="bg-white rounded-xl border border-slate-200 overflow-hidden shadow-sm">
          <table className="min-w-full">
            <thead>
              <tr className="bg-slate-50 border-b border-slate-200">
                <th className="px-6 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">Nome</th>
                <th className="px-6 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">Status</th>
                <th className="px-6 py-3 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">Criado em</th>
                <th className="px-6 py-3 text-right text-xs font-semibold text-slate-500 uppercase tracking-wider">Ações</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {filteredTenants.map((tenant) => (
                <tr key={tenant.id} className="hover:bg-slate-50 transition-colors">
                  <td className="px-6 py-4">
                    <div className="flex items-center gap-3">
                      <div className="w-9 h-9 rounded-lg bg-slate-100 flex items-center justify-center"><Building2 className="w-4 h-4 text-slate-500" /></div>
                      <span className="font-medium text-slate-800">{tenant.name}</span>
                    </div>
                  </td>
                  <td className="px-6 py-4">{getStatusBadge(tenant.status)}</td>
                  <td className="px-6 py-4 text-sm text-slate-500">{new Date(tenant.createdAt).toLocaleDateString('pt-BR')}</td>
                  <td className="px-6 py-4 text-right">
                    {tenant.status === 'Active' ? (
                      <button onClick={() => handleSuspend(tenant.id)} className="text-sm text-red-600 hover:text-red-700 font-medium">Suspender</button>
                    ) : (
                      <button onClick={() => handleReactivate(tenant.id)} className="text-sm text-emerald-600 hover:text-emerald-700 font-medium">Reativar</button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {showCreateForm && (
        <div className="fixed inset-0 bg-black/50 backdrop-blur-sm flex items-center justify-center z-50 animate-fade-in">
          <div className="bg-white rounded-2xl p-6 w-full max-w-md shadow-2xl animate-slide-in">
            <div className="flex items-center justify-between mb-6">
              <h2 className="text-lg font-semibold text-slate-800">Novo Tenant</h2>
              <button onClick={() => setShowCreateForm(false)} className="p-2 hover:bg-slate-100 rounded-lg"><X className="w-5 h-5 text-slate-400" /></button>
            </div>
            <form onSubmit={handleCreate} className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1.5">Nome do Tenant *</label>
                <input name="name" type="text" required className="w-full px-4 py-2.5 border border-slate-200 rounded-xl text-sm focus:ring-2 focus:ring-emerald-500 focus:border-transparent" placeholder="Nome da empresa" />
              </div>
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1.5">Email do Owner *</label>
                <input name="ownerEmail" type="email" required className="w-full px-4 py-2.5 border border-slate-200 rounded-xl text-sm focus:ring-2 focus:ring-emerald-500 focus:border-transparent" placeholder="owner@empresa.com" />
              </div>
              <div className="flex justify-end gap-3 pt-2">
                <button type="button" onClick={() => setShowCreateForm(false)} className="px-4 py-2.5 border border-slate-200 rounded-xl text-sm hover:bg-slate-50 transition-colors">Cancelar</button>
                <button type="submit" className="flex items-center gap-2 px-4 py-2.5 bg-emerald-500 text-white rounded-xl text-sm hover:bg-emerald-600 transition-colors">Criar Tenant</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}

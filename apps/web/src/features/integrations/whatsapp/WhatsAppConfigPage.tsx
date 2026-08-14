import { useState } from 'react'
import {
  Zap,
  CheckCircle2,
  AlertTriangle,
  Eye,
  EyeOff,
  ExternalLink,
  RefreshCw,
  Loader2,
} from 'lucide-react'

export function WhatsAppConfigPage() {
  const [wabaId, setWabaId] = useState('123456789012345')
  const [phoneNumberId, setPhoneNumberId] = useState('987654321098765')
  const [accessToken, setAccessToken] = useState('')
  const [showToken, setShowToken] = useState(false)
  const [isConfigured] = useState(true)
  const [isActive] = useState(true)
  const [success, setSuccess] = useState<string | null>(null)
  const [testResult, setTestResult] = useState<{ success: boolean; message: string } | null>(null)
  const [testing, setTesting] = useState(false)
  const [saving, setSaving] = useState(false)

  const handleTest = () => {
    setTesting(true)
    setTimeout(() => {
      setTestResult({ success: true, message: 'Conexão com a WhatsApp Cloud API estabelecida com sucesso! Webhook configurado corretamente.' })
      setTesting(false)
    }, 1500)
  }

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    setSaving(true)
    setTimeout(() => {
      setSaving(false)
      setAccessToken('')
      setSuccess('Configuração salva com sucesso!')
      setTimeout(() => setSuccess(null), 3000)
    }, 1000)
  }

  return (
    <div className="h-full flex flex-col bg-slate-50">
      <div className="bg-white border-b border-slate-200 px-6 py-4">
        <h1 className="text-xl font-semibold text-slate-800">Configuração WhatsApp</h1>
        <p className="text-sm text-slate-500 mt-0.5">Configure a conexão com a WhatsApp Cloud API</p>
      </div>

      <div className="flex-1 overflow-auto p-6">
        <div className="max-w-2xl">
          {/* Status Card */}
          {isConfigured && (
            <div className="mb-6 p-5 bg-white border border-slate-200 rounded-xl shadow-sm">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-3">
                  <div className={`w-10 h-10 rounded-xl flex items-center justify-center ${isActive ? 'bg-emerald-100' : 'bg-slate-100'}`}>
                    <Zap className={`w-5 h-5 ${isActive ? 'text-emerald-600' : 'text-slate-400'}`} />
                  </div>
                  <div>
                    <p className="font-medium text-slate-800">WhatsApp Configurado</p>
                    <p className="text-sm text-slate-500">Status: <span className={isActive ? 'text-emerald-600 font-medium' : 'text-slate-500'}>{isActive ? 'Ativo' : 'Inativo'}</span></p>
                  </div>
                </div>
                <button onClick={handleTest} disabled={testing} className="flex items-center gap-2 px-4 py-2.5 bg-slate-100 text-slate-700 rounded-xl hover:bg-slate-200 disabled:opacity-50 transition-colors text-sm">
                  {testing ? <Loader2 className="w-4 h-4 animate-spin" /> : <RefreshCw className="w-4 h-4" />}
                  {testing ? 'Testando...' : 'Testar Conexão'}
                </button>
              </div>

              {testResult && (
                <div className={`mt-4 p-4 rounded-xl flex items-start gap-3 ${testResult.success ? 'bg-emerald-50 border border-emerald-200' : 'bg-red-50 border border-red-200'}`}>
                  <CheckCircle2 className={`w-5 h-5 flex-shrink-0 mt-0.5 ${testResult.success ? 'text-emerald-500' : 'text-red-500'}`} />
                  <p className={`text-sm ${testResult.success ? 'text-emerald-700' : 'text-red-700'}`}>{testResult.message}</p>
                </div>
              )}
            </div>
          )}

          {success && (
            <div className="mb-4 flex items-center gap-3 p-4 bg-emerald-50 border border-emerald-200 rounded-xl text-emerald-700 animate-fade-in">
              <CheckCircle2 className="w-5 h-5 flex-shrink-0" /><p className="text-sm">{success}</p>
            </div>
          )}

          <div className="mb-6 p-4 bg-amber-50 border border-amber-200 rounded-xl">
            <div className="flex items-start gap-3">
              <AlertTriangle className="w-5 h-5 text-amber-500 flex-shrink-0 mt-0.5" />
              <div>
                <p className="text-sm font-medium text-amber-800">Importante</p>
                <p className="text-sm text-amber-700 mt-1">O Access Token é armazenado de forma segura e nunca será exibido novamente.</p>
              </div>
            </div>
          </div>

          <form onSubmit={handleSubmit} className="bg-white border border-slate-200 rounded-xl p-6 shadow-sm space-y-5">
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1.5">WABA ID (WhatsApp Business Account ID) *</label>
              <input type="text" value={wabaId} onChange={(e) => setWabaId(e.target.value)} required className="w-full px-4 py-2.5 border border-slate-200 rounded-xl text-sm focus:ring-2 focus:ring-emerald-500 focus:border-transparent" placeholder="Ex: 1234567890123456" />
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1.5">Phone Number ID *</label>
              <input type="text" value={phoneNumberId} onChange={(e) => setPhoneNumberId(e.target.value)} required className="w-full px-4 py-2.5 border border-slate-200 rounded-xl text-sm focus:ring-2 focus:ring-emerald-500 focus:border-transparent" placeholder="Ex: 1234567890123456" />
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1.5">Access Token (deixe vazio para manter o atual)</label>
              <div className="relative">
                <input type={showToken ? 'text' : 'password'} value={accessToken} onChange={(e) => setAccessToken(e.target.value)} className="w-full px-4 py-2.5 pr-12 border border-slate-200 rounded-xl text-sm focus:ring-2 focus:ring-emerald-500 focus:border-transparent" placeholder="Insira o token de acesso" />
                <button type="button" onClick={() => setShowToken(!showToken)} className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600">
                  {showToken ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                </button>
              </div>
            </div>
            <div className="flex gap-3 pt-2">
              <button type="submit" disabled={saving} className="flex items-center gap-2 px-5 py-2.5 bg-emerald-500 text-white rounded-xl text-sm font-medium hover:bg-emerald-600 disabled:opacity-50 transition-colors">
                {saving && <Loader2 className="w-4 h-4 animate-spin" />}
                {saving ? 'Salvando...' : 'Salvar Configuração'}
              </button>
            </div>
          </form>

          <div className="mt-6 bg-white border border-slate-200 rounded-xl p-6 shadow-sm">
            <h3 className="font-medium text-slate-800 mb-4 flex items-center gap-2"><ExternalLink className="w-4 h-4" /> Como obter esses valores</h3>
            <ol className="space-y-3 text-sm text-slate-600">
              {['Acesse o Meta Business Suite', 'Navegue para Configurações > Contas Comerciais', 'Selecione sua conta WhatsApp Business', 'Copie o WABA ID dos detalhes da conta', 'Vá para WhatsApp > Configuração da API', 'Copie o Phone Number ID', 'Gere ou copie seu Access Token'].map((step, i) => (
                <li key={i} className="flex items-start gap-2">
                  <span className="w-5 h-5 rounded-full bg-slate-100 flex items-center justify-center text-[10px] font-semibold text-slate-500 flex-shrink-0 mt-0.5">{i + 1}</span>
                  {step}
                </li>
              ))}
            </ol>
          </div>
        </div>
      </div>
    </div>
  )
}

import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../../../lib/auth'
import {
  Zap,
  CheckCircle2,
  Eye,
  EyeOff,
  RefreshCw,
  Loader2,
  QrCode,
  Wifi,
  WifiOff,
  Smartphone,
} from 'lucide-react'

export function WhatsAppConfigPage() {
  const { isOperator } = useAuth()
  const queryClient = useQueryClient()
  const [wabaId, setWabaId] = useState('')
  const [phoneNumberId, setPhoneNumberId] = useState('')
  const [accessToken, setAccessToken] = useState('')
  const [showToken, setShowToken] = useState(false)
  const [success, setSuccess] = useState<string | null>(null)
  const [testResult, setTestResult] = useState<{ success: boolean; message: string } | null>(null)
  const [connectionMode, setConnectionMode] = useState<'qrcode' | 'api'>('qrcode')

  const { isLoading } = useQuery({
    queryKey: ['whatsapp-config'],
    queryFn: async () => {
      const res = await fetch('/api/integrations/whatsapp', { credentials: 'include' })
      return res.json()
    },
  })

  const { data: qrData, isLoading: qrLoading, refetch: refetchQr } = useQuery({
    queryKey: ['whatsapp-qrcode'],
    queryFn: async () => {
      const res = await fetch('/api/integrations/whatsapp/qrcode', { credentials: 'include' })
      if (!res.ok) return null
      return res.json()
    },
    enabled: connectionMode === 'qrcode',
    refetchInterval: 5000,
  })

  const { data: sessionStatus } = useQuery({
    queryKey: ['whatsapp-session'],
    queryFn: async () => {
      const res = await fetch('/api/integrations/whatsapp/session/status', { credentials: 'include' })
      return res.json()
    },
    refetchInterval: 5000, // Poll every 5 seconds
  })

  const disconnectMutation = useMutation({
    mutationFn: async () => {
      const res = await fetch('/api/integrations/whatsapp/session/disconnect', {
        method: 'POST',
        credentials: 'include',
      })
      return res.json()
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['whatsapp-session'] })
      queryClient.invalidateQueries({ queryKey: ['whatsapp-qrcode'] })
    },
  })

  const saveMutation = useMutation({
    mutationFn: async () => {
      const res = await fetch('/api/integrations/whatsapp', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({ wabaId, phoneNumberId, accessToken }),
      })
      if (!res.ok) throw new Error('Erro ao salvar')
      return res.json()
    },
    onSuccess: () => {
      setSuccess('Configuração salva com sucesso!')
      setAccessToken('')
      queryClient.invalidateQueries({ queryKey: ['whatsapp-config'] })
      setTimeout(() => setSuccess(null), 3000)
    },
  })

  const testMutation = useMutation({
    mutationFn: async () => {
      const res = await fetch('/api/integrations/whatsapp/test-connection', {
        method: 'POST',
        credentials: 'include',
      })
      return res.json()
    },
    onSuccess: (data) => setTestResult(data),
  })

  if (isLoading) {
    return (
      <div className="h-full flex items-center justify-center">
        <Loader2 className="w-8 h-8 text-emerald-500 animate-spin" />
      </div>
    )
  }

  return (
    <div className="h-full overflow-y-auto">
      <div className="max-w-2xl mx-auto px-6 py-8">
        <div className="flex items-center gap-3 mb-8">
          <div className="w-10 h-10 rounded-lg bg-emerald-50 text-emerald-600 flex items-center justify-center">
            <Zap className="w-5 h-5" />
          </div>
          <div>
            <h1 className="text-xl font-bold text-slate-900">Configuração WhatsApp</h1>
            <p className="text-sm text-slate-500">Conecte seu WhatsApp ao sistema</p>
          </div>
        </div>

        {/* Connection Mode Toggle */}
        {!isOperator && <div className="mb-6 p-1 bg-slate-100 rounded-xl flex">
          <button
            onClick={() => setConnectionMode('qrcode')}
            className={`flex-1 flex items-center justify-center gap-2 py-2.5 rounded-lg text-sm font-medium transition-colors ${
              connectionMode === 'qrcode'
                ? 'bg-white text-slate-900 shadow-sm'
                : 'text-slate-600 hover:text-slate-900'
            }`}
          >
            <QrCode className="w-4 h-4" />
            QR Code
          </button>
          <button
            onClick={() => setConnectionMode('api')}
            className={`flex-1 flex items-center justify-center gap-2 py-2.5 rounded-lg text-sm font-medium transition-colors ${
              connectionMode === 'api'
                ? 'bg-white text-slate-900 shadow-sm'
                : 'text-slate-600 hover:text-slate-900'
            }`}
          >
            <Smartphone className="w-4 h-4" />
            API Oficial
          </button>
        </div>}

        {/* QR Code Connection */}
        {connectionMode === 'qrcode' && (
          <div className="mb-6 p-6 bg-white border border-slate-200 rounded-xl shadow-sm">
            <div className="flex items-center gap-3 mb-4">
              <QrCode className="w-5 h-5 text-slate-600" />
              <h2 className="text-lg font-semibold text-slate-800">Conectar via QR Code</h2>
            </div>

            {/* Session Status */}
            <div className={`mb-4 p-4 rounded-xl flex items-center gap-3 ${
              sessionStatus?.isConnected
                ? 'bg-emerald-50 border border-emerald-200'
                : 'bg-slate-50 border border-slate-200'
            }`}>
              {sessionStatus?.isConnected ? (
                <>
                  <Wifi className="w-5 h-5 text-emerald-600" />
                  <div>
                    <p className="font-medium text-emerald-800">Conectado</p>
                    <p className="text-sm text-emerald-600">{sessionStatus.phoneNumber}</p>
                  </div>
                  <button
                    onClick={() => disconnectMutation.mutate()}
                    disabled={disconnectMutation.isPending}
                    className="ml-auto px-3 py-1.5 text-sm text-red-600 hover:bg-red-50 rounded-lg"
                  >
                    Desconectar
                  </button>
                </>
              ) : (
                <>
                  <WifiOff className="w-5 h-5 text-slate-400" />
                  <div>
                    <p className="font-medium text-slate-700">Desconectado</p>
                    <p className="text-sm text-slate-500">Abra o WhatsApp Web para parear o aparelho</p>
                  </div>
                </>
              )}
            </div>

            {/* QR Code Display */}
            {!sessionStatus?.isConnected && (
              <div className="flex flex-col items-center gap-4">
                {qrLoading ? (
                  <div className="flex items-center justify-center py-12">
                    <Loader2 className="w-8 h-8 text-emerald-500 animate-spin" />
                  </div>
                ) : qrData?.qrCode ? (
                  <>
                    <div className="p-4 bg-white border-2 border-slate-200 rounded-xl">
                      <img
                        src={`data:image/png;base64,${qrData.qrCode}`}
                        alt="QR Code WhatsApp"
                        className="w-64 h-64"
                      />
                    </div>
                    <div className="text-center">
                      <p className="text-sm text-slate-600">
                        1. Abra o WhatsApp no seu celular
                      </p>
                      <p className="text-sm text-slate-600">
                        2. Vá em <strong>Configurações → Aparelhos conectados</strong>
                      </p>
                      <p className="text-sm text-slate-600">
                        3. Escaneie o QR Code acima
                      </p>
                    </div>
                    <button
                      onClick={() => refetchQr()}
                      className="flex items-center gap-2 px-4 py-2 bg-slate-100 text-slate-700 rounded-lg hover:bg-slate-200 text-sm"
                    >
                      <RefreshCw className="w-4 h-4" />
                      Atualizar QR Code
                    </button>
                  </>
                ) : (
                  <div className="text-center py-8 text-slate-500">
                    <p>Erro ao gerar QR Code. Tente novamente.</p>
                    <button
                      onClick={() => refetchQr()}
                      className="mt-2 px-4 py-2 bg-emerald-500 text-white rounded-lg hover:bg-emerald-600 text-sm"
                    >
                      Tentar novamente
                    </button>
                  </div>
                )}
              </div>
            )}

            {/* Connected Info */}
            {sessionStatus?.isConnected && (
              <div className="text-center py-4">
                <CheckCircle2 className="w-12 h-12 text-emerald-500 mx-auto mb-3" />
                <p className="text-lg font-medium text-slate-900">WhatsApp Conectado!</p>
                <p className="text-sm text-slate-500 mt-1">
                  Seu WhatsApp está conectado e pronto para receber mensagens.
                </p>
              </div>
            )}
          </div>
        )}

        {/* API Configuration */}
        {!isOperator && connectionMode === 'api' && (
          <div className="bg-white rounded-xl border border-slate-200 p-6">
            <h2 className="font-semibold text-slate-900 mb-4">Credenciais da API Oficial</h2>

            {success && (
              <div className="mb-4 p-3 bg-emerald-50 border border-emerald-200 rounded-lg text-emerald-700 text-sm flex items-center gap-2">
                <CheckCircle2 className="w-4 h-4" />
                {success}
              </div>
            )}

            <form onSubmit={(e) => { e.preventDefault(); saveMutation.mutate() }} className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1">WABA ID</label>
                <input
                  type="text"
                  value={wabaId}
                  onChange={(e) => setWabaId(e.target.value)}
                  placeholder="ID da WhatsApp Business Account"
                  className="w-full px-4 py-2.5 border border-slate-300 rounded-lg focus:ring-2 focus:ring-emerald-500 focus:border-transparent"
                />
              </div>

              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1">Phone Number ID</label>
                <input
                  type="text"
                  value={phoneNumberId}
                  onChange={(e) => setPhoneNumberId(e.target.value)}
                  placeholder="ID do número de telefone"
                  className="w-full px-4 py-2.5 border border-slate-300 rounded-lg focus:ring-2 focus:ring-emerald-500 focus:border-transparent"
                />
              </div>

              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1">Access Token</label>
                <div className="relative">
                  <input
                    type={showToken ? 'text' : 'password'}
                    value={accessToken}
                    onChange={(e) => setAccessToken(e.target.value)}
                    placeholder="Token de acesso permanente"
                    className="w-full px-4 py-2.5 pr-10 border border-slate-300 rounded-lg focus:ring-2 focus:ring-emerald-500 focus:border-transparent"
                  />
                  <button
                    type="button"
                    onClick={() => setShowToken(!showToken)}
                    className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600"
                  >
                    {showToken ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                  </button>
                </div>
              </div>

              <div className="flex gap-3 pt-2">
                <button
                  type="submit"
                  disabled={saveMutation.isPending}
                  className="flex items-center gap-2 px-5 py-2.5 bg-emerald-500 hover:bg-emerald-600 text-white rounded-lg font-medium transition-colors disabled:opacity-50"
                >
                  {saveMutation.isPending ? (
                    <Loader2 className="w-4 h-4 animate-spin" />
                  ) : (
                    <CheckCircle2 className="w-4 h-4" />
                  )}
                  Salvar
                </button>

                <button
                  type="button"
                  onClick={() => testMutation.mutate()}
                  disabled={testMutation.isPending}
                  className="flex items-center gap-2 px-5 py-2.5 bg-slate-100 text-slate-700 rounded-lg hover:bg-slate-200 disabled:opacity-50 transition-colors"
                >
                  {testMutation.isPending ? (
                    <Loader2 className="w-4 h-4 animate-spin" />
                  ) : (
                    <RefreshCw className="w-4 h-4" />
                  )}
                  Testar
                </button>
              </div>
            </form>

            {testResult && (
              <div className={`mt-4 p-4 rounded-xl flex items-start gap-3 ${testResult.success ? 'bg-emerald-50 border border-emerald-200' : 'bg-red-50 border border-red-200'}`}>
                <CheckCircle2 className={`w-5 h-5 flex-shrink-0 mt-0.5 ${testResult.success ? 'text-emerald-500' : 'text-red-500'}`} />
                <p className={`text-sm ${testResult.success ? 'text-emerald-700' : 'text-red-700'}`}>{testResult.message}</p>
              </div>
            )}

            {/* Help Section */}
            <div className="mt-6 p-4 bg-blue-50 border border-blue-200 rounded-xl">
              <h3 className="font-medium text-blue-800 mb-2">Como obter as credenciais?</h3>
              <ol className="text-sm text-blue-700 space-y-1.5 list-decimal list-inside">
                <li>Acesse o <a href="https://developers.facebook.com" target="_blank" rel="noopener noreferrer" className="underline">Meta for Developers</a></li>
                <li>Crie um app e adicione o produto WhatsApp</li>
                <li>Configure o webhook e obtenha o WABA ID</li>
                <li>Gere um token de acesso permanente</li>
              </ol>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}

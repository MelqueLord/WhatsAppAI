import { useState } from 'react'
import { useSearchParams, useNavigate } from 'react-router-dom'
import { useMutation, useQuery } from '@tanstack/react-query'
import {
  MessageSquare,
  Eye,
  EyeOff,
  CheckCircle2,
  XCircle,
  Clock,
  ArrowRight,
  Loader2,
} from 'lucide-react'

interface InvitationInfo {
  id: string
  email: string
  purpose: string
  isUsable: boolean
  expiresAt: string
}

interface ActivateRequest {
  invitationId: string
  token: string
  password: string
}

interface ActivateResponse {
  userId: string
  email: string
  tenantId: string
  role: string
}

async function fetchInvitationInfo(invitationId: string): Promise<InvitationInfo> {
  const response = await fetch(`/api/auth/activate/invitation/${invitationId}`, {
    credentials: 'include',
  })
  if (!response.ok) throw new Error('Failed to fetch invitation info')
  return response.json()
}

async function activateAccount(request: ActivateRequest): Promise<ActivateResponse> {
  const response = await fetch('/api/auth/activate', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify(request),
  })
  if (!response.ok) {
    const error = await response.json()
    throw new Error(error.error || 'Failed to activate account')
  }
  return response.json()
}

export function ActivatePage() {
  const [searchParams] = useSearchParams()
  const navigate = useNavigate()
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const invitationId = searchParams.get('invitation')
  const token = searchParams.get('token')

  const { data: invitationInfo, isLoading: isLoadingInfo } = useQuery({
    queryKey: ['invitation', invitationId],
    queryFn: () => fetchInvitationInfo(invitationId!),
    enabled: !!invitationId,
  })

  const activateMutation = useMutation({
    mutationFn: activateAccount,
    onSuccess: () => navigate('/inbox'),
    onError: (err: Error) => setError(err.message),
  })

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)

    if (!invitationId || !token) {
      setError('Link de ativação inválido.')
      return
    }

    if (password !== confirmPassword) {
      setError('As senhas não coincidem.')
      return
    }

    if (password.length < 8) {
      setError('A senha deve ter pelo menos 8 caracteres.')
      return
    }

    activateMutation.mutate({ invitationId, token, password })
  }

  if (!invitationId || !token) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-slate-900 p-4">
        <div className="w-full max-w-md bg-white rounded-2xl p-8 shadow-2xl text-center animate-fade-in">
          <div className="w-16 h-16 mx-auto mb-4 rounded-2xl bg-red-100 flex items-center justify-center">
            <XCircle className="w-8 h-8 text-red-500" />
          </div>
          <h1 className="text-xl font-semibold text-slate-800 mb-2">Link Inválido</h1>
          <p className="text-slate-500">
            Este link de ativação é inválido. Por favor, verifique o link e tente novamente.
          </p>
        </div>
      </div>
    )
  }

  if (isLoadingInfo) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-slate-900">
        <div className="flex flex-col items-center gap-4">
          <Loader2 className="w-10 h-10 text-emerald-500 animate-spin" />
          <p className="text-slate-400">Verificando convite...</p>
        </div>
      </div>
    )
  }

  if (invitationInfo && !invitationInfo.isUsable) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-slate-900 p-4">
        <div className="w-full max-w-md bg-white rounded-2xl p-8 shadow-2xl text-center animate-fade-in">
          <div className="w-16 h-16 mx-auto mb-4 rounded-2xl bg-amber-100 flex items-center justify-center">
            <Clock className="w-8 h-8 text-amber-500" />
          </div>
          <h1 className="text-xl font-semibold text-slate-800 mb-2">Link Expirado</h1>
          <p className="text-slate-500">
            Este link de ativação já expirou ou foi utilizado.
            Solicite um novo convite.
          </p>
        </div>
      </div>
    )
  }

  return (
    <div className="min-h-screen flex bg-slate-900">
      {/* Left Panel */}
      <div className="hidden lg:flex lg:w-1/2 relative overflow-hidden">
        <div className="absolute inset-0 bg-gradient-to-br from-emerald-600 via-emerald-700 to-teal-800" />
        <div className="absolute inset-0 opacity-10">
          <div className="absolute top-20 left-20 w-72 h-72 bg-white rounded-full blur-3xl" />
          <div className="absolute bottom-20 right-20 w-96 h-96 bg-emerald-300 rounded-full blur-3xl" />
        </div>
        <div className="relative z-10 flex flex-col justify-center px-12">
          <div className="w-16 h-16 rounded-2xl bg-white/20 backdrop-blur-sm flex items-center justify-center mb-8">
            <MessageSquare className="w-8 h-8 text-white" />
          </div>
          <h1 className="text-4xl font-bold text-white mb-4">Ative sua conta</h1>
          <p className="text-emerald-100 text-lg max-w-md">
            Defina sua senha para começar a usar a plataforma de atendimento WhatsApp com IA.
          </p>
        </div>
      </div>

      {/* Right Panel - Form */}
      <div className="flex-1 flex items-center justify-center p-8">
        <div className="w-full max-w-md">
          {/* Mobile Logo */}
          <div className="lg:hidden flex items-center gap-3 mb-10">
            <div className="w-12 h-12 rounded-xl bg-emerald-500 flex items-center justify-center">
              <MessageSquare className="w-6 h-6 text-white" />
            </div>
            <div>
              <h1 className="text-xl font-bold text-white">WhatsApp AI</h1>
              <p className="text-xs text-slate-400 uppercase tracking-widest">Platform</p>
            </div>
          </div>

          <div className="mb-8">
            <h2 className="text-2xl font-bold text-white mb-2">Ative sua conta</h2>
            <p className="text-slate-400">Defina uma senha para acessar a plataforma.</p>
          </div>

          {invitationInfo && (
            <div className="mb-6 p-4 bg-slate-800 border border-slate-700 rounded-xl">
              <div className="flex items-center gap-3 mb-2">
                <CheckCircle2 className="w-5 h-5 text-emerald-400" />
                <p className="text-sm font-medium text-white">Convite válido</p>
              </div>
              <div className="ml-8 space-y-1">
                <p className="text-sm text-slate-400">
                  <span className="text-slate-500">Email:</span> {invitationInfo.email}
                </p>
                <p className="text-sm text-slate-400">
                  <span className="text-slate-500">Função:</span> {invitationInfo.purpose}
                </p>
              </div>
            </div>
          )}

          <form onSubmit={handleSubmit} className="space-y-5">
            {error && (
              <div className="p-4 rounded-xl bg-red-500/10 border border-red-500/20 text-red-400 text-sm flex items-start gap-3">
                <XCircle className="w-5 h-5 flex-shrink-0 mt-0.5" />
                {error}
              </div>
            )}

            <div>
              <label className="block text-sm font-medium text-slate-300 mb-2">Senha *</label>
              <div className="relative">
                <input
                  type={showPassword ? 'text' : 'password'}
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  required
                  minLength={8}
                  className="w-full px-4 py-3 bg-slate-800 border border-slate-700 rounded-xl text-white placeholder-slate-500 focus:ring-2 focus:ring-emerald-500 focus:border-transparent pr-12"
                  placeholder="Mínimo 8 caracteres"
                />
                <button
                  type="button"
                  onClick={() => setShowPassword(!showPassword)}
                  className="absolute right-4 top-1/2 -translate-y-1/2 text-slate-400 hover:text-white transition-colors"
                >
                  {showPassword ? <EyeOff className="w-5 h-5" /> : <Eye className="w-5 h-5" />}
                </button>
              </div>
            </div>

            <div>
              <label className="block text-sm font-medium text-slate-300 mb-2">Confirmar Senha *</label>
              <input
                type="password"
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                required
                minLength={8}
                className="w-full px-4 py-3 bg-slate-800 border border-slate-700 rounded-xl text-white placeholder-slate-500 focus:ring-2 focus:ring-emerald-500 focus:border-transparent"
                placeholder="Repita a senha"
              />
            </div>

            <button
              type="submit"
              disabled={activateMutation.isPending}
              className="w-full flex items-center justify-center gap-2 px-6 py-3.5 bg-emerald-500 hover:bg-emerald-600 text-white font-semibold rounded-xl transition-all duration-200 disabled:opacity-50 group"
            >
              {activateMutation.isPending ? (
                <Loader2 className="w-5 h-5 animate-spin" />
              ) : (
                <>
                  Ativar Conta
                  <ArrowRight className="w-4 h-4 group-hover:translate-x-1 transition-transform" />
                </>
              )}
            </button>
          </form>
        </div>
      </div>
    </div>
  )
}

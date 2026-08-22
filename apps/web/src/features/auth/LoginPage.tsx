import { useState } from 'react'
import { useNavigate, Navigate } from 'react-router-dom'
import { useAuth } from '../../lib/auth'
import { Mail, Lock, Eye, EyeOff, ArrowRight, MessageCircle } from 'lucide-react'

function AtenzLogo({ size = 'lg' }: { size?: 'lg' | 'sm' }) {
  const s = size === 'lg' ? 'w-12 h-12' : 'w-7 h-7'
  return (
    <svg viewBox="0 0 48 48" className={s} fill="none">
      <path d="M24 6L44 42h-9l-4-8H17l-4 8H4L24 6z" fill="url(#ag)" />
      <circle cx="6" cy="40" r="2" fill="#22c55e" />
      <circle cx="11" cy="44" r="2" fill="#22c55e" />
      <circle cx="11" cy="36" r="2" fill="#3b82f6" />
      <defs>
        <linearGradient id="ag" x1="0" y1="0" x2="48" y2="48">
          <stop stopColor="#3b82f6" />
          <stop offset="1" stopColor="#22c55e" />
        </linearGradient>
      </defs>
    </svg>
  )
}

export function LoginPage() {
  const { login, isAuthenticated, isLoading } = useAuth()
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const [remember, setRemember] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  if (isLoading) {
    return (
      <div className="h-screen flex items-center justify-center bg-[#070b16]">
        <div className="w-12 h-12 border-4 border-emerald-200 border-t-emerald-500 rounded-full animate-spin" />
      </div>
    )
  }

  if (isAuthenticated) {
    return <Navigate to="/inbox" replace />
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    setIsSubmitting(true)

    try {
      await login(email, password)
      navigate('/dashboard')
    } catch {
      // eslint-disable-next-line no-empty
      setError('Email ou senha incorretos.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <div className="min-h-screen flex bg-[#070b16] text-white">
      {/* Left Panel - Branding */}
      <div className="hidden lg:flex lg:w-1/2 xl:w-[55%] relative overflow-hidden border-r border-white/5">
        {/* Glow waves */}
        <div className="absolute inset-x-0 bottom-0 h-72 pointer-events-none">
          <svg viewBox="0 0 800 300" preserveAspectRatio="none" className="w-full h-full opacity-60">
            <path d="M0 220 Q 400 60 800 220" stroke="url(#wg1)" strokeWidth="2" fill="none" />
            <path d="M0 240 Q 400 90 800 240" stroke="url(#wg1)" strokeWidth="1.5" fill="none" opacity="0.7" />
            <path d="M0 260 Q 400 120 800 260" stroke="url(#wg1)" strokeWidth="1" fill="none" opacity="0.5" />
            <defs>
              <linearGradient id="wg1" x1="0" y1="0" x2="800" y2="0" gradientUnits="userSpaceOnUse">
                <stop stopColor="#06b6d4" />
                <stop offset="0.5" stopColor="#22d3ee" />
                <stop offset="1" stopColor="#3b82f6" />
              </linearGradient>
            </defs>
          </svg>
        </div>

        <div className="relative z-10 flex flex-col justify-center px-12 xl:px-20 w-full">
          {/* Logo */}
          <div className="flex items-center gap-3 mb-16">
            <AtenzLogo />
            <div>
              <div className="flex items-baseline">
                <span className="text-4xl font-extrabold tracking-wide">ATEN</span>
                <span className="text-4xl font-extrabold tracking-wide bg-gradient-to-r from-blue-400 to-emerald-400 bg-clip-text text-transparent">Z</span>
              </div>
              <p className="text-[11px] tracking-[0.25em] text-slate-300 uppercase">
                API Oficial <span className="text-emerald-400">•</span> IA <span className="text-emerald-400">•</span> Automação
              </p>
            </div>
          </div>

          <h1 className="text-5xl xl:text-6xl font-extrabold leading-tight mb-2">
            Mais <span className="text-emerald-400">agilidade.</span>
          </h1>
          <h1 className="text-5xl xl:text-6xl font-extrabold leading-tight mb-8">
            Mais <span className="text-blue-500">resultado.</span>
          </h1>

          <div className="flex items-start gap-3 max-w-md mb-14">
            <MessageCircle className="w-6 h-6 text-emerald-400 mt-1 shrink-0" />
            <p className="text-slate-300 text-lg leading-relaxed">
              Atendimento inteligente que vai{' '}
              <span className="text-emerald-400 font-semibold">revolucionar</span> a sua operação.
            </p>
          </div>

          <div className="flex flex-col items-center gap-2 w-40">
            <svg viewBox="0 0 48 28" className="w-16 h-10" fill="none">
              <path d="M14 24c-5 0-9-4.5-9-10S9 4 14 4c4 0 7 3 10 6 3-3 6-6 10-6 5 0 9 4.5 9 10s-4 10-9 10c-4 0-7-3-10-6-3 3-6 6-10 6z" stroke="#3b82f6" strokeWidth="3" />
            </svg>
            <p className="text-sm font-semibold tracking-wide">API Oficial</p>
          </div>
        </div>
      </div>

      {/* Right Panel - Login Form */}
      <div className="flex-1 flex items-center justify-center p-8 relative">
        <div className="w-full max-w-md">
          <div className="flex flex-col items-center mb-8">
            <div className="w-20 h-20 rounded-3xl bg-white/5 border border-white/10 flex items-center justify-center mb-6">
              <AtenzLogo size="sm" />
            </div>
            <h2 className="text-3xl font-bold mb-2">Bem-vindo de volta!</h2>
            <p className="text-slate-400">Acesse sua conta para continuar</p>
          </div>

          <form onSubmit={handleSubmit} className="space-y-5">
            {error && (
              <div className="p-4 rounded-xl bg-red-500/10 border border-red-500/20 text-red-400 text-sm">
                {error}
              </div>
            )}

            <div>
              <label className="block text-sm font-medium text-slate-300 mb-2">E-mail</label>
              <div className="relative">
                <Mail className="absolute left-4 top-1/2 -translate-y-1/2 w-5 h-5 text-slate-500" />
                <input
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  required
                  className="w-full pl-12 pr-4 py-3.5 bg-white/5 border border-white/10 rounded-2xl text-white placeholder-slate-500 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-all"
                  placeholder="seu@email.com"
                />
              </div>
            </div>

            <div>
              <label className="block text-sm font-medium text-slate-300 mb-2">Senha</label>
              <div className="relative">
                <Lock className="absolute left-4 top-1/2 -translate-y-1/2 w-5 h-5 text-slate-500" />
                <input
                  type={showPassword ? 'text' : 'password'}
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  required
                  className="w-full pl-12 pr-12 py-3.5 bg-white/5 border border-white/10 rounded-2xl text-white placeholder-slate-500 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-all"
                  placeholder="••••••••••"
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

            <div className="flex items-center justify-between">
              <label className="flex items-center gap-2 cursor-pointer">
                <input
                  type="checkbox"
                  checked={remember}
                  onChange={(e) => setRemember(e.target.checked)}
                  className="w-4 h-4 rounded accent-blue-500"
                />
                <span className="text-sm text-slate-300">Lembrar de mim</span>
              </label>
              <a href="#" className="text-sm text-blue-400 hover:text-blue-300 transition-colors">
                Esqueci minha senha
              </a>
            </div>

            <button
              type="submit"
              disabled={isSubmitting}
              className="w-full flex items-center justify-center gap-2 px-6 py-4 bg-gradient-to-r from-blue-600 to-emerald-500 hover:from-blue-500 hover:to-emerald-400 text-white font-semibold rounded-2xl transition-all duration-200 disabled:opacity-50 disabled:cursor-not-allowed group shadow-lg shadow-blue-900/40"
            >
              {isSubmitting ? (
                <div className="w-5 h-5 border-2 border-white/30 border-t-white rounded-full animate-spin" />
              ) : (
                <>
                  Entrar
                  <ArrowRight className="w-4 h-4 group-hover:translate-x-1 transition-transform" />
                </>
              )}
            </button>
          </form>

          <p className="mt-10 text-center text-xs text-slate-500">
            © 2025 ATENZ. Todos os direitos reservados.
          </p>
        </div>
      </div>
    </div>
  )
}

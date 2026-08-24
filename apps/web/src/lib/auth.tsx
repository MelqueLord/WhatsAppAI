import { createContext, useContext, useCallback, type ReactNode } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { api, clearStoredToken, type User } from './api'

interface AuthContextType {
  user: User | null
  isLoading: boolean
  isAuthenticated: boolean
  isPlatformAdmin: boolean
  isTenantOwner: boolean
  isOperator: boolean
  mustChangePassword: boolean
  login: (email: string, password: string) => Promise<void>
  logout: () => Promise<void>
  changePassword: (currentPassword: string, newPassword: string) => Promise<void>
  refetch: () => void
}

const AuthContext = createContext<AuthContextType | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()

  const { data: user, isLoading, refetch } = useQuery({
    queryKey: ['auth', 'me'],
    queryFn: async () => {
      try {
        return await api.auth.getMe()
      } catch {
        return null
      }
    },
    retry: false,
    staleTime: 60000,
  })

  const login = useCallback(async (email: string, password: string) => {
    const user = await api.auth.login(email, password)
    console.log('[auth] login response user:', user)
    console.log('[auth] token in storage:', localStorage.getItem('whatsappai.token'))
    const result = await refetch()
    console.log('[auth] refetch result:', result.data)
  }, [refetch])

  const logout = useCallback(async () => {
    try {
      await api.auth.logout()
    } catch {
      // eslint-disable-next-line no-empty
      // Ignore errors - still redirect
    }
    clearStoredToken()
    queryClient.clear()
    window.location.replace('/login')
  }, [queryClient])

  const changePassword = useCallback(async (currentPassword: string, newPassword: string) => {
    await api.auth.changePassword(currentPassword, newPassword)
    await refetch()
  }, [refetch])

  const value: AuthContextType = {
    user: user ?? null,
    isLoading,
    isAuthenticated: !!user,
    isPlatformAdmin: user?.isPlatformAdmin ?? false,
    isTenantOwner: user?.role === 'TenantOwner',
    isOperator: user?.role === 'Operator',
    mustChangePassword: user?.mustChangePassword ?? false,
    login,
    logout,
    changePassword,
    refetch,
  }

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth must be used within AuthProvider')
  return context
}

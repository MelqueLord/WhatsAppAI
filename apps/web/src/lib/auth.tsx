import { createContext, useContext, useCallback, type ReactNode } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { api, type User } from './api'

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
    queryFn: api.auth.getMe,
    retry: false,
    staleTime: 60000,
  })

  const login = useCallback(async (email: string, password: string) => {
    await api.auth.login(email, password)
    await refetch()
  }, [refetch])

  const logout = useCallback(async () => {
    await api.auth.logout()
    queryClient.clear()
    window.location.href = '/login'
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

export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth must be used within AuthProvider')
  return context
}

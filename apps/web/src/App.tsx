import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { AuthProvider, useAuth } from './lib/auth'
import { Layout } from './components/Layout'
import { LoginPage } from './features/auth/LoginPage'
import { ActivatePage } from './features/auth/activate/ActivatePage'
import { DashboardPage } from './features/dashboard/DashboardPage'
import { InboxPage } from './features/inbox/InboxPage'
import { OperatorsPage } from './features/operators/OperatorsPage'
import { WhatsAppConfigPage } from './features/integrations/whatsapp/WhatsAppConfigPage'
import { AiConfigPage } from './features/integrations/ai/AiConfigPage'
import { BotConfigPage } from './features/bot/BotConfigPage'
import { KnowledgePage } from './features/knowledge/KnowledgePage'
import { ClientTagsPage } from './features/tags/ClientTagsPage'

import { UsagePage } from './features/usage/UsagePage'
import { AdminTenantsPage } from './features/admin/tenants/AdminTenantsPage'
import { WebhookEventsPage } from './features/admin/webhooks/WebhookEventsPage'
import { ContactsPage } from './features/contacts/ContactsPage'
import { Loader2 } from 'lucide-react'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30000,
      retry: 1,
    },
  },
})

function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, isLoading } = useAuth()

  if (isLoading) {
    return (
      <div className="h-screen flex items-center justify-center bg-slate-50">
        <Loader2 className="w-8 h-8 text-emerald-500 animate-spin" />
      </div>
    )
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />
  }

  return <>{children}</>
}

function AdminRoute({ children }: { children: React.ReactNode }) {
  const { isPlatformAdmin } = useAuth()
  if (!isPlatformAdmin) return <Navigate to="/dashboard" replace />
  return <>{children}</>
}

function OwnerRoute({ children }: { children: React.ReactNode }) {
  const { isTenantOwner } = useAuth()
  if (!isTenantOwner) return <Navigate to="/dashboard" replace />
  return <>{children}</>
}

function OperatorRoute({ children }: { children: React.ReactNode }) {
  const { isOperator } = useAuth()
  // Operators can only access inbox
  if (isOperator) return <Navigate to="/inbox" replace />
  return <>{children}</>
}

function NavigateToHome() {
  const { isOperator } = useAuth()
  return <Navigate to={isOperator ? '/inbox' : '/dashboard'} replace />
}

function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <AuthProvider>
          <Routes>
            <Route path="/login" element={<LoginPage />} />
            <Route path="/activate" element={<ActivatePage />} />
            <Route
              element={
                <ProtectedRoute>
                  <Layout />
                </ProtectedRoute>
              }
            >
              <Route path="/dashboard" element={<OperatorRoute><DashboardPage /></OperatorRoute>} />
              <Route path="/inbox" element={<InboxPage />} />
              <Route path="/contacts" element={<ContactsPage />} />
              <Route path="/operators" element={<OwnerRoute><OperatorsPage /></OwnerRoute>} />
              <Route path="/integrations/whatsapp" element={<OwnerRoute><WhatsAppConfigPage /></OwnerRoute>} />
              <Route path="/bot-config" element={<OwnerRoute><BotConfigPage /></OwnerRoute>} />
              <Route path="/integrations/ai" element={<OwnerRoute><AiConfigPage /></OwnerRoute>} />
              <Route path="/knowledge" element={<OwnerRoute><KnowledgePage /></OwnerRoute>} />
              <Route path="/tags" element={<OwnerRoute><ClientTagsPage /></OwnerRoute>} />
              <Route path="/usage" element={<OperatorRoute><UsagePage /></OperatorRoute>} />
              <Route path="/admin/tenants" element={<AdminRoute><AdminTenantsPage /></AdminRoute>} />
              <Route path="/admin/webhooks" element={<AdminRoute><WebhookEventsPage /></AdminRoute>} />
            </Route>
            <Route path="/" element={<NavigateToHome />} />
            <Route path="*" element={<NavigateToHome />} />
          </Routes>
        </AuthProvider>
      </BrowserRouter>
    </QueryClientProvider>
  )
}

export default App

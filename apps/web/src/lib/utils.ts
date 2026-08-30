import { clsx, type ClassValue } from 'clsx'
import { twMerge } from 'tailwind-merge'

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

export function formatTime(date: Date | string): string {
  const d = new Date(date)
  return d.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })
}

export function formatDate(date: Date | string): string {
  const d = new Date(date)
  const now = new Date()
  const diff = now.getTime() - d.getTime()
  const days = Math.floor(diff / (1000 * 60 * 60 * 24))

  if (days === 0) return 'Hoje'
  if (days === 1) return 'Ontem'
  if (days < 7) return d.toLocaleDateString('pt-BR', { weekday: 'long' })
  return d.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' })
}

export function truncate(str: string, length: number): string {
  if (str.length <= length) return str
  return str.slice(0, length) + '...'
}

export function formatConversationMode(mode: string): string {
  const labels: Record<string, string> = {
    Automatic: 'Automático',
    Human: 'Humano',
    Paused: 'Pausado',
  }
  return labels[mode] ?? mode
}

export function formatUserRole(role?: string): string {
  const labels: Record<string, string> = {
    PlatformAdmin: 'Administrador da plataforma',
    TenantOwner: 'Administrador da empresa',
    Operator: 'Operador',
  }
  return (role && labels[role]) ?? 'Usuário'
}

export function formatAiDecision(decision: string): string {
  const labels: Record<string, string> = {
    Reply: 'Responder',
    Handoff: 'Encaminhar para atendimento humano',
    NoAction: 'Sem ação',
  }
  return labels[decision] ?? decision
}

export function formatAiReason(reason: string): string {
  const labels: Record<string, string> = {
    low_confidence: 'Confiança abaixo do limiar',
    sensitive_topic: 'Assunto sensível',
    out_of_scope: 'Fora do escopo',
    customer_request: 'Solicitação do cliente',
    escalation_needed: 'Necessidade de escalonamento',
    complaint: 'Reclamação',
    refund_request: 'Solicitação de reembolso',
    legal_issue: 'Questão jurídica',
    unsafe_content: 'Conteúdo inseguro',
    queue_selection: 'Encaminhamento para fila',
    invalid_response: 'Resposta inválida do provedor',
    ai_unavailable: 'Provedor de IA indisponível',
    ai_retry_exhausted: 'Tentativas do provedor esgotadas',
  }
  return labels[reason] ?? reason.replaceAll('_', ' ')
}

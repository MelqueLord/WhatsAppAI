import { describe, expect, it } from 'vitest'
import { formatAiDecision, formatAiReason, formatConversationMode, formatUserRole } from './utils'

describe('presentation labels', () => {
  it('localizes conversation modes and user roles', () => {
    expect(formatConversationMode('Human')).toBe('Humano')
    expect(formatConversationMode('Automatic')).toBe('Automático')
    expect(formatUserRole('TenantOwner')).toBe('Administrador da empresa')
    expect(formatUserRole('PlatformAdmin')).toBe('Administrador da plataforma')
  })

  it('localizes AI decisions and reasons', () => {
    expect(formatAiDecision('Handoff')).toBe('Encaminhar para atendimento humano')
    expect(formatAiDecision('NoAction')).toBe('Sem ação')
    expect(formatAiReason('low_confidence')).toBe('Confiança abaixo do limiar')
    expect(formatAiReason('queue_selection')).toBe('Encaminhamento para fila')
  })
})

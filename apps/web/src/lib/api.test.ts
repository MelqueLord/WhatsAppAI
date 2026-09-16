import { beforeEach, describe, expect, it, vi } from 'vitest'
import { api } from './api'

describe('api error handling', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn())
  })

  it('extracts the message from JSON error responses', async () => {
    vi.mocked(fetch).mockResolvedValue({
      ok: false,
      status: 400,
      text: () => Promise.resolve(JSON.stringify({ error: 'Mensagem inválida.' })),
    } as Response)

    await expect(api.dashboard.getStats()).rejects.toThrow('Mensagem inválida.')
  })

  it('uses a friendly message for unavailable services without exposing technical details', async () => {
    vi.mocked(fetch).mockResolvedValue({
      ok: false,
      status: 500,
      text: () => Promise.resolve(JSON.stringify({ detail: 'NpgsqlException: connection refused' })),
    } as Response)

    await expect(api.dashboard.getStats()).rejects.toThrow('Não foi possível concluir agora. Tente novamente em alguns instantes.')
  })

  it('uses a friendly message when the network is unavailable', async () => {
    vi.mocked(fetch).mockRejectedValue(new TypeError('Failed to fetch'))

    await expect(api.dashboard.getStats()).rejects.toThrow('Não foi possível conectar ao sistema. Verifique sua internet e tente novamente.')
  })
})

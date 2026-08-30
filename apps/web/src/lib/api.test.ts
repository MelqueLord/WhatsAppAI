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
})

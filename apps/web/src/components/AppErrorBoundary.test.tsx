import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { AppErrorBoundary } from './AppErrorBoundary'

function BrokenScreen(): never {
  throw new Error('render failed')
}

describe('AppErrorBoundary', () => {
  it('shows a friendly recovery screen when a page cannot render', () => {
    vi.spyOn(console, 'error').mockImplementation(() => undefined)

    render(
      <AppErrorBoundary>
        <BrokenScreen />
      </AppErrorBoundary>,
    )

    expect(screen.getByRole('heading', { name: 'Não foi possível abrir esta tela' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Atualizar página' })).toBeInTheDocument()
  })
})

import { act, render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { ApiError } from '../lib/errors'
import { ErrorNotifications, notifyError } from './ErrorNotifications'

describe('ErrorNotifications', () => {
  it('presents a friendly message for global errors', () => {
    render(<ErrorNotifications><div>Conteúdo</div></ErrorNotifications>)

    act(() => {
      notifyError(new ApiError('Muitas tentativas em pouco tempo. Aguarde alguns instantes e tente novamente.', 429, 'HTTP_429'))
    })

    expect(screen.getByRole('alert')).toHaveTextContent('Muitas tentativas em pouco tempo')
  })
})

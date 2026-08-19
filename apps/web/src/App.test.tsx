import { render, waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import App from './App'

describe('App', () => {
  it('renders the login page when not authenticated', async () => {
    render(<App />)

    await waitFor(() => {
      expect(window.location.pathname).toBe('/login')
    })
  })
})

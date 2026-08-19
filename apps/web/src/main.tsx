import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'

async function bootstrap() {
  if (import.meta.env.DEV && import.meta.env.VITE_USE_MOCK_API === 'true') {
    const { setupMockApi } = await import('./lib/mockApi')
    setupMockApi()
  }

  createRoot(document.getElementById('root')!).render(
    <StrictMode>
      <App />
    </StrictMode>,
  )
}

void bootstrap()

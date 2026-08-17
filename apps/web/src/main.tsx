import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'

// Mock API disabled - using real API at localhost:5000
// if (import.meta.env.DEV) {
//   import('./lib/mockApi').then(({ setupMockApi }) => setupMockApi())
// }

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)

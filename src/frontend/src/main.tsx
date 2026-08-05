import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { registerSW } from 'virtual:pwa-register'
import { App } from './app/App'
import './styles/tokens.css'
import './styles/global.css'

const updateServiceWorker = registerSW({
  onNeedRefresh() {
    window.dispatchEvent(new CustomEvent('family-dashboard:update-ready', {
      detail: () => updateServiceWorker(true),
    }))
  },
})

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)

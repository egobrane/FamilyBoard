import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router'
import { App } from './app/App'
import { AuthenticationProvider } from './features/authentication/AuthenticationContext'
import { PwaUpdateProvider } from './features/pwa/PwaUpdateProvider'
import './styles/tokens.css'
import './styles/global.css'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter>
      <PwaUpdateProvider>
        <AuthenticationProvider>
          <App />
        </AuthenticationProvider>
      </PwaUpdateProvider>
    </BrowserRouter>
  </StrictMode>,
)

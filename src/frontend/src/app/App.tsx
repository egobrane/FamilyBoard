import { useEffect, useState } from 'react'
import { NavigationBar } from '../components/NavigationBar'
import { DashboardPage } from '../features/dashboard/DashboardPage'
import { demoHousehold } from '../features/dashboard/mockDashboardData'
import { configuration } from '../lib/configuration'

function formattedTime(date: Date) {
  return new Intl.DateTimeFormat(undefined, { hour: 'numeric', minute: '2-digit' }).format(date)
}

export function App() {
  const [now, setNow] = useState(() => new Date())
  const [applyUpdate, setApplyUpdate] = useState<(() => Promise<void>) | null>(null)

  useEffect(() => {
    const timer = window.setInterval(() => setNow(new Date()), 30_000)
    const showUpdate = (event: Event) => {
      const updateEvent = event as CustomEvent<() => Promise<void>>
      setApplyUpdate(() => updateEvent.detail)
    }
    window.addEventListener('family-dashboard:update-ready', showUpdate)

    return () => {
      window.clearInterval(timer)
      window.removeEventListener('family-dashboard:update-ready', showUpdate)
    }
  }, [])

  return (
    <div className="app-shell">
      <a className="skip-link" href="#main-content">Skip to dashboard</a>
      <header className="topbar">
        <div className="brand" aria-label={configuration.appName}>
          <span className="brand__mark" aria-hidden="true"><i /><i /><i /></span>
          <span className="brand__text">Family<span>Dashboard</span></span>
        </div>
        <h1 className="household-name">{demoHousehold.name}</h1>
        <div className="topbar__right">
          <time className="current-time" dateTime={now.toISOString()}>{formattedTime(now)}</time>
          <span className="avatar" aria-label="Household profile">RB</span>
        </div>
      </header>

      {applyUpdate && (
        <div className="update-banner" role="status">
          A fresh version is ready. Reload when convenient.
          <button type="button" onClick={() => void applyUpdate()}>Reload</button>
        </div>
      )}

      <DashboardPage />
      <NavigationBar />
    </div>
  )
}

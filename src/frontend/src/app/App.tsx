import { useEffect, useState } from 'react'
import { Navigate, Outlet, Route, Routes } from 'react-router'
import { AccountMenu } from '../components/AccountMenu'
import { NavigationBar } from '../components/NavigationBar'
import {
  useAuthentication,
  type AuthenticationState,
} from '../features/authentication/AuthenticationContext'
import { AuthenticationErrorPage } from '../features/authentication/AuthenticationErrorPage'
import { WelcomePage } from '../features/authentication/WelcomePage'
import { DashboardPage } from '../features/dashboard/DashboardPage'
import { HouseholdAdminLayout } from '../features/household-admin/HouseholdAdminLayout'
import { HouseholdMembersPage } from '../features/household-admin/HouseholdMembersPage'
import { HouseholdSettingsPage } from '../features/household-admin/HouseholdSettingsPage'
import { HouseholdSelectionPage } from '../features/households/HouseholdSelectionPage'
import { HouseholdSetupPage } from '../features/households/HouseholdSetupPage'
import { configuration } from '../lib/configuration'

function formattedTime(date: Date) {
  return new Intl.DateTimeFormat(undefined, { hour: 'numeric', minute: '2-digit' }).format(date)
}

function Brand() {
  return (
    <div aria-label={configuration.appName} className="brand">
      <span aria-hidden="true" className="brand__mark"><i /><i /><i /></span>
      <span className="brand__text">Family<span>Dashboard</span></span>
    </div>
  )
}

function StatusPage({ state, onRetry }: { state: AuthenticationState; onRetry: () => Promise<void> }) {
  const accountUnavailable = state.status === 'accountUnavailable'
  return (
    <main className="entry-page" id="main-content">
      <div className="entry-card" role={accountUnavailable ? 'alert' : 'status'}>
        <Brand />
        <p className="eyebrow">{accountUnavailable ? 'Account unavailable' : 'One moment'}</p>
        <h1>{accountUnavailable ? 'This account cannot open Family Dashboard.' : 'Family Dashboard is getting ready.'}</h1>
        <p className="entry-card__lede">
          {accountUnavailable
            ? 'Ask a household administrator to confirm that your account is active.'
            : state.status === 'unavailable'
              ? state.message
              : 'Checking your secure family session…'}
        </p>
        {state.status === 'unavailable' && (
          <button className="primary-action" onClick={() => void onRetry()} type="button">Try again</button>
        )}
      </div>
    </main>
  )
}

function HouseholdShell() {
  const { state, isMutating, logout } = useAuthentication()
  const [now, setNow] = useState(() => new Date())

  useEffect(() => {
    const timer = window.setInterval(() => setNow(new Date()), 30_000)
    return () => window.clearInterval(timer)
  }, [])

  if (state.status !== 'authenticated') {
    return null
  }

  const household = state.currentUser.households.find(
    (candidate) => candidate.id === state.currentUser.selectedHouseholdId,
  )
  if (!household) {
    return <Navigate replace to="/households/select" />
  }

  return (
    <div className="app-shell">
      <a className="skip-link" href="#main-content">Skip to content</a>
      <header className="topbar">
        <Brand />
        <h1 className="household-name">{household.name}</h1>
        <div className="topbar__right">
          <time className="current-time" dateTime={now.toISOString()}>{formattedTime(now)}</time>
          <AccountMenu
            canSwitchHouseholds={state.currentUser.households.length > 1}
            displayName={state.currentUser.user.displayName}
            householdSettingsPath={household.role === 'adult'
              ? `/households/${household.id}/settings`
              : undefined}
            isBusy={isMutating}
            onLogout={logout}
          />
        </div>
      </header>
      <Outlet />
    </div>
  )
}

function DashboardHome() {
  return (
    <>
      <DashboardPage />
      <NavigationBar />
    </>
  )
}

function AuthenticatedRoutes() {
  const { state } = useAuthentication()
  if (state.status !== 'authenticated') {
    return null
  }

  if (state.currentUser.households.length === 0) {
    return (
      <Routes>
        <Route element={<HouseholdSetupPage />} path="/setup/household" />
        <Route element={<Navigate replace to="/setup/household" />} path="*" />
      </Routes>
    )
  }

  if (state.currentUser.selectedHouseholdId === null) {
    return (
      <Routes>
        <Route element={<HouseholdSelectionPage />} path="/households/select" />
        <Route element={<Navigate replace to="/households/select" />} path="*" />
      </Routes>
    )
  }

  return (
    <Routes>
      <Route element={<HouseholdShell />}>
        <Route element={<DashboardHome />} path="/" />
        <Route element={<HouseholdAdminLayout />} path="/households/:householdId">
          <Route element={<HouseholdSettingsPage />} path="settings" />
          <Route element={<HouseholdMembersPage />} path="members" />
          <Route element={<Navigate replace to="settings" />} index />
        </Route>
      </Route>
      <Route element={<HouseholdSelectionPage />} path="/households/select" />
      <Route element={<Navigate replace to="/" />} path="*" />
    </Routes>
  )
}

export function App() {
  const { state, refresh } = useAuthentication()
  const [applyUpdate, setApplyUpdate] = useState<(() => Promise<void>) | null>(null)

  useEffect(() => {
    const showUpdate = (event: Event) => {
      const updateEvent = event as CustomEvent<() => Promise<void>>
      setApplyUpdate(() => updateEvent.detail)
    }
    window.addEventListener('family-dashboard:update-ready', showUpdate)
    return () => window.removeEventListener('family-dashboard:update-ready', showUpdate)
  }, [])

  return (
    <>
      {applyUpdate && (
        <div className="update-banner" role="status">
          A fresh version is ready. Reload when convenient.
          <button type="button" onClick={() => void applyUpdate()}>Reload</button>
        </div>
      )}
      {state.status === 'loading' && <StatusPage onRetry={refresh} state={state} />}
      {state.status === 'unavailable' && <StatusPage onRetry={refresh} state={state} />}
      {state.status === 'accountUnavailable' && <StatusPage onRetry={refresh} state={state} />}
      {state.status === 'signedOut' && (
        <Routes>
          <Route element={<AuthenticationErrorPage />} path="/auth/error" />
          <Route element={<WelcomePage />} path="*" />
        </Routes>
      )}
      {state.status === 'authenticated' && <AuthenticatedRoutes />}
    </>
  )
}

import { useEffect, useState } from 'react'
import { Navigate, Outlet, Route, Routes, useLocation } from 'react-router'
import { AccountMenu } from '../components/AccountMenu'
import { NavigationBar } from '../components/NavigationBar'
import {
  useAuthentication,
  type AuthenticationState,
} from '../features/authentication/AuthenticationContext'
import { AuthenticationErrorPage } from '../features/authentication/AuthenticationErrorPage'
import { WelcomePage } from '../features/authentication/WelcomePage'
import { DashboardPage } from '../features/dashboard/DashboardPage'
import { CalendarPage } from '../features/calendar/CalendarPage'
import { HouseholdCalendarsPage } from '../features/calendar/HouseholdCalendarsPage'
import { HouseholdAdminLayout } from '../features/household-admin/HouseholdAdminLayout'
import { HouseholdMembersPage } from '../features/household-admin/HouseholdMembersPage'
import { HouseholdSettingsPage } from '../features/household-admin/HouseholdSettingsPage'
import { HouseholdInvitationsPage } from '../features/invitations/HouseholdInvitationsPage'
import { ParentAccessPage } from '../features/parent-access/ParentAccessPage'
import { InvitationLandingPage } from '../features/invitations/InvitationLandingPage'
import { HouseholdSelectionPage } from '../features/households/HouseholdSelectionPage'
import { HouseholdSetupPage } from '../features/households/HouseholdSetupPage'
import { configuration } from '../lib/configuration'
import { lockParentAccess } from '../lib/api'

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
  const { state, isMutating, logout, refreshSession } = useAuthentication()
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
  const session = state.currentUser.session
  const isParentElevated = session?.administrativeElevationHouseholdId === household.id
    && session.administrativeElevationExpiresAt !== null
    && new Date(session.administrativeElevationExpiresAt).getTime() > now.getTime()

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
            parentAccessPath={household.role === 'adult'
              ? `/households/${household.id}/parent-access`
              : undefined}
            isParentElevated={isParentElevated}
            isSharedDisplay={session?.isSharedDisplay === true}
            isBusy={isMutating}
            onLockParentAccess={async () => {
              await lockParentAccess(household.id)
              await refreshSession()
            }}
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
        <Route element={<><CalendarPage /><NavigationBar /></>} path="/calendar" />
        <Route element={<HouseholdAdminLayout />} path="/households/:householdId">
          <Route element={<HouseholdSettingsPage />} path="settings" />
          <Route element={<HouseholdMembersPage />} path="members" />
          <Route element={<HouseholdInvitationsPage />} path="invitations" />
          <Route element={<ParentAccessPage />} path="parent-access" />
          <Route element={<HouseholdCalendarsPage />} path="calendars" />
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
  const location = useLocation()
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
      {location.pathname === '/invite' && <InvitationLandingPage />}
      {location.pathname !== '/invite' && state.status === 'loading' && <StatusPage onRetry={refresh} state={state} />}
      {location.pathname !== '/invite' && state.status === 'unavailable' && <StatusPage onRetry={refresh} state={state} />}
      {location.pathname !== '/invite' && state.status === 'accountUnavailable' && <StatusPage onRetry={refresh} state={state} />}
      {location.pathname !== '/invite' && state.status === 'signedOut' && (
        <Routes>
          <Route element={<AuthenticationErrorPage />} path="/auth/error" />
          <Route element={<WelcomePage />} path="*" />
        </Routes>
      )}
      {location.pathname !== '/invite' && state.status === 'authenticated' && <AuthenticatedRoutes />}
    </>
  )
}

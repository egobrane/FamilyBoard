import { useEffect, useState } from 'react'
import { Link, NavLink, Outlet, useParams } from 'react-router'
import { useAuthentication } from '../authentication/AuthenticationContext'
import { ParentAccessGate } from '../parent-access/ParentAccessGate'

export function HouseholdAdminLayout() {
  const { householdId } = useParams()
  const { state, selectHousehold } = useAuthentication()
  const [selectionFailed, setSelectionFailed] = useState(false)
  const household = state.status === 'authenticated'
    ? state.currentUser.households.find((candidate) => candidate.id === householdId)
    : undefined
  const mustSelectHousehold = household !== undefined
    && state.status === 'authenticated'
    && state.currentUser.selectedHouseholdId !== household.id

  useEffect(() => {
    if (!mustSelectHousehold || household === undefined) return
    let active = true
    void selectHousehold(household.id).catch(() => {
      if (active) setSelectionFailed(true)
    })
    return () => { active = false }
  }, [household, mustSelectHousehold, selectHousehold])

  if (state.status !== 'authenticated') {
    return null
  }

  if (household === undefined) {
    return (
      <main className="admin-page" id="main-content">
        <section className="admin-status" role="alert">
          <p className="eyebrow">Household unavailable</p>
          <h2>That household could not be opened.</h2>
          <p>Choose one of the households available to this account.</p>
          <Link className="primary-action" to="/households/select">Choose household</Link>
        </section>
      </main>
    )
  }

  if (household.role !== 'adult') {
    return (
      <main className="admin-page" id="main-content">
        <section className="admin-status" role="alert">
          <p className="eyebrow">Adult access required</p>
          <h2>Household administration is not available.</h2>
          <p>An adult household account must make these changes.</p>
          <Link className="secondary-action" to="/">Return to dashboard</Link>
        </section>
      </main>
    )
  }

  if (mustSelectHousehold) {
    return (
      <main className="admin-page" id="main-content">
        <section className="admin-status" role={selectionFailed ? 'alert' : 'status'}>
          <p className="eyebrow">Opening household</p>
          <h2>{selectionFailed ? 'That household could not be selected.' : `Opening ${household.name}…`}</h2>
          {selectionFailed && <Link className="secondary-action" to="/households/select">Choose household</Link>}
        </section>
      </main>
    )
  }

  const basePath = `/households/${household.id}`
  return (
    <main className="admin-page" id="main-content">
      <div className="admin-shell">
        <header className="admin-header">
          <div>
            <Link className="back-link" to="/">← Dashboard</Link>
            <p className="eyebrow">Household administration</p>
            <h2>{household.name}</h2>
          </div>
          <nav aria-label="Household administration" className="admin-tabs">
            <NavLink to={`${basePath}/settings`}>Settings</NavLink>
            <NavLink to={`${basePath}/members`}>Members</NavLink>
            <NavLink to={`${basePath}/invitations`}>Invitations</NavLink>
            <NavLink to={`${basePath}/calendars`}>Calendars</NavLink>
            <NavLink to={`${basePath}/chores`}>Chores</NavLink>
            <NavLink to={`${basePath}/points`}>Points</NavLink>
            <NavLink to={`${basePath}/rewards`}>Rewards</NavLink>
            <NavLink to={`${basePath}/parent-access`}>Parent access</NavLink>
          </nav>
        </header>
        <ParentAccessGate householdId={household.id}><Outlet /></ParentAccessGate>
      </div>
    </main>
  )
}

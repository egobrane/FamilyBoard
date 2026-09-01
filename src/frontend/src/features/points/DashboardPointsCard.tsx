import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import { DashboardCard } from '../../components/DashboardCard'
import { useAuthentication } from '../authentication/AuthenticationContext'
import { ApiError, getPointSummary, type HouseholdPointSummaryResponse } from '../../lib/api'
import { MemberAvatar } from '../../components/MemberAvatar'

export function DashboardPointsCard() {
  const { state } = useAuthentication()
  const [summary, setSummary] = useState<HouseholdPointSummaryResponse | null>(null)
  const [error, setError] = useState('')
  const householdId = state.status === 'authenticated' ? state.currentUser.selectedHouseholdId : null
  useEffect(() => {
    if (!householdId) return
    let active = true
    void getPointSummary(householdId).then((result) => { if (active) setSummary(result) })
      .catch((reason) => { if (active) setError(reason instanceof ApiError ? reason.problem.title : 'Points are unavailable.') })
    return () => { active = false }
  }, [householdId])

  return <DashboardCard className="family-card" eyebrow="Keep it going" id="points-preview" title="Family points">
    {!summary && !error && <p role="status">Loading family points…</p>}
    {error && <p role="alert">{error}</p>}
    {summary && <>
      <p className="household-point-total"><strong>{summary.householdBalance}</strong> household points</p>
      <div className="member-grid">{summary.members.filter((member) => member.isActive).slice(0, 4).map((member) => (
        <div className="member" key={member.memberId}>
          <MemberAvatar className="member__avatar" member={member} />
          <span><strong>{member.displayName}</strong><small>{member.balance} points</small></span>
        </div>
      ))}</div>
      {summary.members.length === 0 && <p>No household members have point balances yet.</p>}
      <Link className="dashboard-card__link" to="/points">View point history →</Link>
    </>}
  </DashboardCard>
}

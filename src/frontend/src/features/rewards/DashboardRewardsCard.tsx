import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import { DashboardCard } from '../../components/DashboardCard'
import { ApiError, getRewardCatalog, type RewardCatalogResponse } from '../../lib/api'
import { useAuthentication } from '../authentication/AuthenticationContext'
import { MemberAvatar } from '../../components/MemberAvatar'

export function DashboardRewardsCard() {
  const { state } = useAuthentication(); const [catalog, setCatalog] = useState<RewardCatalogResponse | null>(null); const [error, setError] = useState('')
  const householdId = state.status === 'authenticated' ? state.currentUser.selectedHouseholdId : null
  useEffect(() => { if (!householdId) return; let active = true
    void getRewardCatalog(householdId).then((value) => { if (active) setCatalog(value) })
      .catch((cause) => { if (active) setError(cause instanceof ApiError ? cause.problem.title : 'Rewards are unavailable.') })
    return () => { active = false } }, [householdId])
  return <DashboardCard className="family-card" eyebrow="Something to look forward to" id="rewards-preview" title="Rewards">
    {!catalog && !error && <p role="status">Loading rewards…</p>}{error && <p role="alert">{error}</p>}
    {catalog && <><p><strong>{catalog.rewards.length}</strong> rewards available</p>
      <div className="member-grid">{catalog.members.slice(0, 4).map((member) => <div className="member" key={member.memberId}>
        <MemberAvatar className="member__avatar" member={member} />
        <span><strong>{member.displayName}</strong><small>{member.balance} points</small></span></div>)}</div>
      <Link className="dashboard-card__link" to="/rewards">Browse rewards →</Link></>}
  </DashboardCard>
}

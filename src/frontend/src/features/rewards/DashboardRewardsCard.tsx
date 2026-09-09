import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import { DashboardCard } from '../../components/DashboardCard'
import { ApiError, getRewardCatalog, type PointMemberBalanceResponse, type RewardCatalogResponse } from '../../lib/api'
import { useAuthentication } from '../authentication/AuthenticationContext'
import { MemberAvatar } from '../../components/MemberAvatar'

export function RewardMemberStandings({ members }: { members: PointMemberBalanceResponse[] }) {
  const standings = [...members]
    .filter((member) => member.isActive)
    .sort((left, right) => right.balance - left.balance
      || left.displayName.localeCompare(right.displayName, undefined, { sensitivity: 'base' })
      || left.memberId.localeCompare(right.memberId))
  const leaderBalance = Math.max(0, ...standings.map((member) => member.balance))

  return <ol aria-label="Household point standings" className="member-grid member-grid--standings">
    {standings.map((member) => {
      const percentage = leaderBalance > 0 ? Math.max(0, Math.round((member.balance / leaderBalance) * 100)) : 0
      return <li className="member member--standing" key={member.memberId}>
      <MemberAvatar className="member__avatar" member={member} />
      <span className="member-standing__details"><span><strong>{member.displayName}</strong><small>{member.balance} points</small></span>
        <span aria-label={`${member.displayName}: ${member.balance} points, ${percentage}% of the household leader`}
          aria-valuemax={100} aria-valuemin={0} aria-valuenow={percentage} className="member-standing__track" role="progressbar">
          <span className="member-standing__fill" style={{ width: `${percentage}%` }} />
        </span>
      </span>
    </li>})}
  </ol>
}

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
      <RewardMemberStandings members={catalog.members} />
      <Link className="dashboard-card__link" to="/rewards">Browse rewards →</Link></>}
  </DashboardCard>
}

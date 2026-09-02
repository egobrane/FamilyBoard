import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router'
import { DashboardCard } from '../../components/DashboardCard'
import { useAuthentication } from '../authentication/AuthenticationContext'
import { getChoreDashboard, listChoreParticipants,
  type ChoreAssignmentResponse, type ChoreParticipantResponse } from '../../lib/api'
import { MemberAvatar } from '../../components/MemberAvatar'
import { CompleteChoreDialog } from './CompleteChoreDialog'

export function DashboardChoresCard() {
  const { state } = useAuthentication()
  const [items, setItems] = useState<ChoreAssignmentResponse[]>([])
  const [participants, setParticipants] = useState<ChoreParticipantResponse[]>([])
  const [selected, setSelected] = useState<ChoreAssignmentResponse | null>(null)
  const [waiting, setWaiting] = useState(0)
  const household = state.status === 'authenticated'
    ? state.currentUser.households.find((item) => item.id === state.currentUser.selectedHouseholdId) : undefined
  const householdId = household?.id ?? null
  const load = useCallback(async () => {
    if (!householdId) return
    try {
      const [response, people] = await Promise.all([
        getChoreDashboard(householdId), listChoreParticipants(householdId).catch(() => []),
      ])
      const dashboardItems = [...response.overdue, ...response.dueToday, ...response.upcoming].slice(0, 3)
      setItems(dashboardItems)
      setParticipants(people.length > 0 ? people : [...new Map(dashboardItems.map((item) =>
        [item.assignedMember.id, item.assignedMember])).values()])
      setWaiting(response.awaitingReviewCount)
    } catch { setItems([]); setParticipants([]) }
  }, [householdId])
  useEffect(() => {
    const handle = window.setTimeout(() => void load(), 0)
    return () => window.clearTimeout(handle)
  }, [load])

  return (
    <DashboardCard className="chores-card" eyebrow="A little progress" id="chores-preview" title="Chores"
      action={<Link className="card-link" to="/chores">View all</Link>}>
      {items.length === 0 ? <p className="preview-note">No chores are due right now.</p> : (
        <ul className="chore-list">
          {items.map((chore) => <li className="chore" key={chore.id}>
            {chore.status === 'pending' ? <button aria-label={`Mark ${chore.title} done`}
              className="chore__check chore__check--button" onClick={() => setSelected(chore)} type="button" />
              : <span aria-hidden="true" className="chore__check chore__check--pending">…</span>}
            <MemberAvatar className="chore__member-avatar" member={chore.assignedMember} />
            <span className="chore__details"><strong>{chore.title}</strong><span>{chore.assignedMember.displayName}</span></span>
            {chore.status === 'awaitingReview' ? <span className="status-pill status-pill--mint">Waiting for review</span>
              : chore.isOverdue && <span className="status-pill status-pill--coral">Overdue</span>}
          </li>)}
        </ul>
      )}
      {waiting > 0 && <p className="preview-note">{waiting} waiting for adult review.</p>}
      {selected && household && <CompleteChoreDialog assignment={selected} householdId={household.id}
        defaultMemberId={selected.assignedMember.id}
        participants={participants} onClose={() => setSelected(null)}
        onCompleted={() => { setSelected(null); void load() }} />}
    </DashboardCard>
  )
}

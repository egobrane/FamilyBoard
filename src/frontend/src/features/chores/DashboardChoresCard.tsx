import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router'
import { DashboardCard } from '../../components/DashboardCard'
import { useAuthentication } from '../authentication/AuthenticationContext'
import { getChoreDashboard, listChoreParticipants,
  type ChoreAssignmentResponse, type ChoreParticipantResponse } from '../../lib/api'
import { MemberAvatar } from '../../components/MemberAvatar'
import { CompleteChoreDialog } from './CompleteChoreDialog'
import { ClaimChoreDialog } from './ClaimChoreDialog'

export function DashboardChoresCard() {
  const { state } = useAuthentication()
  const [assignedItems, setAssignedItems] = useState<ChoreAssignmentResponse[]>([])
  const [openItems, setOpenItems] = useState<ChoreAssignmentResponse[]>([])
  const [participants, setParticipants] = useState<ChoreParticipantResponse[]>([])
  const [selected, setSelected] = useState<ChoreAssignmentResponse | null>(null)
  const [selectedClaim, setSelectedClaim] = useState<ChoreAssignmentResponse | null>(null)
  const [announcement, setAnnouncement] = useState('')
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
      const dashboardItems = [...response.overdue, ...response.dueToday, ...response.upcoming]
      setAssignedItems(dashboardItems.slice(0, 3))
      setOpenItems((response.open ?? []).slice(0, 3))
      setParticipants(people.length > 0 ? people : [...new Map(dashboardItems.map((item) =>
        item.assignedMember ? [item.assignedMember.id, item.assignedMember] : null)
        .filter((item): item is [string, ChoreParticipantResponse] => item !== null)).values()])
      setWaiting(response.awaitingReviewCount)
    } catch { setAssignedItems([]); setOpenItems([]); setParticipants([]) }
  }, [householdId])
  useEffect(() => {
    const handle = window.setTimeout(() => void load(), 0)
    return () => window.clearTimeout(handle)
  }, [load])

  return (
    <DashboardCard className="chores-card" eyebrow="A little progress" id="chores-preview" title="Chores"
      action={<Link className="card-link" to="/chores">View all</Link>}>
      <p aria-live="polite" className="visually-hidden">{announcement}</p>
      <div className="chore-lanes">
        <section aria-labelledby="assigned-chores-heading" className="chore-lane">
          <h3 id="assigned-chores-heading">Assigned</h3>
          {assignedItems.length === 0 ? <p className="preview-note">No assigned chores are due right now.</p> : <ul className="chore-list">
          {assignedItems.map((chore) => <li className="chore" key={chore.id}>
            {chore.status === 'pending' ? <button aria-label={`Mark ${chore.title} done`}
              className="chore__check chore__check--button" onClick={() => setSelected(chore)} type="button" />
              : <span aria-hidden="true" className="chore__check chore__check--pending">…</span>}
            {chore.assignedMember && <MemberAvatar className="chore__member-avatar" member={chore.assignedMember} />}
            <span className="chore__details"><strong>{chore.title}</strong><span>{chore.assignedMember?.displayName}</span></span>
            {chore.status === 'awaitingReview' ? <span className="status-pill status-pill--mint">Waiting for review</span>
              : chore.isOverdue && <span className="status-pill status-pill--coral">Overdue</span>}
          </li>)}
        </ul>}
        </section>
        <section aria-labelledby="open-chores-heading" className="chore-lane chore-lane--open">
          <h3 id="open-chores-heading">Up for grabs</h3>
          {openItems.length === 0 ? <p className="preview-note">No open chores are waiting.</p> : <ul className="chore-list">
            {openItems.map((chore) => <li className="chore chore--open" key={chore.id}>
              <span aria-hidden="true" className="chore__open-mark">✦</span>
              <span className="chore__details"><strong>{chore.title}</strong><span>{chore.isOverdue ? 'Overdue' : chore.dueLocalTime ? `Due ${chore.dueLocalTime.slice(0, 5)}` : 'Ready to claim'}{chore.pointValue > 0 ? ` · ${chore.pointValue} points` : ''}</span></span>
              <button className="claim-action" onClick={() => setSelectedClaim(chore)} type="button">I’ll do it</button>
            </li>)}
          </ul>}
        </section>
      </div>
      {waiting > 0 && <p className="preview-note">{waiting} waiting for adult review.</p>}
      {selected && household && selected.assignedMember && <CompleteChoreDialog assignment={selected} householdId={household.id}
        defaultMemberId={selected.assignedMember.id}
        participants={participants} onClose={() => setSelected(null)}
        onCompleted={() => { setSelected(null); void load() }} />}
      {selectedClaim && household && <ClaimChoreDialog assignment={selectedClaim} householdId={household.id}
        defaultMemberId={state.status === 'authenticated' && !state.currentUser.session?.isSharedDisplay ? household.memberId : ''}
        participants={participants} onClose={() => setSelectedClaim(null)}
        onClaimed={(memberName) => { setAnnouncement(`${selectedClaim.title} assigned to ${memberName}.`); setSelectedClaim(null); void load() }} />}
    </DashboardCard>
  )
}

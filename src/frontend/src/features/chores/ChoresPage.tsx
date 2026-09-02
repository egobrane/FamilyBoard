import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router'
import { useAuthentication } from '../authentication/AuthenticationContext'
import { ApiError, listChoreAssignments, listChoreParticipants, type ChoreAssignmentResponse, type ChoreParticipantResponse } from '../../lib/api'
import { ChoreList } from './ChoreList'
import { CompleteChoreDialog } from './CompleteChoreDialog'

export function ChoresPage() {
  const { state } = useAuthentication()
  const [assignments, setAssignments] = useState<ChoreAssignmentResponse[]>([])
  const [participants, setParticipants] = useState<ChoreParticipantResponse[]>([])
  const [selected, setSelected] = useState<ChoreAssignmentResponse | null>(null)
  const [view, setView] = useState<'active' | 'history'>('active')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const household = state.status === 'authenticated'
    ? state.currentUser.households.find((item) => item.id === state.currentUser.selectedHouseholdId) : undefined
  const load = useCallback(async () => {
    if (!household) return
    setLoading(true); setError('')
    try {
      const [list, people] = await Promise.all([
        listChoreAssignments(household.id, view), listChoreParticipants(household.id),
      ])
      setAssignments(list.items); setParticipants(people)
    } catch (reason) {
      setError(reason instanceof ApiError ? reason.problem.title : 'Chores could not be loaded.')
    } finally { setLoading(false) }
  }, [household, view])
  useEffect(() => {
    const handle = window.setTimeout(() => void load(), 0)
    return () => window.clearTimeout(handle)
  }, [load])
  if (!household) return null
  const isSharedDisplay = state.status === 'authenticated' && state.currentUser.session?.isSharedDisplay === true

  return (
    <main className="feature-page workspace-feature-page" id="main-content" tabIndex={-1}>
      <header className="feature-header">
        <div><p className="eyebrow">Today’s teamwork</p><h2>Chores</h2></div>
        {household.role === 'adult' && <Link className="secondary-action" to={`/households/${household.id}/chores`}>Manage chores</Link>}
      </header>
      <div aria-label="Chore list view" className="segmented-control" role="group">
        <button aria-pressed={view === 'active'} onClick={() => setView('active')} type="button">Active</button>
        <button aria-pressed={view === 'history'} onClick={() => setView('history')} type="button">History</button>
      </div>
      {loading && <p role="status">Loading household chores…</p>}
      {error && <div className="admin-status" role="alert"><p>{error}</p><button onClick={() => void load()} type="button">Try again</button></div>}
      {!loading && !error && assignments.length === 0 && <section className="empty-state"><h3>{view === 'active' ? 'All clear!' : 'No chore history yet'}</h3><p>{view === 'active' ? 'There are no active chores right now.' : 'Completed and skipped chores will appear here.'}</p></section>}
      {!loading && assignments.length > 0 && <ChoreList assignments={assignments} onComplete={view === 'active' ? setSelected : undefined} />}
      {selected && <CompleteChoreDialog assignment={selected} householdId={household.id}
        defaultMemberId={isSharedDisplay ? '' : household.memberId}
        participants={participants} onClose={() => setSelected(null)}
        onCompleted={() => { setSelected(null); void load() }} />}
    </main>
  )
}

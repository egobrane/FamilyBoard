import { useCallback, useEffect, useState } from 'react'
import { useParams } from 'react-router'
import { ApiError, listChoreAssignments, listChoreDefinitions, listChoreParticipants, listPendingChoreReviews,
  setChoreDefinitionActive, type ChoreAssignmentResponse, type ChoreCompletionResponse,
  skipChoreAssignment, type ChoreDefinitionResponse, type ChoreParticipantResponse } from '../../lib/api'
import { ChoreAssignmentForm } from './ChoreAssignmentForm'
import { ChoreDefinitionForm } from './ChoreDefinitionForm'
import { ChoreReviewQueue } from './ChoreReviewQueue'

export function HouseholdChoresPage() {
  const { householdId = '' } = useParams()
  const [definitions, setDefinitions] = useState<ChoreDefinitionResponse[]>([])
  const [participants, setParticipants] = useState<ChoreParticipantResponse[]>([])
  const [assignments, setAssignments] = useState<ChoreAssignmentResponse[]>([])
  const [reviews, setReviews] = useState<ChoreCompletionResponse[]>([])
  const [editing, setEditing] = useState<ChoreDefinitionResponse | null>(null)
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)
  const load = useCallback(async () => {
    setLoading(true); setError('')
    try {
      const [defs, people, active, pending] = await Promise.all([
        listChoreDefinitions(householdId), listChoreParticipants(householdId),
        listChoreAssignments(householdId), listPendingChoreReviews(householdId),
      ])
      setDefinitions(defs); setParticipants(people); setAssignments(active.items); setReviews(pending)
    } catch (reason) { setError(reason instanceof ApiError ? reason.problem.title : 'Chore administration could not be loaded.') }
    finally { setLoading(false) }
  }, [householdId])
  useEffect(() => {
    const handle = window.setTimeout(() => void load(), 0)
    return () => window.clearTimeout(handle)
  }, [load])
  async function changeDefinitionState(definition: ChoreDefinitionResponse) {
    setError('')
    try {
      await setChoreDefinitionActive(householdId, definition, !definition.isActive)
      await load()
    } catch (reason) {
      setError(reason instanceof ApiError ? reason.problem.title : 'The chore definition could not be updated.')
    }
  }
  async function skipAssignment(assignment: ChoreAssignmentResponse) {
    setError('')
    try {
      await skipChoreAssignment(householdId, assignment, null)
      await load()
    } catch (reason) {
      setError(reason instanceof ApiError ? reason.problem.title : 'The assignment could not be skipped.')
    }
  }
  if (loading) return <p role="status">Loading chore administration…</p>
  if (error) return <section className="admin-status" role="alert"><p>{error}</p><button onClick={() => void load()} type="button">Try again</button></section>

  return <div className="chore-admin-grid">
    <section className="admin-section"><h3>Chore definitions</h3>
      <ul className="definition-list">{definitions.map((definition) => <li key={definition.id}>
        <div><strong>{definition.title}</strong><span>{definition.isActive ? 'Active' : 'Inactive'}</span></div>
        <div className="form-actions"><button onClick={() => setEditing(definition)} type="button">Edit</button>
          <button onClick={() => void changeDefinitionState(definition)} type="button">
            {definition.isActive ? 'Deactivate' : 'Reactivate'}</button></div></li>)}</ul>
    </section>
    <ChoreDefinitionForm definition={editing ?? undefined} householdId={householdId} key={editing?.id ?? 'new'}
      onCancel={editing ? () => setEditing(null) : undefined} onSaved={() => { setEditing(null); void load() }} />
    <ChoreAssignmentForm definitions={definitions} householdId={householdId} participants={participants} onSaved={() => void load()} />
    <section className="admin-section"><h3>Active assignments</h3>
      {!assignments.some((assignment) => assignment.status === 'pending') ? <p>No assignments are waiting to be completed.</p> : <ul className="definition-list">
        {assignments.filter((assignment) => assignment.status === 'pending').map((assignment) => <li key={assignment.id}>
          <div><strong>{assignment.title}</strong><span>{assignment.assignedMember.displayName}</span></div>
          <button onClick={() => void skipAssignment(assignment)} type="button">Skip assignment</button>
        </li>)}
      </ul>}
    </section>
    <ChoreReviewQueue assignments={assignments} completions={reviews} householdId={householdId} onReviewed={() => void load()} />
  </div>
}

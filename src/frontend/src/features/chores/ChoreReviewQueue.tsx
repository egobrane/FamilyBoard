import { useState } from 'react'
import { ApiError, reviewChoreCompletion, type ChoreAssignmentResponse, type ChoreCompletionResponse } from '../../lib/api'

export function ChoreReviewQueue({ householdId, assignments, completions, onReviewed }: {
  householdId: string
  assignments: ChoreAssignmentResponse[]
  completions: ChoreCompletionResponse[]
  onReviewed: () => void
}) {
  const [error, setError] = useState('')
  const [busy, setBusy] = useState('')
  async function review(item: ChoreCompletionResponse, decision: 'approved' | 'rejected') {
    setBusy(item.id); setError('')
    try { await reviewChoreCompletion(householdId, item, decision, null); onReviewed() }
    catch (reason) { setError(reason instanceof ApiError ? reason.problem.title : 'The review could not be saved.') }
    finally { setBusy('') }
  }
  return <section className="admin-section"><h3>Waiting for review</h3>
    {completions.length === 0 ? <p>No chores are waiting for review.</p> : <ul className="review-list">{completions.map((item) => {
      const assignment = assignments.find((candidate) => candidate.id === item.assignmentId)
      return <li key={item.id}><div><strong>{assignment?.title ?? 'Chore completion'}</strong><span>{item.completedByMember.displayName}</span></div>
        <div className="form-actions"><button disabled={busy === item.id} onClick={() => void review(item, 'rejected')} type="button">Try again</button>
          <button className="primary-action" disabled={busy === item.id} onClick={() => void review(item, 'approved')} type="button">Approve</button></div></li>
    })}</ul>}
    {error && <p role="alert">{error}</p>}
  </section>
}

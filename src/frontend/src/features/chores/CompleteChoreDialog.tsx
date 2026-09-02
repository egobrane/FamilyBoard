import { useEffect, useRef, useState } from 'react'
import { ApiError, completeChore, type ChoreAssignmentResponse, type ChoreParticipantResponse } from '../../lib/api'
import { MemberPicker } from '../../components/MemberPicker'

export function CompleteChoreDialog({ assignment, householdId, defaultMemberId, participants, onClose, onCompleted }: {
  assignment: ChoreAssignmentResponse
  householdId: string
  defaultMemberId: string
  participants: ChoreParticipantResponse[]
  onClose: () => void
  onCompleted: () => void
}) {
  const dialog = useRef<HTMLDialogElement>(null)
  const requestId = useRef(crypto.randomUUID())
  const [memberId, setMemberId] = useState(defaultMemberId)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  useEffect(() => { dialog.current?.showModal() }, [])

  async function submit(event: React.FormEvent) {
    event.preventDefault()
    if (!memberId) { setError('Choose who completed this chore.'); return }
    setBusy(true); setError('')
    try {
      await completeChore(householdId, assignment.id, {
        clientRequestId: requestId.current,
        expectedAssignmentVersion: assignment.version,
        completedByMemberId: memberId,
      })
      requestId.current = crypto.randomUUID()
      onCompleted()
    } catch (reason) {
      setError(reason instanceof ApiError ? reason.problem.title : 'The chore could not be completed.')
    } finally { setBusy(false) }
  }

  return (
    <dialog aria-labelledby="complete-chore-title" className="action-dialog" onCancel={onClose} ref={dialog}>
      <form onSubmit={(event) => void submit(event)}>
        <p className="eyebrow">Nice work</p>
        <h2 id="complete-chore-title">Mark “{assignment.title}” done?</h2>
        <MemberPicker autoFocus legend="Who completed it?" members={participants} value={memberId} onChange={(nextMemberId) => {
            setMemberId(nextMemberId)
            requestId.current = crypto.randomUUID()
          }} />
        <p>This will wait for an adult review before it is final.</p>
        {error && <p role="alert">{error}</p>}
        <div className="form-actions">
          <button className="secondary-action" disabled={busy} onClick={onClose} type="button">Not yet</button>
          <button className="primary-action" disabled={busy} type="submit">{busy ? 'Saving…' : 'Mark done'}</button>
        </div>
      </form>
    </dialog>
  )
}

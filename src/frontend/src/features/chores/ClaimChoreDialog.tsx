import { useEffect, useRef, useState } from 'react'
import { ApiError, claimChore, type ChoreAssignmentResponse, type ChoreParticipantResponse } from '../../lib/api'
import { MemberPicker } from '../../components/MemberPicker'

export function ClaimChoreDialog({ assignment, householdId, defaultMemberId, participants, onClose, onClaimed }: {
  assignment: ChoreAssignmentResponse
  householdId: string
  defaultMemberId: string
  participants: ChoreParticipantResponse[]
  onClose: () => void
  onClaimed: (memberName: string) => void
}) {
  const dialog = useRef<HTMLDialogElement>(null)
  const requestId = useRef(crypto.randomUUID())
  const [memberId, setMemberId] = useState(defaultMemberId)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  useEffect(() => { dialog.current?.showModal() }, [])

  async function submit(event: React.FormEvent) {
    event.preventDefault()
    if (!memberId) { setError('Choose who is taking this chore.'); return }
    setBusy(true); setError('')
    try {
      await claimChore(householdId, assignment.id, {
        clientRequestId: requestId.current,
        expectedAssignmentVersion: assignment.version,
        householdMemberId: memberId,
      })
      onClaimed(participants.find((member) => member.id === memberId)?.displayName ?? 'A household member')
    } catch (reason) {
      setError(reason instanceof ApiError ? reason.problem.title : 'The chore could not be claimed.')
    } finally { setBusy(false) }
  }

  return <dialog aria-labelledby="claim-chore-title" className="action-dialog" onCancel={onClose} ref={dialog}>
    <form onSubmit={(event) => void submit(event)}>
      <p className="eyebrow">Up for grabs</p>
      <h2 id="claim-chore-title">Who wants to take “{assignment.title}”?</h2>
      <MemberPicker autoFocus legend="Choose a family member" members={participants} value={memberId}
        onChange={(nextMemberId) => { setMemberId(nextMemberId); requestId.current = crypto.randomUUID() }} />
      <p>After claiming it, the chore will move to the assigned list.</p>
      {error && <p role="alert">{error}</p>}
      <div className="form-actions">
        <button className="secondary-action" disabled={busy} onClick={onClose} type="button">Not now</button>
        <button className="primary-action" disabled={busy} type="submit">{busy ? 'Claiming…' : 'I’ll do it'}</button>
      </div>
    </form>
  </dialog>
}

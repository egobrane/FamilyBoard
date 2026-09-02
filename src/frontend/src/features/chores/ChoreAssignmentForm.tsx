import { useRef, useState } from 'react'
import { ApiError, createChoreAssignment, type ChoreDefinitionResponse, type ChoreParticipantResponse } from '../../lib/api'

export function ChoreAssignmentForm({ householdId, definitions, participants, onSaved }: {
  householdId: string
  definitions: ChoreDefinitionResponse[]
  participants: ChoreParticipantResponse[]
  onSaved: () => void
}) {
  const [definitionId, setDefinitionId] = useState('')
  const [assignmentMode, setAssignmentMode] = useState<'assigned' | 'open'>('assigned')
  const [memberId, setMemberId] = useState('')
  const [date, setDate] = useState(() => new Date().toISOString().slice(0, 10))
  const [time, setTime] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const requestId = useRef(crypto.randomUUID())
  function changed(update: () => void) {
    update()
    requestId.current = crypto.randomUUID()
  }
  async function submit(event: React.FormEvent) {
    event.preventDefault(); setBusy(true); setError('')
    try {
      await createChoreAssignment(householdId, { clientRequestId: requestId.current,
        choreDefinitionId: definitionId, assignmentMode,
        assignedMemberId: assignmentMode === 'assigned' ? memberId : null,
        dueLocalDate: date, dueLocalTime: time || null })
      requestId.current = crypto.randomUUID()
      setDefinitionId(''); setMemberId(''); setTime(''); onSaved()
    } catch (reason) { setError(reason instanceof ApiError ? reason.problem.title : 'The assignment could not be created.') }
    finally { setBusy(false) }
  }
  return <form className="admin-form" onSubmit={(event) => void submit(event)}>
    <h3>Assign one time</h3>
    <label>Chore<select required value={definitionId} onChange={(event) => changed(() => setDefinitionId(event.target.value))}>
      <option value="">Choose a chore</option>{definitions.filter((item) => item.isActive).map((item) => <option key={item.id} value={item.id}>{item.title}</option>)}</select></label>
    <fieldset className="assignment-mode"><legend>Who should do it?</legend>
      <label><input checked={assignmentMode === 'assigned'} name="assignment-mode" onChange={() => changed(() => setAssignmentMode('assigned'))} type="radio" />Specific person</label>
      <label><input checked={assignmentMode === 'open'} name="assignment-mode" onChange={() => changed(() => setAssignmentMode('open'))} type="radio" />Up for grabs</label>
    </fieldset>
    {assignmentMode === 'assigned' && <label>Family member<select required value={memberId} onChange={(event) => changed(() => setMemberId(event.target.value))}>
      <option value="">Choose a person</option>{participants.map((item) => <option key={item.id} value={item.id}>{item.displayName}</option>)}</select></label>}
    <div className="form-grid"><label>Due date<input onChange={(event) => changed(() => setDate(event.target.value))} required type="date" value={date} /></label>
      <label>Time (optional)<input onChange={(event) => changed(() => setTime(event.target.value))} type="time" value={time} /></label></div>
    {error && <p role="alert">{error}</p>}
    <button className="primary-action" disabled={busy || definitions.every((item) => !item.isActive)} type="submit">{busy ? 'Assigning…' : 'Assign chore'}</button>
  </form>
}

import { useState, type FormEvent } from 'react'
import { ApiError, createChoreSchedule, updateChoreSchedule, type ChoreDefinitionResponse,
  type ChoreParticipantResponse, type ChoreRecurrenceRequest, type ChoreScheduleResponse } from '../../lib/api'
import { ChoreRecurrenceFields } from './ChoreRecurrenceFields'

interface Props {
  householdId: string
  definitions: ChoreDefinitionResponse[]
  participants: ChoreParticipantResponse[]
  schedule?: ChoreScheduleResponse
  onSaved: () => void
  onCancel?: () => void
}

export function ChoreScheduleForm({ householdId, definitions, participants, schedule, onSaved, onCancel }: Props) {
  const [definitionId, setDefinitionId] = useState(schedule?.definition.id ?? '')
  const [assignmentMode, setAssignmentMode] = useState<'assigned' | 'open'>(schedule?.assignmentMode ?? 'assigned')
  const [memberId, setMemberId] = useState(schedule?.assignedMember?.id ?? '')
  const [recurrence, setRecurrence] = useState<ChoreRecurrenceRequest>(schedule?.recurrence
    ?? { kind: 'daily', interval: 1, daysOfWeek: [] })
  const [startDate, setStartDate] = useState(schedule?.startLocalDate ?? new Date().toISOString().slice(0, 10))
  const [endDate, setEndDate] = useState(schedule?.endLocalDate ?? '')
  const [dueTime, setDueTime] = useState(schedule?.dueLocalTime?.slice(0, 5) ?? '08:00')
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)

  async function submit(event: FormEvent) {
    event.preventDefault(); setError(''); setSaving(true)
    try {
      const body = { choreDefinitionId: definitionId, assignmentMode,
        assignedMemberId: assignmentMode === 'assigned' ? memberId : null, recurrence,
        startLocalDate: startDate, endLocalDate: endDate || null, dueLocalTime: dueTime || null }
      if (schedule) await updateChoreSchedule(householdId, schedule, body)
      else await createChoreSchedule(householdId, body)
      onSaved()
    } catch (reason) {
      setError(reason instanceof ApiError ? reason.problem.title : 'The schedule could not be saved.')
    } finally { setSaving(false) }
  }

  const recurrenceSummary = recurrence.kind === 'daily'
    ? `Every ${recurrence.interval === 1 ? 'day' : `${recurrence.interval} days`}`
    : `Every ${recurrence.interval === 1 ? 'week' : `${recurrence.interval} weeks`} on ${recurrence.daysOfWeek.join(', ') || 'no selected days'}`

  return <form className="admin-section schedule-form" onSubmit={(event) => void submit(event)}>
    <h3>{schedule ? 'Edit schedule' : 'Schedule a chore'}</h3>
    <p>Assignments appear in advance. The time below is the household-local due time.</p>
    {error && <p role="alert">{error}</p>}
    <div className="form-grid">
      <label>Chore<select onChange={(event) => setDefinitionId(event.target.value)} required value={definitionId}>
        <option value="">Choose a chore</option>{definitions.filter((item) => item.isActive).map((item) =>
          <option key={item.id} value={item.id}>{item.title}</option>)}</select></label>
      <fieldset className="assignment-mode"><legend>Who should do it?</legend>
        <label><input checked={assignmentMode === 'assigned'} name="schedule-assignment-mode"
          onChange={() => setAssignmentMode('assigned')} type="radio" />Specific person</label>
        <label><input checked={assignmentMode === 'open'} name="schedule-assignment-mode"
          onChange={() => setAssignmentMode('open')} type="radio" />Up for grabs</label>
      </fieldset>
      {assignmentMode === 'assigned' && <label>Assigned to<select onChange={(event) => setMemberId(event.target.value)} required value={memberId}>
        <option value="">Choose a person</option>{participants.map((item) =>
          <option key={item.id} value={item.id}>{item.displayName}</option>)}</select></label>}
    </div>
    <ChoreRecurrenceFields onChange={setRecurrence} value={recurrence} />
    <div className="form-grid">
      <label>Starts<input onChange={(event) => setStartDate(event.target.value)} required type="date" value={startDate} /></label>
      <label>Ends (optional)<input min={startDate} onChange={(event) => setEndDate(event.target.value)} type="date" value={endDate} /></label>
      <label>Due time<input onChange={(event) => setDueTime(event.target.value)} type="time" value={dueTime} /></label>
    </div>
    <p aria-live="polite" className="schedule-summary"><strong>{assignmentMode === 'open' ? 'Up for grabs' : participants.find((person) => person.id === memberId)?.displayName ?? 'Specific person'} · {recurrenceSummary}</strong>{dueTime ? `, due at ${dueTime}` : ', due by end of day'}.</p>
    <div className="form-actions"><button disabled={saving} type="submit">{saving ? 'Saving…' : 'Save schedule'}</button>
      {onCancel && <button className="button-secondary" onClick={onCancel} type="button">Cancel</button>}</div>
  </form>
}

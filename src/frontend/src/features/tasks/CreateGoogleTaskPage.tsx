import { useRef, useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { ApiError, createGoogleTask } from '../../lib/api'
import { useAuthentication } from '../authentication/AuthenticationContext'

export function CreateGoogleTaskPage() {
  const { state } = useAuthentication()
  const navigate = useNavigate()
  const household = state.status === 'authenticated'
    ? state.currentUser.households.find((item) => item.id === state.currentUser.selectedHouseholdId) : undefined
  const requestId = useRef(crypto.randomUUID())
  const [title, setTitle] = useState('')
  const [notes, setNotes] = useState('')
  const [dueDate, setDueDate] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  if (!household) return null
  async function submit(event: React.FormEvent) {
    event.preventDefault()
    setBusy(true); setError('')
    try {
      await createGoogleTask(household!.id, { idempotencyKey: requestId.current,
        title, notes: notes.trim() || null, dueDate: dueDate || null })
      requestId.current = crypto.randomUUID()
      navigate('/tasks', { replace: true, state: { message: 'Task added to Google Tasks.' } })
    } catch (reason) {
      setError(reason instanceof ApiError ? reason.problem.title : 'The task could not be added.')
    } finally { setBusy(false) }
  }
  return <main className="feature-page task-create-page" id="main-content">
    <header className="feature-header"><div><p className="eyebrow">Google Tasks</p><h2>Add a task</h2><p>The task is saved directly to the household’s writable Google list.</p></div><Link className="secondary-action" to="/tasks">Cancel</Link></header>
    <form className="admin-form task-create-form" onSubmit={(event) => void submit(event)}>
      <label>Task title<input autoFocus maxLength={200} onChange={(event) => { setTitle(event.target.value); requestId.current = crypto.randomUUID() }} required value={title}/></label>
      <label>Notes<textarea maxLength={2000} onChange={(event) => { setNotes(event.target.value); requestId.current = crypto.randomUUID() }} rows={4} value={notes}/></label>
      <label>Due date <span className="optional-label">Optional, date only</span><input onChange={(event) => { setDueDate(event.target.value); requestId.current = crypto.randomUUID() }} type="date" value={dueDate}/></label>
      {error && <p role="alert">{error}</p>}
      <button className="primary-action" disabled={busy} type="submit">{busy ? 'Adding…' : 'Add task'}</button>
    </form>
  </main>
}

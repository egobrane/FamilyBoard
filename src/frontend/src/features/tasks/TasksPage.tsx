import { useCallback, useEffect, useState } from 'react'
import { Link, useLocation } from 'react-router'
import { ApiError, listGoogleTasks, listHouseholdMembers, updateGoogleTaskStatus,
  type GoogleTaskResponse, type GoogleTasksResponse, type HouseholdMemberResponse } from '../../lib/api'
import { useAuthentication } from '../authentication/AuthenticationContext'
import { GoogleTaskList } from './GoogleTaskList'

export function TasksPage() {
  const { state } = useAuthentication()
  const location = useLocation()
  const household = state.status === 'authenticated'
    ? state.currentUser.households.find((item) => item.id === state.currentUser.selectedHouseholdId) : undefined
  const shared = state.status === 'authenticated' && state.currentUser.session?.isSharedDisplay === true
  const [includeCompleted, setIncludeCompleted] = useState(false)
  const [result, setResult] = useState<GoogleTasksResponse | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [message, setMessage] = useState((location.state as { message?: string } | null)?.message ?? '')
  const [members, setMembers] = useState<HouseholdMemberResponse[]>([])
  const [attributedMemberId, setAttributedMemberId] = useState('')
  const [busyTaskId, setBusyTaskId] = useState<string | null>(null)
  const load = useCallback(async (cursor?: string, append = false) => {
    if (!household) return
    setLoading(true); setError('')
    try {
      const response = await listGoogleTasks(household.id, includeCompleted, cursor)
      setResult((current) => append && current
        ? { ...response, canCreateTasks: current.canCreateTasks || response.canCreateTasks,
          tasks: [...current.tasks, ...response.tasks], warnings: [...current.warnings, ...response.warnings] }
        : response)
    } catch (reason) { setError(reason instanceof ApiError ? reason.problem.title : 'Google Tasks could not be loaded.') }
    finally { setLoading(false) }
  }, [household, includeCompleted])
  useEffect(() => { const handle = window.setTimeout(() => void load(), 0); return () => window.clearTimeout(handle) }, [load])
  useEffect(() => {
    if (!household || !shared) return
    void listHouseholdMembers(household.id).then((items) => setMembers(items.filter((item) => item.isActive)))
  }, [household, shared])
  if (!household) return null

  async function changeStatus(task: GoogleTaskResponse) {
    if (!task.mutationVersion) return
    if (shared && !attributedMemberId) { setError('Choose who is using the board before changing a task.'); return }
    setBusyTaskId(task.id); setError(''); setMessage('')
    try {
      await updateGoogleTaskStatus(household!.id, { sourceId: task.sourceId, taskId: task.id,
        idempotencyKey: crypto.randomUUID(), attributedMemberId: shared ? attributedMemberId : null,
        targetStatus: task.status === 'completed' ? 'needsAction' : 'completed', mutationVersion: task.mutationVersion })
      setMessage(task.status === 'completed' ? 'Task reopened in Google Tasks.' : 'Task completed in Google Tasks.')
      await load()
    } catch (reason) { setError(reason instanceof ApiError ? reason.problem.title : 'The task could not be changed.') }
    finally { setBusyTaskId(null) }
  }

  return <main className="feature-page tasks-page" id="main-content" tabIndex={-1}>
    <header className="feature-header"><div><p className="eyebrow">Plans from Google</p><h2>Tasks</h2></div><div className="feature-header__actions">{result?.canCreateTasks && <Link className="primary-action" to="/tasks/new">Add task</Link>}{household.role === 'adult' && <Link className="secondary-action" to={`/households/${household.id}/tasks`}>Task settings</Link>}</div></header>
    {shared && <label className="task-attribution">Who is using the board?<select aria-label="Household member attribution" onChange={(event) => setAttributedMemberId(event.target.value)} value={attributedMemberId}><option value="">Choose a household member</option>{members.map((member) => <option key={member.id} value={member.id}>{member.displayName}</option>)}</select></label>}
    {message && <p className="calendar-status calendar-status--success" role="status">{message}</p>}
    <div aria-label="Task list view" className="segmented-control" role="group"><button aria-pressed={!includeCompleted} onClick={() => setIncludeCompleted(false)} type="button">To do</button><button aria-pressed={includeCompleted} onClick={() => setIncludeCompleted(true)} type="button">Include completed</button></div>
    {loading && !result && <p role="status">Loading Google Tasks…</p>}
    {error && <div className="admin-status" role="alert"><p>{error}</p><button onClick={() => void load()} type="button">Try again</button></div>}
    {result?.warnings.map((warning) => <p className="preview-note" key={`${warning.sourceId}:${warning.code}`} role="status">{warning.message}</p>)}
    {result?.isStale && <p className="preview-note">Showing recently cached tasks.</p>}
    {!loading && !error && result?.tasks.length === 0 && <section className="empty-state"><h3>No tasks to show</h3><p>An adult can choose visible Google task lists in household settings.</p></section>}
    {result && result.tasks.length > 0 && <GoogleTaskList busyTaskId={busyTaskId} tasks={result.tasks} onStatusChange={(task) => void changeStatus(task)} />}
    {result?.nextCursor && <button className="secondary-action" disabled={loading} onClick={() => void load(result.nextCursor ?? undefined, true)} type="button">{loading ? 'Loading…' : 'Load more'}</button>}
  </main>
}

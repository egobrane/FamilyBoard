import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router'
import { DashboardCard } from '../../components/DashboardCard'
import { ApiError, listGoogleTasks, updateGoogleTaskStatus,
  type GoogleTaskResponse, type GoogleTasksResponse } from '../../lib/api'
import { useAuthentication } from '../authentication/AuthenticationContext'
import { GoogleTaskList } from './GoogleTaskList'

export function DashboardTasksCard() {
  const { state } = useAuthentication()
  const householdId = state.status === 'authenticated' ? state.currentUser.selectedHouseholdId : null
  const [result, setResult] = useState<GoogleTasksResponse | null>(null)
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const [busyTaskId, setBusyTaskId] = useState<string | null>(null)
  const load = useCallback(async () => {
    if (!householdId) return
    try { setResult(await listGoogleTasks(householdId)); setError('') }
    catch { setError('Tasks are temporarily unavailable.') }
  }, [householdId])
  useEffect(() => {
    const handle = window.setTimeout(() => void load(), 0)
    return () => window.clearTimeout(handle)
  }, [load])

  async function changeStatus(task: GoogleTaskResponse) {
    if (!householdId || !task.mutationVersion) return
    setBusyTaskId(task.id); setError(''); setMessage('')
    try {
      await updateGoogleTaskStatus(householdId, { sourceId: task.sourceId, taskId: task.id,
        idempotencyKey: crypto.randomUUID(),
        targetStatus: task.status === 'completed' ? 'needsAction' : 'completed', mutationVersion: task.mutationVersion })
      setMessage(task.status === 'completed' ? 'Task reopened.' : 'Task completed.')
      await load()
    } catch (reason) {
      setError(reason instanceof ApiError ? reason.problem.title : 'The task could not be changed.')
    } finally { setBusyTaskId(null) }
  }

  return <DashboardCard className="tasks-card" eyebrow="What’s next" id="tasks-preview" title="Tasks"
    action={<Link className="status-pill status-pill--link" to="/tasks">Open tasks</Link>}>
    {!result && !error && <p className="preview-note" role="status">Loading Google Tasks…</p>}
    {error && <p className="preview-note" role="alert">{error}</p>}
    {message && <p className="preview-note" role="status">{message}</p>}
    {result?.tasks.length === 0 && <p className="preview-note">No Google tasks are showing.</p>}
    {result && result.tasks.length > 0 && <GoogleTaskList busyTaskId={busyTaskId} compact
      onStatusChange={(task) => void changeStatus(task)} tasks={result.tasks.slice(0, 4)} />}
  </DashboardCard>
}

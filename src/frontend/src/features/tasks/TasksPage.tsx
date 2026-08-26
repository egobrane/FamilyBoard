import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router'
import { ApiError, listGoogleTasks, type GoogleTasksResponse } from '../../lib/api'
import { useAuthentication } from '../authentication/AuthenticationContext'
import { GoogleTaskList } from './GoogleTaskList'

export function TasksPage() {
  const { state } = useAuthentication()
  const household = state.status === 'authenticated'
    ? state.currentUser.households.find((item) => item.id === state.currentUser.selectedHouseholdId) : undefined
  const [includeCompleted, setIncludeCompleted] = useState(false)
  const [result, setResult] = useState<GoogleTasksResponse | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const load = useCallback(async (cursor?: string, append = false) => {
    if (!household) return
    setLoading(true); setError('')
    try {
      const response = await listGoogleTasks(household.id, includeCompleted, cursor)
      setResult((current) => append && current
        ? { ...response, tasks: [...current.tasks, ...response.tasks], warnings: [...current.warnings, ...response.warnings] }
        : response)
    } catch (reason) {
      setError(reason instanceof ApiError ? reason.problem.title : 'Google Tasks could not be loaded.')
    } finally { setLoading(false) }
  }, [household, includeCompleted])
  useEffect(() => {
    const handle = window.setTimeout(() => void load(), 0)
    return () => window.clearTimeout(handle)
  }, [load])
  if (!household) return null
  return (
    <main className="feature-page tasks-page" id="main-content">
      <header className="feature-header">
        <div><p className="eyebrow">Plans from Google</p><h2>Tasks</h2></div>
        {household.role === 'adult' && <Link className="secondary-action" to={`/households/${household.id}/tasks`}>Task settings</Link>}
      </header>
      <div aria-label="Task list view" className="segmented-control" role="group">
        <button aria-pressed={!includeCompleted} onClick={() => setIncludeCompleted(false)} type="button">To do</button>
        <button aria-pressed={includeCompleted} onClick={() => setIncludeCompleted(true)} type="button">Include completed</button>
      </div>
      {loading && !result && <p role="status">Loading Google Tasks…</p>}
      {error && <div className="admin-status" role="alert"><p>{error}</p><button onClick={() => void load()} type="button">Try again</button></div>}
      {result?.warnings.map((warning) => <p className="preview-note" key={`${warning.sourceId}:${warning.code}`} role="status">{warning.message}</p>)}
      {result?.isStale && <p className="preview-note">Showing recently cached tasks.</p>}
      {!loading && !error && result?.tasks.length === 0 && <section className="empty-state"><h3>No tasks to show</h3><p>An adult can choose visible Google task lists in household settings.</p></section>}
      {result && result.tasks.length > 0 && <GoogleTaskList tasks={result.tasks} />}
      {result?.nextCursor && <button className="secondary-action" disabled={loading} onClick={() => void load(result.nextCursor ?? undefined, true)} type="button">{loading ? 'Loading…' : 'Load more'}</button>}
    </main>
  )
}

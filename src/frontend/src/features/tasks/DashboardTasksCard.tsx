import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import { DashboardCard } from '../../components/DashboardCard'
import { listGoogleTasks, type GoogleTasksResponse } from '../../lib/api'
import { useAuthentication } from '../authentication/AuthenticationContext'
import { GoogleTaskList } from './GoogleTaskList'

export function DashboardTasksCard() {
  const { state } = useAuthentication()
  const householdId = state.status === 'authenticated' ? state.currentUser.selectedHouseholdId : null
  const [result, setResult] = useState<GoogleTasksResponse | null>(null)
  const [failed, setFailed] = useState(false)
  useEffect(() => {
    if (!householdId) return
    let active = true
    void listGoogleTasks(householdId).then((value) => { if (active) setResult(value) })
      .catch(() => { if (active) setFailed(true) })
    return () => { active = false }
  }, [householdId])
  return <DashboardCard className="tasks-card" eyebrow="What’s next" id="tasks-preview" title="Tasks"
    action={<Link className="status-pill status-pill--link" to="/tasks">Open tasks</Link>}>
    {!result && !failed && <p className="preview-note" role="status">Loading Google Tasks…</p>}
    {failed && <p className="preview-note" role="alert">Tasks are temporarily unavailable.</p>}
    {result?.tasks.length === 0 && <p className="preview-note">No Google tasks are showing.</p>}
    {result && result.tasks.length > 0 && <GoogleTaskList compact tasks={result.tasks.slice(0, 4)} />}
  </DashboardCard>
}

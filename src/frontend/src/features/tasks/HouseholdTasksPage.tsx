import { useCallback, useEffect, useState } from 'react'
import { useLocation, useParams } from 'react-router'
import {
  ApiError, beginTasksAuthorization, disconnectTasks, getTasksConnection,
  listProviderTaskLists, listTaskListSources, updateTaskListSources, updateTaskWriteTarget,
  type ProviderTaskListResponse, type TaskListSourceResponse, type TasksConnectionResponse,
} from '../../lib/api'

export function HouseholdTasksPage() {
  const { householdId } = useParams()
  const location = useLocation()
  const [connection, setConnection] = useState<TasksConnectionResponse | null>(null)
  const [lists, setLists] = useState<ProviderTaskListResponse[]>([])
  const [sources, setSources] = useState<TaskListSourceResponse[]>([])
  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [writeTargetId, setWriteTargetId] = useState<string>('')
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [message, setMessage] = useState(new URLSearchParams(location.search).get('tasks') === 'connected'
    ? 'Google Tasks connected. Choose what this household can see.' : '')
  const [confirmDisconnect, setConfirmDisconnect] = useState(false)
  const load = useCallback(async () => {
    if (!householdId) return
    setLoading(true)
    try {
      const status = await getTasksConnection(householdId)
      setConnection(status)
      if (status.status === 'active') {
        const [available, configured] = await Promise.all([
          listProviderTaskLists(householdId), listTaskListSources(householdId),
        ])
        setLists(available); setSources(configured)
        setWriteTargetId(configured.find((item) => item.isWriteTarget)?.id ?? '')
        setSelected(new Set(available.filter((item) => item.isSelected).map((item) => item.id)))
      } else { setLists([]); setSources([]); setSelected(new Set()) }
      setError('')
    } catch (reason) {
      setError(reason instanceof ApiError && reason.problem.code === 'parent_elevation_required'
        ? 'Unlock parent access to manage Google Tasks.' : 'Google Tasks settings could not be loaded.')
    } finally { setLoading(false) }
  }, [householdId])
  useEffect(() => {
    const handle = window.setTimeout(() => void load(), 0)
    return () => window.clearTimeout(handle)
  }, [load])
  if (!householdId) return null
  async function connect(capability = 'read') {
    setBusy(true); setError('')
    try {
      const result = await beginTasksAuthorization(householdId!, `/households/${householdId}/tasks`, capability)
      window.location.assign(result.authorizationUrl)
    } catch { setError('Google Tasks authorization could not be started.'); setBusy(false) }
  }
  async function saveWriteTarget() {
    setBusy(true); setError('')
    try {
      await updateTaskWriteTarget(householdId!, writeTargetId || null)
      setMessage(writeTargetId ? 'Writable Google task list saved.' : 'Task actions disabled for this household.')
      await load()
    } catch (reason) { setError(reason instanceof ApiError ? reason.problem.title : 'The writable task list could not be saved.') }
    finally { setBusy(false) }
  }
  async function save() {
    if (!connection?.connectionId) return
    setBusy(true); setError('')
    try {
      const updated = await updateTaskListSources(householdId!, connection.connectionId, [...selected])
      setSources(updated)
      setWriteTargetId(updated.find((source) => source.isWriteTarget)?.id ?? '')
      setMessage('Visible Google task lists saved.')
    } catch { setError('The selected task lists could not be saved.') }
    finally { setBusy(false) }
  }
  async function disconnect() {
    if (!connection?.connectionId) return
    setBusy(true); setError('')
    try {
      await disconnectTasks(householdId!, connection.connectionId)
      setConfirmDisconnect(false); setMessage('Google Tasks disconnected from every household.')
      await load()
    } catch { setError('Google Tasks could not be disconnected.'); setBusy(false) }
  }
  if (loading) return <section className="admin-status" role="status"><p>Loading Google Tasks settings…</p></section>
  return <section aria-labelledby="tasks-settings-title" className="admin-panel tasks-settings">
    <div className="admin-panel__heading"><div><p className="eyebrow">Google Tasks integration</p><h3 id="tasks-settings-title">Google Tasks</h3><p>Tasks authorization is separate from Google sign-in and Calendar. Reading and task actions are authorized independently.</p></div></div>
    {message && <div className="calendar-status calendar-status--success" role="status">{message}</div>}
    {error && <div className="calendar-status calendar-status--error" role="alert">{error}</div>}
    {!connection?.isAvailable && <div className="calendar-status calendar-status--warning">Google Tasks is not enabled for this environment.</div>}
    {connection?.isAvailable && connection.status !== 'active' && <div className="calendar-connection-card"><div><h4>{connection.status === 'reauthorizationRequired' ? 'Reconnect Google Tasks' : 'Connect Google Tasks'}</h4><p>Choose a Google account and grant read-only Tasks access.</p></div><button className="primary-action" disabled={busy} onClick={() => void connect()} type="button">{busy ? 'Opening Google…' : connection.status === 'reauthorizationRequired' ? 'Reconnect' : 'Connect'}</button></div>}
    {connection?.status === 'active' && <>
      <div className="calendar-connection-card"><div><p className="eyebrow">Connected account</p><h4>{connection.providerEmail}</h4><p>{connection.activeSourceCount} task list{connection.activeSourceCount === 1 ? '' : 's'} visible to this household.</p><p>{connection.canWrite ? 'Task actions authorized.' : 'Read-only authorization.'}</p></div><div className="button-group">{connection.mutationsAvailable && !connection.canWrite && <button className="secondary-action" disabled={busy} onClick={() => void connect('write')} type="button">Enable task actions</button>}<button className="danger-action" disabled={busy} onClick={() => setConfirmDisconnect(true)} type="button">Disconnect</button></div></div>
      <fieldset className="calendar-source-picker"><legend>Task lists visible to this household</legend><p>Select only lists appropriate for the shared family display.</p>
        {lists.length === 0 && <p>No readable task lists were returned by Google.</p>}
        {lists.map((list) => <label className="calendar-source-option" key={list.id}><input checked={selected.has(list.id)} onChange={(event) => setSelected((current) => { const next = new Set(current); if (event.target.checked) next.add(list.id); else next.delete(list.id); return next })} type="checkbox"/><span aria-hidden="true" className="task-list-dot"/><strong>{list.name}</strong></label>)}
      </fieldset>
      <button className="primary-action" disabled={busy} onClick={() => void save()} type="button">{busy ? 'Saving…' : 'Save visible task lists'}</button>
      {connection.mutationsAvailable && connection.canWrite && <fieldset className="calendar-source-picker"><legend>Writable task list</legend><p>Choose one list where this household may create, complete, and reopen tasks.</p><label className="calendar-source-option"><input checked={writeTargetId === ''} name="write-target" onChange={() => setWriteTargetId('')} type="radio"/><strong>No writable list</strong></label>{sources.filter((source) => source.isActive && source.canWrite && source.isOwnedByCurrentAdult).map((source) => <label className="calendar-source-option" key={source.id}><input checked={writeTargetId === source.id} name="write-target" onChange={() => setWriteTargetId(source.id)} type="radio"/><span aria-hidden="true" className="task-list-dot"/><strong>{source.name}</strong></label>)}<button className="secondary-action" disabled={busy} onClick={() => void saveWriteTarget()} type="button">Save writable list</button></fieldset>}
      {sources.some((source) => source.isActive && !source.isOwnedByCurrentAdult) && <p className="preview-note">This household also shows lists shared by another active adult.</p>}
      {confirmDisconnect && <div aria-labelledby="tasks-disconnect-title" aria-modal="true" className="dialog-backdrop" role="dialog"><div className="dialog-card"><h3 id="tasks-disconnect-title">Disconnect Google Tasks everywhere?</h3><p>This removes this adult’s task lists from every household. Google tasks are not changed or deleted.</p><div className="dialog-actions"><button className="secondary-action" onClick={() => setConfirmDisconnect(false)} type="button">Keep connected</button><button className="danger-action" disabled={busy} onClick={() => void disconnect()} type="button">Disconnect everywhere</button></div></div></div>}
    </>}
  </section>
}

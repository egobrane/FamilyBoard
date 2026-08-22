import { useRef, useState } from 'react'
import { ApiError, createChoreDefinition, updateChoreDefinition, type ChoreDefinitionResponse } from '../../lib/api'

export function ChoreDefinitionForm({ householdId, definition, onSaved, onCancel }: {
  householdId: string
  definition?: ChoreDefinitionResponse
  onSaved: () => void
  onCancel?: () => void
}) {
  const [title, setTitle] = useState(definition?.title ?? '')
  const [description, setDescription] = useState(definition?.description ?? '')
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
      if (definition) await updateChoreDefinition(householdId, definition.id, {
        expectedVersion: definition.version, title, description: description || null,
      })
      else await createChoreDefinition(householdId, {
        clientRequestId: requestId.current, title, description: description || null,
      })
      requestId.current = crypto.randomUUID()
      onSaved()
    } catch (reason) { setError(reason instanceof ApiError ? reason.problem.title : 'The chore could not be saved.') }
    finally { setBusy(false) }
  }
  return <form className="admin-form" onSubmit={(event) => void submit(event)}>
    <h3>{definition ? 'Edit chore' : 'Create a chore'}</h3>
    <label>Chore name<input maxLength={120} onChange={(event) => changed(() => setTitle(event.target.value))} required value={title} /></label>
    <label>Helpful details<textarea maxLength={500} onChange={(event) => changed(() => setDescription(event.target.value))} value={description} /></label>
    {error && <p role="alert">{error}</p>}
    <div className="form-actions">{onCancel && <button className="secondary-action" onClick={onCancel} type="button">Cancel</button>}
      <button className="primary-action" disabled={busy} type="submit">{busy ? 'Saving…' : 'Save chore'}</button></div>
  </form>
}

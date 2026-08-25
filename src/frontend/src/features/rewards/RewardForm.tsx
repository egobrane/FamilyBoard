import { useRef, useState } from 'react'
import { ApiError, createRewardDefinition, updateRewardDefinition, type RewardResponse } from '../../lib/api'

export function RewardForm({ householdId, reward, onSaved, onCancel }: { householdId: string; reward?: RewardResponse; onSaved: () => void; onCancel?: () => void }) {
  const [title, setTitle] = useState(reward?.title ?? ''); const [description, setDescription] = useState(reward?.description ?? '')
  const [pointCost, setPointCost] = useState(reward?.pointCost ?? 10); const [busy, setBusy] = useState(false); const [error, setError] = useState('')
  const requestId = useRef(crypto.randomUUID())
  async function submit(event: React.FormEvent) { event.preventDefault(); setBusy(true); setError('')
    try { const body = { title, description: description.trim() || null, pointCost }
      if (reward) await updateRewardDefinition(householdId, reward, body)
      else await createRewardDefinition(householdId, { ...body, clientRequestId: requestId.current })
      requestId.current = crypto.randomUUID(); onSaved() }
    catch (cause) { setError(cause instanceof ApiError ? cause.problem.title : 'The reward could not be saved.') }
    finally { setBusy(false) } }
  return <form className="admin-form" onSubmit={(event) => void submit(event)}><h3>{reward ? 'Edit reward' : 'Create a reward'}</h3>
    <label>Title<input maxLength={120} onChange={(e) => setTitle(e.target.value)} required value={title} /></label>
    <label>Description<textarea maxLength={500} onChange={(e) => setDescription(e.target.value)} value={description} /></label>
    <label>Point cost<input inputMode="numeric" max={10000} min={1} onChange={(e) => setPointCost(e.target.valueAsNumber || 1)} required type="number" value={pointCost} /></label>
    <div className="form-actions">{onCancel && <button onClick={onCancel} type="button">Cancel</button>}<button className="primary-action" disabled={busy} type="submit">{busy ? 'Saving…' : 'Save reward'}</button></div>
    {error && <p role="alert">{error}</p>}</form>
}

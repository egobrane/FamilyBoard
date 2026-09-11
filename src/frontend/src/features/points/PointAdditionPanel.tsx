import { useRef, useState } from 'react'
import type { PointAdditionDefinitionResponse, PointMemberBalanceResponse } from '../../lib/api'

export function PointAdditionPanel({ definitions, members, busy, onCreate, onUpdate,
  onToggle, onApply, selectedMemberId }: {
  definitions: PointAdditionDefinitionResponse[]
  members: PointMemberBalanceResponse[]
  busy: boolean
  selectedMemberId: string
  onCreate: (requestId: string, title: string, amount: number) => Promise<boolean>
  onUpdate: (definition: PointAdditionDefinitionResponse, title: string,
    amount: number) => Promise<boolean>
  onToggle: (definition: PointAdditionDefinitionResponse, active: boolean) => Promise<boolean>
  onApply: (definition: PointAdditionDefinitionResponse, memberId: string,
    requestId: string) => Promise<boolean>
}) {
  const activeMembers = members.filter((member) => member.isActive)
  const activeDefinitions = definitions.filter((definition) => definition.isActive)
  const [editing, setEditing] = useState<PointAdditionDefinitionResponse | null>(null)
  const [confirming, setConfirming] = useState<PointAdditionDefinitionResponse | null>(null)
  const [title, setTitle] = useState('')
  const [amount, setAmount] = useState(0)
  const createRequestId = useRef(crypto.randomUUID())
  const applicationRequestId = useRef(crypto.randomUUID())

  function beginEdit(definition: PointAdditionDefinitionResponse) {
    setEditing(definition)
    setTitle(definition.title)
    setAmount(definition.additionAmount)
  }

  function resetForm() {
    setEditing(null)
    setTitle('')
    setAmount(0)
    createRequestId.current = crypto.randomUUID()
  }

  async function submit(event: React.FormEvent) {
    event.preventDefault()
    const saved = editing
      ? await onUpdate(editing, title, amount)
      : await onCreate(createRequestId.current, title, amount)
    if (saved) resetForm()
  }

  function beginApply(definition: PointAdditionDefinitionResponse) {
    applicationRequestId.current = crypto.randomUUID()
    setConfirming(definition)
  }

  async function apply() {
    if (!confirming || !selectedMemberId) return
    if (await onApply(confirming, selectedMemberId, applicationRequestId.current)) setConfirming(null)
  }

  const selectedMember = activeMembers.find((member) => member.memberId === selectedMemberId)

  return <section className="point-addition-panel admin-section" aria-labelledby="quick-additions-heading">
    <div className="point-addition-panel__heading">
      <div><p className="eyebrow">Celebrate good habits</p><h3 id="quick-additions-heading">Quick additions</h3>
        <p>Select a saved reason to record an auditable point award. Repeatable work with review should remain a chore.</p></div>
    </div>

    {activeDefinitions.length > 0
      ? <div className="point-addition-grid">{activeDefinitions.map((definition) =>
          <button className="point-addition-card" disabled={busy || !selectedMemberId} key={definition.id}
            onClick={() => beginApply(definition)} type="button">
            <strong>{definition.title}</strong><span>+{definition.additionAmount.toLocaleString()} points</span>
          </button>)}</div>
      : <p className="empty-state">No quick additions have been defined yet.</p>}

    <div className="point-addition-management">
      <form className="admin-form" onSubmit={(event) => void submit(event)}>
        <h3>{editing ? 'Edit quick addition' : 'Create a quick addition'}</h3>
        <label>Reason<input maxLength={120} onChange={(event) => setTitle(event.target.value)}
          placeholder="Homework completed on time" required value={title} /></label>
        <label>Points to add<input inputMode="numeric" max={10000} min={1}
          onChange={(event) => setAmount(event.target.valueAsNumber || 0)} required type="number"
          value={amount || ''} /></label>
        <div className="form-actions">{editing && <button onClick={resetForm} type="button">Cancel</button>}
          <button className="primary-action" disabled={busy || !title.trim() || amount < 1}
            type="submit">{busy ? 'Saving…' : editing ? 'Save addition' : 'Create addition'}</button></div>
      </form>
      <section className="admin-section point-addition-library">
        <h3>Saved additions</h3>
        {definitions.length === 0 ? <p>Nothing has been saved yet.</p>
          : <ul>{definitions.map((definition) => <li key={definition.id}>
            <div><strong>{definition.title}</strong><span>+{definition.additionAmount.toLocaleString()} points</span>
              {!definition.isActive && <small>Inactive</small>}</div>
            <div className="form-actions"><button disabled={busy} onClick={() => beginEdit(definition)}
              type="button">Edit</button><button disabled={busy} onClick={() => void onToggle(definition,
                !definition.isActive)} type="button">{definition.isActive ? 'Deactivate' : 'Reactivate'}</button></div>
          </li>)}</ul>}
      </section>
    </div>

    {confirming && selectedMember && <div aria-labelledby="confirm-addition-heading"
      aria-modal="true" className="dialog-backdrop" role="dialog">
      <section className="dialog-card point-addition-confirmation">
        <h3 id="confirm-addition-heading">Add {confirming.additionAmount.toLocaleString()} points to {selectedMember.displayName}?</h3>
        <p><strong>{confirming.title}</strong> will be recorded in the permanent point history.</p>
        <div className="form-actions"><button disabled={busy} onClick={() => setConfirming(null)} type="button">Cancel</button>
          <button autoFocus className="primary-action" disabled={busy} onClick={() => void apply()} type="button">
            {busy ? 'Recording…' : `Add ${confirming.additionAmount.toLocaleString()} points`}</button></div>
      </section>
    </div>}
  </section>
}

import { useRef, useState } from 'react'
import type { PointDeductionDefinitionResponse, PointMemberBalanceResponse } from '../../lib/api'

export function PointDeductionPanel({ definitions, members, busy, onCreate, onUpdate,
  onToggle, onApply, selectedMemberId }: {
  definitions: PointDeductionDefinitionResponse[]
  members: PointMemberBalanceResponse[]
  busy: boolean
  selectedMemberId: string
  onCreate: (requestId: string, title: string, amount: number) => Promise<boolean>
  onUpdate: (definition: PointDeductionDefinitionResponse, title: string,
    amount: number) => Promise<boolean>
  onToggle: (definition: PointDeductionDefinitionResponse, active: boolean) => Promise<boolean>
  onApply: (definition: PointDeductionDefinitionResponse, memberId: string,
    requestId: string) => Promise<boolean>
}) {
  const activeMembers = members.filter((member) => member.isActive)
  const activeDefinitions = definitions.filter((definition) => definition.isActive)
  const [editing, setEditing] = useState<PointDeductionDefinitionResponse | null>(null)
  const [confirming, setConfirming] = useState<PointDeductionDefinitionResponse | null>(null)
  const [title, setTitle] = useState('')
  const [amount, setAmount] = useState(0)
  const createRequestId = useRef(crypto.randomUUID())
  const applicationRequestId = useRef(crypto.randomUUID())

  function beginEdit(definition: PointDeductionDefinitionResponse) {
    setEditing(definition)
    setTitle(definition.title)
    setAmount(definition.deductionAmount)
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

  function beginApply(definition: PointDeductionDefinitionResponse) {
    applicationRequestId.current = crypto.randomUUID()
    setConfirming(definition)
  }

  async function apply() {
    if (!confirming || !selectedMemberId) return
    if (await onApply(confirming, selectedMemberId, applicationRequestId.current)) setConfirming(null)
  }

  const selectedMember = activeMembers.find((member) => member.memberId === selectedMemberId)

  return <section className="point-deduction-panel admin-section" aria-labelledby="quick-deductions-heading">
    <div className="point-deduction-panel__heading">
      <div><p className="eyebrow">Reusable point corrections</p><h3 id="quick-deductions-heading">Quick deductions</h3>
        <p>Choose a family member, then select a reason. Every deduction remains in the point ledger and can be reversed.</p></div>
    </div>

    {activeDefinitions.length > 0
      ? <div className="point-deduction-grid">{activeDefinitions.map((definition) =>
          <button className="point-deduction-card" disabled={busy || !selectedMemberId} key={definition.id}
            onClick={() => beginApply(definition)} type="button">
            <strong>{definition.title}</strong><span>−{definition.deductionAmount.toLocaleString()} points</span>
          </button>)}</div>
      : <p className="empty-state">No quick deductions have been defined yet.</p>}

    <div className="point-deduction-management">
      <form className="admin-form" onSubmit={(event) => void submit(event)}>
        <h3>{editing ? 'Edit quick deduction' : 'Create a quick deduction'}</h3>
        <label>Reason<input maxLength={120} onChange={(event) => setTitle(event.target.value)}
          placeholder="Hitting someone" required value={title} /></label>
        <label>Points to deduct<input inputMode="numeric" max={10000} min={1}
          onChange={(event) => setAmount(event.target.valueAsNumber || 0)} required type="number"
          value={amount || ''} /></label>
        <div className="form-actions">{editing && <button onClick={resetForm} type="button">Cancel</button>}
          <button className="primary-action" disabled={busy || !title.trim() || amount < 1}
            type="submit">{busy ? 'Saving…' : editing ? 'Save deduction' : 'Create deduction'}</button></div>
      </form>
      <section className="admin-section point-deduction-library">
        <h3>Saved deductions</h3>
        {definitions.length === 0 ? <p>Nothing has been saved yet.</p>
          : <ul>{definitions.map((definition) => <li key={definition.id}>
            <div><strong>{definition.title}</strong><span>−{definition.deductionAmount.toLocaleString()} points</span>
              {!definition.isActive && <small>Inactive</small>}</div>
            <div className="form-actions"><button disabled={busy} onClick={() => beginEdit(definition)}
              type="button">Edit</button><button disabled={busy} onClick={() => void onToggle(definition,
                !definition.isActive)} type="button">{definition.isActive ? 'Deactivate' : 'Reactivate'}</button></div>
          </li>)}</ul>}
      </section>
    </div>

    {confirming && selectedMember && <div aria-labelledby="confirm-deduction-heading"
      aria-modal="true" className="dialog-backdrop" role="dialog">
      <section className="dialog-card point-deduction-confirmation">
        <h3 id="confirm-deduction-heading">Deduct {confirming.deductionAmount.toLocaleString()} points from {selectedMember.displayName}?</h3>
        <p><strong>{confirming.title}</strong> will be recorded in the permanent point history.</p>
        <div className="form-actions"><button disabled={busy} onClick={() => setConfirming(null)} type="button">Cancel</button>
          <button autoFocus className="danger-action" disabled={busy} onClick={() => void apply()} type="button">
            {busy ? 'Recording…' : `Deduct ${confirming.deductionAmount.toLocaleString()} points`}</button></div>
      </section>
    </div>}
  </section>
}

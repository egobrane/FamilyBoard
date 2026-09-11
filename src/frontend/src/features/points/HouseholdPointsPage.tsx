import { useCallback, useEffect, useRef, useState } from 'react'
import { useParams } from 'react-router'
import { ApiError, createPointAdjustment, getPointSummary, listPointTransactions,
  listPointAdditionDefinitions, createPointAdditionDefinition,
  updatePointAdditionDefinition, setPointAdditionDefinitionActive, applyPointAddition,
  reversePointTransaction, listPointDeductionDefinitions, createPointDeductionDefinition,
  updatePointDeductionDefinition, setPointDeductionDefinitionActive, applyPointDeduction,
  type HouseholdPointSummaryResponse, type PointAdditionDefinitionResponse,
  type PointDeductionDefinitionResponse,
  type PointTransactionResponse } from '../../lib/api'
import { MemberPicker } from '../../components/MemberPicker'
import { PointAdditionPanel } from './PointAdditionPanel'
import { PointHistoryList } from './PointHistoryList'
import { PointDeductionPanel } from './PointDeductionPanel'

export function HouseholdPointsPage() {
  const { householdId = '' } = useParams()
  const [summary, setSummary] = useState<HouseholdPointSummaryResponse | null>(null)
  const [transactions, setTransactions] = useState<PointTransactionResponse[]>([])
  const [additionDefinitions, setAdditionDefinitions] = useState<PointAdditionDefinitionResponse[]>([])
  const [definitions, setDefinitions] = useState<PointDeductionDefinitionResponse[]>([])
  const [quickMemberId, setQuickMemberId] = useState('')
  const [memberId, setMemberId] = useState('')
  const [amount, setAmount] = useState(0)
  const [reason, setReason] = useState('')
  const [reversing, setReversing] = useState<PointTransactionResponse | null>(null)
  const [reversalReason, setReversalReason] = useState('')
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')
  const requestId = useRef(crypto.randomUUID())
  const reversalRequestId = useRef(crypto.randomUUID())
  const load = useCallback(async () => {
    setLoading(true); setError('')
    try { const [nextSummary, history, nextAdditionDefinitions, nextDefinitions] = await Promise.all([
      getPointSummary(householdId), listPointTransactions(householdId),
      listPointAdditionDefinitions(householdId),
      listPointDeductionDefinitions(householdId),
    ])
      setSummary(nextSummary); setTransactions(history.items)
      setAdditionDefinitions(nextAdditionDefinitions); setDefinitions(nextDefinitions)
      if (!memberId && nextSummary.members[0]) setMemberId(nextSummary.members[0].memberId) }
    catch (cause) { setError(cause instanceof ApiError ? cause.problem.title : 'Point administration could not be loaded.') }
    finally { setLoading(false) }
  }, [householdId, memberId])
  useEffect(() => { const handle = window.setTimeout(() => void load(), 0); return () => window.clearTimeout(handle) }, [load])
  async function adjust(event: React.FormEvent) {
    event.preventDefault(); setBusy(true); setError(''); setSuccess('')
    try { await createPointAdjustment(householdId, { clientRequestId: requestId.current, householdMemberId: memberId, amount, reason })
      requestId.current = crypto.randomUUID(); setAmount(0); setReason(''); setSuccess('Point correction recorded.'); await load() }
    catch (cause) { setError(cause instanceof ApiError ? cause.problem.title : 'The correction could not be recorded.') }
    finally { setBusy(false) }
  }
  async function reverse(event: React.FormEvent) {
    event.preventDefault(); if (!reversing) return
    setBusy(true); setError(''); setSuccess('')
    try { await reversePointTransaction(householdId, reversing.id, reversalRequestId.current, reversalReason)
      reversalRequestId.current = crypto.randomUUID(); setReversing(null); setReversalReason(''); setSuccess('Compensating reversal recorded.'); await load() }
    catch (cause) { setError(cause instanceof ApiError ? cause.problem.title : 'The reversal could not be recorded.') }
    finally { setBusy(false) }
  }
  async function perform(operation: () => Promise<unknown>, message: string,
    fallback: string) {
    setBusy(true); setError(''); setSuccess('')
    try { await operation(); setSuccess(message); await load(); return true }
    catch (cause) { setError(cause instanceof ApiError ? cause.problem.title : fallback); return false }
    finally { setBusy(false) }
  }
  if (loading) return <p role="status">Loading point administration…</p>
  if (!summary) return <section className="admin-status" role="alert"><p>{error || 'Point administration is unavailable.'}</p><button onClick={() => void load()} type="button">Try again</button></section>
  const activeMembers = summary.members.filter((member) => member.isActive)
  const selectedQuickMemberId = activeMembers.some((member) => member.memberId === quickMemberId)
    ? quickMemberId : activeMembers[0]?.memberId ?? ''

  return <div className="point-admin-grid">
  <section className="point-quick-actions admin-section" aria-labelledby="quick-point-actions-heading">
    <div><p className="eyebrow">Fast household recognition</p><h2 id="quick-point-actions-heading">Quick point actions</h2>
      <p>Choose a family member once, then add or deduct points using a saved reason.</p></div>
    {activeMembers.length > 0
      ? <MemberPicker legend="Who are these points for?" members={activeMembers.map((member) => ({
          id: member.memberId, displayName: member.displayName, avatarColor: member.avatarColor,
          photo: member.photo, detail: `${member.balance.toLocaleString()} points`,
        }))} value={selectedQuickMemberId} onChange={setQuickMemberId} />
      : <p className="empty-state">Add or reactivate a household member before recording quick points.</p>}
    <div className="point-quick-actions__columns">
  <PointAdditionPanel busy={busy} definitions={additionDefinitions} members={summary.members}
    selectedMemberId={selectedQuickMemberId}
    onCreate={(clientRequestId, title, additionAmount) => perform(
      () => createPointAdditionDefinition(householdId, { clientRequestId, title, additionAmount }),
      'Quick addition created.', 'The quick addition could not be created.')}
    onUpdate={(definition, title, additionAmount) => perform(
      () => updatePointAdditionDefinition(householdId, definition, { title, additionAmount }),
      'Quick addition updated.', 'The quick addition could not be updated.')}
    onToggle={(definition, active) => perform(
      () => setPointAdditionDefinitionActive(householdId, definition, active),
      active ? 'Quick addition reactivated.' : 'Quick addition deactivated.',
      'The quick addition could not be changed.')}
    onApply={(definition, selectedMemberId, clientRequestId) => perform(
      () => applyPointAddition(householdId, definition, {
        clientRequestId, householdMemberId: selectedMemberId,
      }), 'Point addition recorded.', 'The point addition could not be recorded.')} />
  <PointDeductionPanel busy={busy} definitions={definitions} members={summary.members}
    selectedMemberId={selectedQuickMemberId}
    onCreate={(clientRequestId, title, deductionAmount) => perform(
      () => createPointDeductionDefinition(householdId, { clientRequestId, title, deductionAmount }),
      'Quick deduction created.', 'The quick deduction could not be created.')}
    onUpdate={(definition, title, deductionAmount) => perform(
      () => updatePointDeductionDefinition(householdId, definition, { title, deductionAmount }),
      'Quick deduction updated.', 'The quick deduction could not be updated.')}
    onToggle={(definition, active) => perform(
      () => setPointDeductionDefinitionActive(householdId, definition, active),
      active ? 'Quick deduction reactivated.' : 'Quick deduction deactivated.',
      'The quick deduction could not be changed.')}
    onApply={(definition, selectedMemberId, clientRequestId) => perform(
      () => applyPointDeduction(householdId, definition, {
        clientRequestId, householdMemberId: selectedMemberId,
      }), 'Point deduction recorded.', 'The point deduction could not be recorded.')} />
    </div>
  </section>
  <form className="admin-form point-advanced-correction" onSubmit={(event) => void adjust(event)}>
    <h3>Record a correction</h3><p>Corrections add an immutable ledger entry. Use a negative amount to remove points.</p>
    <label>Household member<select onChange={(event) => { setMemberId(event.target.value); requestId.current = crypto.randomUUID() }} required value={memberId}>
      {summary.members.map((member) => <option key={member.memberId} value={member.memberId}>{member.displayName}{!member.isActive ? ' (inactive)' : ''}</option>)}</select></label>
    <label>Signed amount<input inputMode="numeric" max={10000} min={-10000} onChange={(event) => { setAmount(event.target.valueAsNumber || 0); requestId.current = crypto.randomUUID() }} required type="number" value={amount} /></label>
    <label>Reason<textarea maxLength={240} onChange={(event) => { setReason(event.target.value); requestId.current = crypto.randomUUID() }} required value={reason} /></label>
    <button className="primary-action" disabled={busy || amount === 0} type="submit">{busy ? 'Recording…' : 'Record correction'}</button>
  </form>
  <section className="admin-section"><h3>Ledger history</h3><PointHistoryList onReverse={(item) => { setReversing(item); setReversalReason(''); reversalRequestId.current = crypto.randomUUID() }} transactions={transactions} /></section>
  {reversing && <form className="admin-form" onSubmit={(event) => void reverse(event)}><h3>Reverse {reversing.amount} points for {reversing.householdMember.displayName}?</h3>
    <p>The original entry remains visible. This records an exact compensating transaction.</p><label>Reason<textarea autoFocus maxLength={240} onChange={(event) => setReversalReason(event.target.value)} required value={reversalReason} /></label>
    <div className="form-actions"><button onClick={() => setReversing(null)} type="button">Cancel</button><button className="primary-action" disabled={busy} type="submit">Confirm reversal</button></div></form>}
  {error && <p role="alert">{error}</p>}{success && <p aria-live="polite" className="success-message">{success}</p>}
  </div>
}

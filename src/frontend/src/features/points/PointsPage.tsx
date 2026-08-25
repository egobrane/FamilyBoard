import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router'
import { useAuthentication } from '../authentication/AuthenticationContext'
import { ApiError, getPointSummary, listPointTransactions, type HouseholdPointSummaryResponse,
  type PointTransactionResponse } from '../../lib/api'
import { PointHistoryList } from './PointHistoryList'

export function PointsPage() {
  const { state } = useAuthentication()
  const household = state.status === 'authenticated'
    ? state.currentUser.households.find((item) => item.id === state.currentUser.selectedHouseholdId) : undefined
  const [summary, setSummary] = useState<HouseholdPointSummaryResponse | null>(null)
  const [transactions, setTransactions] = useState<PointTransactionResponse[]>([])
  const [memberId, setMemberId] = useState('')
  const [nextCursor, setNextCursor] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const load = useCallback(async () => {
    if (!household) return
    setLoading(true); setError('')
    try {
      const [nextSummary, history] = await Promise.all([
        getPointSummary(household.id), listPointTransactions(household.id, memberId || undefined),
      ])
      setSummary(nextSummary); setTransactions(history.items); setNextCursor(history.nextCursor)
    } catch (reason) { setError(reason instanceof ApiError ? reason.problem.title : 'Point history could not be loaded.') }
    finally { setLoading(false) }
  }, [household, memberId])
  useEffect(() => { const handle = window.setTimeout(() => void load(), 0); return () => window.clearTimeout(handle) }, [load])
  if (!household) return null
  async function loadMore() {
    if (!nextCursor || !household) return
    try { const history = await listPointTransactions(household.id, memberId || undefined, nextCursor)
      setTransactions((current) => [...current, ...history.items]); setNextCursor(history.nextCursor) }
    catch (reason) { setError(reason instanceof ApiError ? reason.problem.title : 'More point history could not be loaded.') }
  }

  return <main className="feature-page points-page" id="main-content">
    <header className="feature-header"><div><p className="eyebrow">Progress together</p><h2>Family points</h2></div>
      {household.role === 'adult' && <Link className="secondary-action" to={`/households/${household.id}/points`}>Manage points</Link>}</header>
    {loading && <p role="status">Loading family points…</p>}
    {error && <div className="admin-status" role="alert"><p>{error}</p><button onClick={() => void load()} type="button">Try again</button></div>}
    {!loading && summary && <>
      <section aria-labelledby="point-balances-heading" className="point-balance-panel"><h3 id="point-balances-heading">Member balances</h3>
        <div className="point-balance-grid">{summary.members.map((member) => <article key={member.memberId}>
          <span>{member.displayName}{!member.isActive ? ' · inactive' : ''}</span><strong>{member.balance}</strong><small>points</small>
        </article>)}</div></section>
      <section aria-labelledby="point-history-heading" className="point-history-panel"><div className="point-history-heading">
        <h3 id="point-history-heading">Point history</h3><label>Member<select onChange={(event) => setMemberId(event.target.value)} value={memberId}>
          <option value="">Everyone</option>{summary.members.map((member) => <option key={member.memberId} value={member.memberId}>{member.displayName}</option>)}</select></label>
      </div><PointHistoryList transactions={transactions} />{nextCursor && <button className="secondary-action" onClick={() => void loadMore()} type="button">Load more history</button>}</section>
    </>}
  </main>
}

import { useCallback, useEffect, useRef, useState } from 'react'
import { Link } from 'react-router'
import { useAuthentication } from '../authentication/AuthenticationContext'
import { ApiError, getRewardCatalog, listRewardRedemptions, requestRewardRedemption,
  type RewardCatalogResponse, type RewardRedemptionResponse, type RewardResponse } from '../../lib/api'
import { RewardCard } from './RewardCard'
import { RedeemRewardDialog } from './RedeemRewardDialog'
import { RedemptionHistoryList } from './RedemptionHistoryList'

export function RewardsPage() {
  const { state } = useAuthentication()
  const household = state.status === 'authenticated' ? state.currentUser.households.find((x) => x.id === state.currentUser.selectedHouseholdId) : undefined
  const [catalog, setCatalog] = useState<RewardCatalogResponse | null>(null)
  const [history, setHistory] = useState<RewardRedemptionResponse[]>([])
  const [selected, setSelected] = useState<RewardResponse | null>(null)
  const [memberId, setMemberId] = useState('')
  const [loading, setLoading] = useState(true); const [busy, setBusy] = useState(false)
  const [error, setError] = useState(''); const [success, setSuccess] = useState('')
  const requestId = useRef(crypto.randomUUID())
  const load = useCallback(async () => {
    if (!household) return; setLoading(true); setError('')
    try { const [nextCatalog, nextHistory] = await Promise.all([getRewardCatalog(household.id), listRewardRedemptions(household.id)])
      setCatalog(nextCatalog); setHistory(nextHistory.items)
      if (nextCatalog.members.length === 1) setMemberId((current) => current || nextCatalog.members[0].memberId)
    } catch (cause) { setError(cause instanceof ApiError ? cause.problem.title : 'Rewards could not be loaded.') }
    finally { setLoading(false) }
  }, [household])
  useEffect(() => { const handle = window.setTimeout(() => void load(), 0); return () => clearTimeout(handle) }, [load])
  if (!household) return null
  const chosen = catalog?.members.find((x) => x.memberId === memberId)
  async function redeem() { if (!selected || !memberId) return; setBusy(true); setError(''); setSuccess('')
    try { await requestRewardRedemption(household!.id, selected.id, memberId, requestId.current)
      requestId.current = crypto.randomUUID(); setSelected(null); setSuccess('Reward request sent for adult review.'); await load() }
    catch (cause) { setError(cause instanceof ApiError ? cause.problem.title : 'The reward could not be requested.') }
    finally { setBusy(false) } }
  return <main className="feature-page rewards-page" id="main-content">
    <header className="feature-header"><div><p className="eyebrow">Enjoy what you earned</p><h2>Rewards</h2></div>
      <div className="form-actions"><Link className="secondary-action" to="/points">Point history</Link>
        {household.role === 'adult' && <Link className="secondary-action" to={`/households/${household.id}/rewards`}>Manage rewards</Link>}</div></header>
    {loading && <p role="status">Loading rewards…</p>}{error && <p role="alert">{error}</p>}{success && <p aria-live="polite" className="success-message">{success}</p>}
    {!loading && catalog && <><section aria-labelledby="reward-member-heading" className="admin-section"><h3 id="reward-member-heading">Choose a family member</h3>
      <label>Points belong to<select onChange={(event) => setMemberId(event.target.value)} value={memberId}><option value="">Choose a member</option>
        {catalog.members.map((member) => <option key={member.memberId} value={member.memberId}>{member.displayName} · {member.balance} points</option>)}</select></label></section>
      <section aria-labelledby="reward-catalog-heading"><h3 id="reward-catalog-heading">Reward catalog</h3>
        {catalog.rewards.length === 0 ? <p>No rewards are available yet.</p> : <div className="reward-grid">{catalog.rewards.map((reward) =>
          <RewardCard disabled={!chosen || chosen.balance < reward.pointCost} key={reward.id} onRedeem={() => { setSelected(reward); requestId.current = crypto.randomUUID() }} reward={reward} />)}</div>}</section>
      <section className="admin-section" aria-labelledby="redemption-history-heading"><h3 id="redemption-history-heading">Recent requests</h3><RedemptionHistoryList items={history} /></section></>}
    {selected && catalog && <RedeemRewardDialog busy={busy} members={catalog.members} onCancel={() => setSelected(null)}
      onConfirm={() => void redeem()} onMemberChange={setMemberId} reward={selected} selectedMemberId={memberId} />}
  </main>
}

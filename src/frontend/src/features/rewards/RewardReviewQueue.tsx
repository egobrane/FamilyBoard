import { useState } from 'react'
import { ApiError, cancelRewardRedemption, fulfillRewardRedemption, reviewRewardRedemption, type RewardRedemptionResponse } from '../../lib/api'

export function RewardReviewQueue({ householdId, items, onChanged }: { householdId: string; items: RewardRedemptionResponse[]; onChanged: () => void }) {
  const [error, setError] = useState(''); const [busy, setBusy] = useState('')
  async function act(item: RewardRedemptionResponse, action: 'approve' | 'reject' | 'fulfill' | 'cancel') {
    setBusy(item.id); setError('')
    try { if (action === 'approve') await reviewRewardRedemption(householdId, item, 'approved', null)
      else if (action === 'reject') await reviewRewardRedemption(householdId, item, 'rejected', 'Request rejected')
      else if (action === 'fulfill') await fulfillRewardRedemption(householdId, item)
      else await cancelRewardRedemption(householdId, item, 'Cancelled by an adult')
      onChanged() } catch (cause) { setError(cause instanceof ApiError ? cause.problem.title : 'The redemption could not be updated.') }
    finally { setBusy('') } }
  return <section className="admin-section"><h3>Redemption review</h3>{error && <p role="alert">{error}</p>}
    {items.filter((x) => x.status === 'requested' || x.status === 'approved').length === 0 ? <p>No requests need attention.</p> :
      <ul className="definition-list">{items.filter((x) => x.status === 'requested' || x.status === 'approved').map((item) => <li key={item.id}>
        <div><strong>{item.rewardTitle}</strong><span>{item.householdMember.displayName} · {item.pointCost} points · {item.status}</span></div>
        <div className="form-actions">{item.status === 'requested' && <><button disabled={busy === item.id} onClick={() => void act(item, 'approve')} type="button">Approve</button>
          <button disabled={busy === item.id} onClick={() => void act(item, 'reject')} type="button">Reject</button></>}
          {item.status === 'approved' && <button disabled={busy === item.id} onClick={() => void act(item, 'fulfill')} type="button">Mark fulfilled</button>}
          <button disabled={busy === item.id} onClick={() => void act(item, 'cancel')} type="button">Cancel request</button></div></li>)}</ul>}
  </section>
}

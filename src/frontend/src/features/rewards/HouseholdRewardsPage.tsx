import { useCallback, useEffect, useState } from 'react'
import { useParams } from 'react-router'
import { ApiError, listRewardDefinitions, listRewardRedemptions, setRewardActive,
  type RewardRedemptionResponse, type RewardResponse } from '../../lib/api'
import { RewardForm } from './RewardForm'
import { RewardReviewQueue } from './RewardReviewQueue'

export function HouseholdRewardsPage() {
  const { householdId = '' } = useParams(); const [definitions, setDefinitions] = useState<RewardResponse[]>([])
  const [redemptions, setRedemptions] = useState<RewardRedemptionResponse[]>([]); const [editing, setEditing] = useState<RewardResponse | null>(null)
  const [loading, setLoading] = useState(true); const [error, setError] = useState('')
  const load = useCallback(async () => { setLoading(true); setError(''); try { const [rewards, requests] = await Promise.all([
    listRewardDefinitions(householdId), listRewardRedemptions(householdId)])
    setDefinitions(rewards); setRedemptions(requests.items) } catch (cause) { setError(cause instanceof ApiError ? cause.problem.title : 'Reward administration could not be loaded.') }
    finally { setLoading(false) } }, [householdId])
  useEffect(() => { const handle = setTimeout(() => void load(), 0); return () => clearTimeout(handle) }, [load])
  if (loading) return <p role="status">Loading reward administration…</p>
  if (error) return <section className="admin-status" role="alert"><p>{error}</p><button onClick={() => void load()} type="button">Try again</button></section>
  return <div className="reward-admin-grid"><section className="admin-section"><h3>Reward definitions</h3>
    {definitions.length === 0 ? <p>No rewards have been created.</p> : <ul className="definition-list">{definitions.map((reward) => <li key={reward.id}>
      <div><strong>{reward.title}</strong><span>{reward.pointCost} points · {reward.isActive ? 'Active' : 'Inactive'}</span></div>
      <div className="form-actions"><button onClick={() => setEditing(reward)} type="button">Edit</button><button onClick={() => void setRewardActive(householdId, reward, !reward.isActive).then(load)} type="button">{reward.isActive ? 'Deactivate' : 'Reactivate'}</button></div>
    </li>)}</ul>}</section>
    <RewardForm householdId={householdId} key={editing?.id ?? 'new'} onCancel={editing ? () => setEditing(null) : undefined}
      onSaved={() => { setEditing(null); void load() }} reward={editing ?? undefined} />
    <RewardReviewQueue householdId={householdId} items={redemptions} onChanged={() => void load()} />
  </div>
}

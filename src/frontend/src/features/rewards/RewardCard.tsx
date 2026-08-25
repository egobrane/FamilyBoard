import type { RewardResponse } from '../../lib/api'

export function RewardCard({ reward, disabled, onRedeem }: {
  reward: RewardResponse; disabled: boolean; onRedeem: () => void
}) {
  return <article className="reward-card">
    <div><p className="eyebrow">{reward.pointCost} points</p><h3>{reward.title}</h3>
      {reward.description && <p>{reward.description}</p>}</div>
    <button className="primary-action" disabled={disabled} onClick={onRedeem} type="button">
      {disabled ? 'Not enough points' : 'Redeem reward'}
    </button>
  </article>
}

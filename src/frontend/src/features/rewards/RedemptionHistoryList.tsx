import type { RewardRedemptionResponse } from '../../lib/api'

export function RedemptionHistoryList({ items }: { items: RewardRedemptionResponse[] }) {
  if (items.length === 0) return <p>No reward requests yet.</p>
  return <ul className="definition-list redemption-list">{items.map((item) => <li key={item.id}>
    <div><strong>{item.rewardTitle}</strong><span>{item.householdMember.displayName} · {item.pointCost} points</span></div>
    <span className={`status-badge status-badge--${item.status}`}>{item.status}</span>
  </li>)}</ul>
}

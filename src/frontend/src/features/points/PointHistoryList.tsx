import type { PointTransactionResponse } from '../../lib/api'
import { MemberAvatar } from '../../components/MemberAvatar'

function transactionLabel(item: PointTransactionResponse) {
  if (item.type === 'choreCompletion') return 'Chore approved'
  if (item.type === 'adjustment') return 'Adjustment'
  if (item.type === 'reversal') return 'Reversal'
  return 'Reward redemption'
}

export function PointHistoryList({ transactions, onReverse }: {
  transactions: PointTransactionResponse[]
  onReverse?: (transaction: PointTransactionResponse) => void
}) {
  if (transactions.length === 0) return <p>No point activity yet.</p>
  return <ul className="point-history">{transactions.map((item) => (
    <li key={item.id}>
      <MemberAvatar member={item.householdMember} />
      <div className="point-history__details">
        <strong>{item.householdMember.displayName}</strong>
        <span>{transactionLabel(item)} · {item.description}</span>
        <small>{new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(item.createdAt))}
          {item.createdByMember ? ` · by ${item.createdByMember.displayName}` : ''}</small>
      </div>
      <div className="point-history__value">
        <strong className={item.amount >= 0 ? 'point-amount--positive' : 'point-amount--negative'}>
          {item.amount > 0 ? '+' : ''}{item.amount}
        </strong>
        {item.isReversed && <span className="status-pill">Reversed</span>}
        {onReverse && !item.isReversed && (item.type === 'choreCompletion' || item.type === 'adjustment')
          && <button onClick={() => onReverse(item)} type="button">Reverse</button>}
      </div>
    </li>
  ))}</ul>
}

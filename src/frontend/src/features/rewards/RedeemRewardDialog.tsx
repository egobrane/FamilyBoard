import type { PointMemberBalanceResponse, RewardResponse } from '../../lib/api'

export function RedeemRewardDialog({ reward, members, selectedMemberId, busy, onMemberChange, onCancel, onConfirm }: {
  reward: RewardResponse; members: PointMemberBalanceResponse[]; selectedMemberId: string; busy: boolean
  onMemberChange: (id: string) => void; onCancel: () => void; onConfirm: () => void
}) {
  return <section aria-labelledby="redeem-heading" className="admin-form reward-dialog" role="dialog" aria-modal="true">
    <h3 id="redeem-heading">Redeem {reward.title}?</h3><p>This reserves {reward.pointCost} points until an adult reviews the request.</p>
    <label>Who is redeeming?<select autoFocus onChange={(event) => onMemberChange(event.target.value)} required value={selectedMemberId}>
      <option value="">Choose a household member</option>{members.map((member) => <option key={member.memberId} value={member.memberId}>
        {member.displayName} · {member.balance} points</option>)}</select></label>
    <div className="form-actions"><button disabled={busy} onClick={onCancel} type="button">Cancel</button>
      <button className="primary-action" disabled={busy || !selectedMemberId} onClick={onConfirm} type="button">
        {busy ? 'Requesting…' : `Request for ${reward.pointCost} points`}</button></div>
  </section>
}

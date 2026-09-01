import type { PointMemberBalanceResponse, RewardResponse } from '../../lib/api'
import { MemberPicker } from '../../components/MemberPicker'

export function RedeemRewardDialog({ reward, members, selectedMemberId, busy, onMemberChange, onCancel, onConfirm }: {
  reward: RewardResponse; members: PointMemberBalanceResponse[]; selectedMemberId: string; busy: boolean
  onMemberChange: (id: string) => void; onCancel: () => void; onConfirm: () => void
}) {
  return <section aria-labelledby="redeem-heading" className="admin-form reward-dialog" role="dialog" aria-modal="true">
    <h3 id="redeem-heading">Redeem {reward.title}?</h3><p>This reserves {reward.pointCost} points until an adult reviews the request.</p>
    <MemberPicker autoFocus legend="Who is redeeming?" members={members.map((member) => ({
      ...member, id: member.memberId, detail: `${member.balance} points`,
    }))} onChange={onMemberChange} value={selectedMemberId} />
    <div className="form-actions"><button disabled={busy} onClick={onCancel} type="button">Cancel</button>
      <button className="primary-action" disabled={busy || !selectedMemberId} onClick={onConfirm} type="button">
        {busy ? 'Requesting…' : `Request for ${reward.pointCost} points`}</button></div>
  </section>
}

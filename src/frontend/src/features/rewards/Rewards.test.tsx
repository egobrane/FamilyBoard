import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { RewardCard } from './RewardCard'
import { RedeemRewardDialog } from './RedeemRewardDialog'

const reward = { id: 'reward-1', title: 'Movie night', description: 'Choose the movie', pointCost: 50,
  isActive: true, version: 1, createdAt: '2026-08-25T12:00:00Z', updatedAt: '2026-08-25T12:00:00Z' }

describe('Rewards', () => {
  it('offers a large explicit redemption action', async () => {
    const redeem = vi.fn(); render(<RewardCard disabled={false} onRedeem={redeem} reward={reward} />)
    await userEvent.click(screen.getByRole('button', { name: 'Redeem reward' }))
    expect(redeem).toHaveBeenCalledOnce(); expect(screen.getByText('50 points')).toBeInTheDocument()
  })

  it('requires explicit member attribution in the confirmation dialog', async () => {
    const change = vi.fn(); const confirm = vi.fn()
    render(<RedeemRewardDialog busy={false} members={[{ memberId: 'child-1', displayName: 'Zoey', role: 'child',
      avatarColor: null, isActive: true, balance: 80, photo: null }]} onCancel={vi.fn()} onConfirm={confirm}
      onMemberChange={change} reward={reward} selectedMemberId="" />)
    expect(screen.getByRole('button', { name: /Request for 50 points/ })).toBeDisabled()
    await userEvent.click(screen.getByRole('radio', { name: /Zoey/ }))
    expect(change).toHaveBeenCalledWith('child-1')
  })
})

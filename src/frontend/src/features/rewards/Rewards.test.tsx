import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { RewardCard } from './RewardCard'
import { RedeemRewardDialog } from './RedeemRewardDialog'
import { RewardMemberStandings } from './DashboardRewardsCard'

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

  it('shows every active household member ordered by balance and then name', () => {
    render(<RewardMemberStandings members={[
      { memberId: 'member-1', displayName: 'Annie', role: 'adult', avatarColor: null, isActive: true, balance: 0, photo: null },
      { memberId: 'member-2', displayName: 'Ella', role: 'child', avatarColor: null, isActive: true, balance: 30, photo: null },
      { memberId: 'member-3', displayName: 'Jayden', role: 'child', avatarColor: null, isActive: true, balance: 1100, photo: null },
      { memberId: 'member-4', displayName: 'Liam', role: 'child', avatarColor: null, isActive: true, balance: 0, photo: null },
      { memberId: 'member-5', displayName: 'Ryan', role: 'adult', avatarColor: null, isActive: true, balance: 30, photo: null },
      { memberId: 'member-6', displayName: 'Zoey', role: 'child', avatarColor: null, isActive: true, balance: 5, photo: null },
      { memberId: 'member-7', displayName: 'Inactive', role: 'child', avatarColor: null, isActive: false, balance: 5000, photo: null },
    ]} />)

    const names = screen.getAllByRole('listitem').map((item) => item.querySelector('strong')?.textContent)
    expect(names).toEqual(['Jayden', 'Ella', 'Ryan', 'Zoey', 'Annie', 'Liam'])
  })
})

import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { PointTransactionResponse } from '../../lib/api'
import { PointHistoryList } from './PointHistoryList'

const transaction: PointTransactionResponse = {
  id: 'transaction-1',
  householdMember: { id: 'member-1', displayName: 'Zoey', role: 'child', avatarColor: 'mint', isActive: true },
  amount: 10,
  type: 'choreCompletion',
  description: 'Completed Feed Milo',
  choreCompletionId: 'completion-1',
  rewardRedemptionId: null,
  reversesPointTransactionId: null,
  createdByMember: { id: 'adult-1', displayName: 'Ryan', role: 'adult', avatarColor: null, isActive: true },
  createdAt: '2026-08-25T12:00:00Z',
  isReversed: false,
}

describe('PointHistoryList', () => {
  it('communicates signed awards without relying on color and exposes a keyboard-operable reversal', async () => {
    const onReverse = vi.fn()
    render(<PointHistoryList onReverse={onReverse} transactions={[transaction]} />)
    expect(screen.getByText('Zoey')).toBeInTheDocument()
    expect(screen.getByText('+10')).toBeInTheDocument()
    expect(screen.getByText(/Chore approved · Completed Feed Milo/)).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Reverse' }))
    expect(onReverse).toHaveBeenCalledWith(transaction)
  })

  it('retains the original entry and labels reversed history', () => {
    render(<PointHistoryList transactions={[{ ...transaction, isReversed: true }]} />)
    expect(screen.getByText('Reversed')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Reverse' })).not.toBeInTheDocument()
  })
})

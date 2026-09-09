import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { PointTransactionResponse } from '../../lib/api'
import { PointHistoryList } from './PointHistoryList'
import { PointDeductionPanel } from './PointDeductionPanel'

const transaction: PointTransactionResponse = {
  id: 'transaction-1',
  householdMember: { id: 'member-1', displayName: 'Zoey', role: 'child', avatarColor: 'mint', isActive: true, photo: null },
  amount: 10,
  type: 'choreCompletion',
  description: 'Completed Feed Milo',
  choreCompletionId: 'completion-1',
  rewardRedemptionId: null,
  pointDeductionDefinitionId: null,
  reversesPointTransactionId: null,
  createdByMember: { id: 'adult-1', displayName: 'Ryan', role: 'adult', avatarColor: null, isActive: true, photo: null },
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

  it('identifies reusable deduction applications in retained history', () => {
    render(<PointHistoryList transactions={[{ ...transaction, amount: -500,
      type: 'adjustment', description: 'Hitting someone', choreCompletionId: null,
      pointDeductionDefinitionId: 'deduction-1' }]} />)
    expect(screen.getByText(/Quick deduction · Hitting someone/)).toBeInTheDocument()
  })
})

describe('PointDeductionPanel', () => {
  const definition = { id: 'deduction-1', title: 'Hitting someone', deductionAmount: 500,
    isActive: true, version: 2, createdAt: '2026-09-09T12:00:00Z',
    updatedAt: '2026-09-09T12:00:00Z' }
  const member = { memberId: 'member-1', displayName: 'Zoey', role: 'child' as const,
    avatarColor: 'mint', isActive: true, balance: 800, photo: null }

  it('uses the touch member picker and confirms before applying a preset', async () => {
    const onApply = vi.fn().mockResolvedValue(true)
    render(<PointDeductionPanel busy={false} definitions={[definition]} members={[member]}
      onApply={onApply} onCreate={vi.fn()} onToggle={vi.fn()} onUpdate={vi.fn()} />)

    expect(screen.getByRole('radio', { name: /Zoey/ })).toBeChecked()
    await userEvent.click(screen.getByRole('button', { name: /Hitting someone.*500 points/ }))
    expect(screen.getByRole('heading', { name: 'Deduct 500 points from Zoey?' })).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Deduct 500 points' }))
    expect(onApply).toHaveBeenCalledWith(definition, 'member-1', expect.any(String))
  })

  it('creates positive-magnitude definitions while presenting them as deductions', async () => {
    const onCreate = vi.fn().mockResolvedValue(true)
    render(<PointDeductionPanel busy={false} definitions={[]} members={[member]}
      onApply={vi.fn()} onCreate={onCreate} onToggle={vi.fn()} onUpdate={vi.fn()} />)

    await userEvent.type(screen.getByRole('textbox', { name: 'Reason' }), 'Unsafe behavior')
    await userEvent.type(screen.getByRole('spinbutton', { name: 'Points to deduct' }), '125')
    await userEvent.click(screen.getByRole('button', { name: 'Create deduction' }))
    expect(onCreate).toHaveBeenCalledWith(expect.any(String), 'Unsafe behavior', 125)
  })
})

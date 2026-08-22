import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ChoreList } from './ChoreList'
import type { ChoreAssignmentResponse } from '../../lib/api'

const assignment: ChoreAssignmentResponse = {
  id: 'assignment-1', choreDefinitionId: 'definition-1', title: 'Feed Milo',
  description: 'Before dinner', assignedMember: { id: 'member-1', displayName: 'Zoey', role: 'child', avatarColor: 'mint' },
  dueLocalDate: '2026-08-22', dueLocalTime: '18:00:00', dueAt: '2026-08-22T22:00:00Z',
  dueTimeZone: 'America/New_York', dueHasExplicitTime: true, status: 'pending', isOverdue: true,
  version: 1, pendingCompletion: null, createdAt: '2026-08-22T12:00:00Z', updatedAt: '2026-08-22T12:00:00Z',
}

describe('ChoreList', () => {
  it('shows attributed, overdue work and exposes a mouse and keyboard operable completion action', async () => {
    const onComplete = vi.fn()
    render(<ChoreList assignments={[assignment]} onComplete={onComplete} />)
    expect(screen.getByRole('heading', { name: 'Feed Milo' })).toBeInTheDocument()
    expect(screen.getByText(/Zoey/)).toBeInTheDocument()
    expect(screen.getByText('Overdue')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Mark done' }))
    expect(onComplete).toHaveBeenCalledWith(assignment)
  })

  it('prevents a second completion while adult review is pending', () => {
    render(<ChoreList assignments={[{ ...assignment, status: 'awaitingReview', isOverdue: false }]} onComplete={vi.fn()} />)
    expect(screen.getByText('Waiting for review')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Mark done' })).not.toBeInTheDocument()
  })
})

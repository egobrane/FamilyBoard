import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ChoreList } from './ChoreList'
import type { ChoreAssignmentResponse, ChoreScheduleResponse } from '../../lib/api'
import { ChoreRecurrenceFields } from './ChoreRecurrenceFields'
import { ChoreScheduleList } from './ChoreScheduleList'

const assignment: ChoreAssignmentResponse = {
  id: 'assignment-1', choreDefinitionId: 'definition-1', title: 'Feed Milo',
  description: 'Before dinner', pointValue: 10,
  assignedMember: { id: 'member-1', displayName: 'Zoey', role: 'child', avatarColor: 'mint' },
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

const schedule: ChoreScheduleResponse = {
  id: 'schedule-1', definition: { id: 'definition-1', title: 'Feed Milo', description: null,
    defaultPointValue: 10, isActive: true, version: 1, createdAt: '2026-08-22T12:00:00Z', updatedAt: '2026-08-22T12:00:00Z' },
  assignedMember: assignment.assignedMember, recurrence: { kind: 'daily', interval: 1, daysOfWeek: [] },
  startLocalDate: '2026-08-23', endLocalDate: null, dueLocalTime: '08:00:00',
  timeZone: 'America/New_York', status: 'active', blockedReason: null,
  nextOccurrenceLocalDate: '2026-08-23', lastGeneratedOccurrenceLocalDate: null,
  lastEvaluatedAt: null, version: 1, createdAt: '2026-08-22T12:00:00Z', updatedAt: '2026-08-22T12:00:00Z',
}

describe('Recurring chores', () => {
  it('summarizes a household-local schedule and exposes keyboard-operable edit and pause actions', async () => {
    const onEdit = vi.fn(); const onStateChange = vi.fn()
    render(<ChoreScheduleList onEdit={onEdit} onStateChange={onStateChange} schedules={[schedule]} />)
    expect(screen.getByText('Every day · Due 08:00')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Edit' }))
    await userEvent.click(screen.getByRole('button', { name: 'Pause' }))
    expect(onEdit).toHaveBeenCalledWith(schedule)
    expect(onStateChange).toHaveBeenCalledWith(schedule, false)
  })

  it('allows selected weekdays without relying on hover', async () => {
    const onChange = vi.fn()
    render(<ChoreRecurrenceFields onChange={onChange} value={{ kind: 'weekly', interval: 1, daysOfWeek: ['monday'] }} />)
    await userEvent.click(screen.getByRole('button', { name: 'wed' }))
    expect(onChange).toHaveBeenCalledWith({ kind: 'weekly', interval: 1, daysOfWeek: ['monday', 'wednesday'] })
  })
})

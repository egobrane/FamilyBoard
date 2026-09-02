import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ChoreList } from './ChoreList'
import type { ChoreAssignmentResponse, ChoreScheduleResponse } from '../../lib/api'
import { ChoreRecurrenceFields } from './ChoreRecurrenceFields'
import { ChoreScheduleList } from './ChoreScheduleList'
import { DashboardChoresCard } from './DashboardChoresCard'
import { AuthenticationProvider } from '../authentication/AuthenticationContext'

const householdId = '20000000-0000-0000-0000-000000000001'
const response = (body: unknown) => new Response(JSON.stringify(body), {
  status: 200, headers: { 'Content-Type': 'application/json' },
})

afterEach(() => vi.unstubAllGlobals())

const assignment: ChoreAssignmentResponse = {
  id: 'assignment-1', choreDefinitionId: 'definition-1', title: 'Feed Milo',
  description: 'Before dinner', pointValue: 10,
  assignmentMode: 'assigned', claimedAt: null,
  assignedMember: { id: 'member-1', displayName: 'Zoey', role: 'child', avatarColor: 'mint', photo: null },
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

  it('lets a child submit an assigned chore from the dashboard for adult review', async () => {
    Object.defineProperty(HTMLDialogElement.prototype, 'showModal', { configurable: true,
      value(this: HTMLDialogElement) { this.setAttribute('open', '') } })
    const currentUser = { user: { id: 'user-1', displayName: 'Ryan', primaryEmail: 'ryan@example.test' },
      households: [{ id: householdId, name: 'Family', memberId: 'adult-1', role: 'adult' }],
      selectedHouseholdId: householdId, session: { expiresAt: '2026-09-03T00:00:00Z',
        isSharedDisplay: true, deviceLabel: 'Kitchen display', administrativeElevationHouseholdId: null,
        administrativeElevationExpiresAt: null } }
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = new URL(String(input)).pathname
      if (path === '/api/auth/me') return response(currentUser)
      if (path === '/api/auth/antiforgery') return response({ requestToken: 'csrf', headerName: 'X-CSRF-TOKEN' })
      if (path.endsWith('/chores/dashboard')) return response({ overdue: [assignment], dueToday: [],
        upcoming: [], open: [], awaitingReviewCount: 0 })
      if (path.endsWith('/chores/participants')) return response([assignment.assignedMember])
      if (path.endsWith(`/chore-assignments/${assignment.id}/completions`) && init?.method === 'POST')
        return response({ id: 'completion-1', assignmentId: assignment.id,
          completedByMember: assignment.assignedMember, status: 'pendingReview', wasSharedDisplay: true,
          pointValue: assignment.pointValue, completedAt: '2026-09-02T18:00:00Z', reviewedByMember: null,
          reviewedAt: null, reviewNote: null, version: 1, award: null })
      throw new Error(`Unexpected request: ${path}`)
    })
    vi.stubGlobal('fetch', fetchMock)
    render(<MemoryRouter><AuthenticationProvider><DashboardChoresCard /></AuthenticationProvider></MemoryRouter>)

    await userEvent.click(await screen.findByRole('button', { name: 'Mark Feed Milo done' }))
    expect(screen.getByRole('radio', { name: /Zoey/ })).toBeChecked()
    await userEvent.click(screen.getByRole('button', { name: 'Mark done' }))
    const completionCall = fetchMock.mock.calls.find(([input]) =>
      new URL(String(input)).pathname.endsWith(`/chore-assignments/${assignment.id}/completions`))
    expect(completionCall).toBeDefined()
    expect(JSON.parse(String(completionCall?.[1]?.body))).toEqual(expect.objectContaining({
      expectedAssignmentVersion: assignment.version, completedByMemberId: assignment.assignedMember!.id,
    }))
  })

  it('separates open chores and lets a shared-display member claim one', async () => {
    Object.defineProperty(HTMLDialogElement.prototype, 'showModal', { configurable: true,
      value(this: HTMLDialogElement) { this.setAttribute('open', '') } })
    const member = assignment.assignedMember!
    const openAssignment: ChoreAssignmentResponse = { ...assignment, id: 'open-assignment',
      assignmentMode: 'open', assignedMember: null }
    const currentUser = { user: { id: 'user-1', displayName: 'Ryan', primaryEmail: 'ryan@example.test' },
      households: [{ id: householdId, name: 'Family', memberId: 'adult-1', role: 'adult' }],
      selectedHouseholdId: householdId, session: { expiresAt: '2026-09-03T00:00:00Z',
        isSharedDisplay: true, deviceLabel: 'Kitchen display', administrativeElevationHouseholdId: null,
        administrativeElevationExpiresAt: null } }
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = new URL(String(input)).pathname
      if (path === '/api/auth/me') return response(currentUser)
      if (path === '/api/auth/antiforgery') return response({ requestToken: 'csrf', headerName: 'X-CSRF-TOKEN' })
      if (path.endsWith('/chores/dashboard')) return response({ overdue: [assignment],
        dueToday: [], upcoming: [], open: [openAssignment], awaitingReviewCount: 0 })
      if (path.endsWith('/chores/participants')) return response([member])
      if (path.endsWith(`/chore-assignments/${openAssignment.id}/claim`) && init?.method === 'POST')
        return response({ ...openAssignment, assignedMember: member, claimedAt: '2026-09-02T19:00:00Z', version: 2 })
      throw new Error(`Unexpected request: ${path}`)
    })
    vi.stubGlobal('fetch', fetchMock)
    render(<MemoryRouter><AuthenticationProvider><DashboardChoresCard /></AuthenticationProvider></MemoryRouter>)

    expect(await screen.findByRole('heading', { name: 'Assigned' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Up for grabs' })).toBeInTheDocument()
    await userEvent.click(await screen.findByRole('button', { name: 'I’ll do it' }))
    await userEvent.click(screen.getByRole('radio', { name: /Zoey/ }))
    await userEvent.click(screen.getAllByRole('button', { name: 'I’ll do it' }).at(-1)!)
    const claimCall = fetchMock.mock.calls.find(([input]) =>
      new URL(String(input)).pathname.endsWith(`/chore-assignments/${openAssignment.id}/claim`))
    expect(JSON.parse(String(claimCall?.[1]?.body))).toEqual(expect.objectContaining({
      expectedAssignmentVersion: openAssignment.version, householdMemberId: member.id,
    }))
  })
})

const schedule: ChoreScheduleResponse = {
  id: 'schedule-1', definition: { id: 'definition-1', title: 'Feed Milo', description: null,
    defaultPointValue: 10, isActive: true, version: 1, createdAt: '2026-08-22T12:00:00Z', updatedAt: '2026-08-22T12:00:00Z' },
  assignmentMode: 'assigned', assignedMember: assignment.assignedMember,
  recurrence: { kind: 'daily', interval: 1, daysOfWeek: [] },
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

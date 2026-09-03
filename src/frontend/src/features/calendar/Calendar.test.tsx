import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { App } from '../../app/App'
import { AuthenticationProvider } from '../authentication/AuthenticationContext'

const householdId = '20000000-0000-0000-0000-000000000001'

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

function currentUser() {
  return {
    user: { id: '10000000-0000-0000-0000-000000000001', displayName: 'Ryan Bamford', primaryEmail: 'ryan@example.test' },
    households: [{ id: householdId, name: 'Bamford Family', memberId: '30000000-0000-0000-0000-000000000001', role: 'adult' }],
    selectedHouseholdId: householdId,
    session: {
      expiresAt: '2026-08-28T20:05:38Z', isSharedDisplay: false, deviceLabel: null,
      administrativeElevationHouseholdId: null, administrativeElevationExpiresAt: null,
    },
  }
}

function household() {
  return {
    householdId, timeZone: 'America/New_York', locale: 'en-US', weekStartsOn: 'Sunday',
  }
}

afterEach(() => vi.unstubAllGlobals())

describe('calendar integration', () => {
  it('shows normalized read-only events on the family calendar', async () => {
    const user = userEvent.setup()
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const path = new URL(String(input)).pathname
      if (path === '/api/auth/me') return jsonResponse(currentUser())
      if (path.endsWith('/calendar/display-settings')) return jsonResponse(household())
      if (path.endsWith('/calendar/event-creation-target')) return jsonResponse({
        isAvailable: false, isAuthorized: false, sourceId: null, name: null, timeZone: null, color: null,
      })
      if (path.endsWith('/calendar/events')) return jsonResponse({
        events: [{
          id: 'event-1', sourceId: 'source-1', calendarName: 'School', title: 'Fall concert',
          isAllDay: false, start: '2026-08-20T22:00:00Z', end: '2026-08-21T00:00:00Z',
          timeZone: 'America/New_York', location: 'Auditorium', color: '#4285f4',
          canEdit: true, canDelete: true,
          managementId: '60000000-0000-0000-0000-000000000001',
          providerVersion: 'etag-one', managementUnavailableReason: null,
        }],
        nextCursor: null, isStale: false, warnings: [],
      })
      throw new Error(`Unexpected request: ${path}`)
    }))
    render(<MemoryRouter initialEntries={['/calendar?month=2026-08']}><AuthenticationProvider><App /></AuthenticationProvider></MemoryRouter>)

    expect(await screen.findByRole('heading', { name: 'Family calendar' })).toBeInTheDocument()
    expect((await screen.findAllByText('Fall concert')).length).toBeGreaterThan(0)
    await user.click(screen.getByRole('button', { name: /Thursday, August 20, 1 event/ }))
    expect(screen.getByRole('heading', { name: 'Thursday, August 20, 2026' })).toBeInTheDocument()
    expect(screen.getAllByText('Auditorium').length).toBeGreaterThan(0)
    expect(screen.getAllByText(/School/).length).toBeGreaterThan(0)
    expect(screen.getAllByRole('link', { name: 'Manage' })[0]).toHaveAttribute(
      'href', '/calendar/events/60000000-0000-0000-0000-000000000001/edit')
  })

  it('loads and saves the calendars selected for a household', async () => {
    const user = userEvent.setup()
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = new URL(String(input)).pathname
      if (path === '/api/auth/me') return jsonResponse(currentUser())
      if (path.endsWith('/calendar/display-settings')) return jsonResponse(household())
      if (path === '/api/auth/antiforgery') return jsonResponse({ requestToken: 'csrf', headerName: 'X-CSRF-TOKEN' })
      if (path.endsWith('/calendar/connection')) return jsonResponse({
        isAvailable: true, connectionId: '40000000-0000-0000-0000-000000000001',
        status: 'connected', providerEmail: 'calendar@example.test', connectedAt: '2026-08-18T12:00:00Z',
        canManageConnection: true, activeSourceCount: 1,
        eventCreationAvailable: false, eventCreationAuthorized: false,
      })
      if (path.endsWith('/calendar/event-creation-target')) return jsonResponse({
        isAvailable: false, isAuthorized: false, sourceId: null, name: null, timeZone: null, color: null,
      })
      if (path.endsWith('/calendar/provider-calendars')) return jsonResponse([
        { id: 'primary@example.test', name: 'Family', timeZone: 'America/New_York', color: '#4285f4', isPrimary: true, isSelected: true, accessRole: 'owner', canCreateEvents: false, isEventCreationTarget: false },
        { id: 'school@example.test', name: 'School', timeZone: 'America/New_York', color: '#73b49a', isPrimary: false, isSelected: false, accessRole: 'reader', canCreateEvents: false, isEventCreationTarget: false },
      ])
      if (path.endsWith('/calendar/sources') && init?.method === 'PUT') return jsonResponse([
        { id: 'source-1', connectionId: '40000000-0000-0000-0000-000000000001', externalCalendarId: 'primary@example.test', name: 'Family', isActive: true, isOwnedByCurrentAdult: true },
        { id: 'source-2', connectionId: '40000000-0000-0000-0000-000000000001', externalCalendarId: 'school@example.test', name: 'School', isActive: true, isOwnedByCurrentAdult: true },
      ])
      if (path.endsWith('/calendar/sources')) return jsonResponse([])
      throw new Error(`Unexpected request: ${path}`)
    })
    vi.stubGlobal('fetch', fetchMock)
    render(<MemoryRouter initialEntries={[`/households/${householdId}/calendars`]}><AuthenticationProvider><App /></AuthenticationProvider></MemoryRouter>)

    expect(await screen.findByRole('heading', { name: 'Google Calendar' })).toBeInTheDocument()
    await user.click(screen.getByRole('checkbox', { name: /School/ }))
    await user.click(screen.getByRole('button', { name: 'Save visible calendars' }))
    expect(await screen.findByText('Household calendars saved.')).toBeInTheDocument()
    expect(fetchMock).toHaveBeenCalledWith(
      `http://localhost:8080/api/households/${householdId}/calendar/sources`,
      expect.objectContaining({
        method: 'PUT',
        credentials: 'include',
        headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'csrf' }),
        body: JSON.stringify({
          connectionId: '40000000-0000-0000-0000-000000000001',
          externalCalendarIds: ['primary@example.test', 'school@example.test'],
        }),
      }),
    )
  })

  it('creates an event with credentials, antiforgery, and an idempotency key', async () => {
    const user = userEvent.setup()
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = new URL(String(input)).pathname
      if (path === '/api/auth/me') return jsonResponse(currentUser())
      if (path.endsWith('/calendar/display-settings')) return jsonResponse(household())
      if (path === '/api/auth/antiforgery') return jsonResponse({ requestToken: 'csrf', headerName: 'X-CSRF-TOKEN' })
      if (path.endsWith('/calendar/event-creation-target')) return jsonResponse({
        isAvailable: true,
        isAuthorized: true,
        sourceId: '50000000-0000-0000-0000-000000000001',
        name: 'Family',
        timeZone: 'America/New_York',
        color: '#4285f4',
      })
      if (path.endsWith('/calendar/events') && init?.method === 'POST') return jsonResponse({
        id: 'provider-event-id',
        sourceId: '50000000-0000-0000-0000-000000000001',
        calendarName: 'Family',
        title: 'Dentist appointment',
        isAllDay: false,
        start: '2026-08-20T10:00:00-04:00',
        end: '2026-08-20T11:00:00-04:00',
        timeZone: 'America/New_York',
        location: null,
        color: '#4285f4',
        attributedMemberId: currentUser().households[0].memberId,
        recoveredExistingEvent: false,
      }, 201)
      if (path.endsWith('/calendar/events')) return jsonResponse({ events: [], nextCursor: null, isStale: false, warnings: [] })
      throw new Error(`Unexpected request: ${path}`)
    })
    vi.stubGlobal('fetch', fetchMock)
    render(<MemoryRouter initialEntries={['/calendar/new?date=2026-09-12']}><AuthenticationProvider><App /></AuthenticationProvider></MemoryRouter>)

    expect(await screen.findByLabelText('Starts')).toHaveValue('2026-09-12T09:00')
    await user.type(await screen.findByLabelText('Event title'), 'Dentist appointment')
    await user.click(screen.getByRole('button', { name: 'Add to calendar' }))

    expect(await screen.findByText('Event added to Google Calendar.')).toBeInTheDocument()
    const createCall = fetchMock.mock.calls.find(([, init]) => init?.method === 'POST'
      && String(init.body).includes('Dentist appointment'))
    expect(createCall).toBeDefined()
    expect(createCall?.[1]).toEqual(expect.objectContaining({
      credentials: 'include',
      headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'csrf' }),
    }))
    expect(JSON.parse(String(createCall?.[1]?.body))).toEqual(expect.objectContaining({
      sourceId: '50000000-0000-0000-0000-000000000001',
      title: 'Dentist appointment',
      idempotencyKey: expect.any(String),
    }))
  })
})

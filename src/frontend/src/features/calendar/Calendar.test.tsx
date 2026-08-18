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

afterEach(() => vi.unstubAllGlobals())

describe('calendar integration', () => {
  it('shows normalized read-only events on the family calendar', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const path = new URL(String(input)).pathname
      if (path === '/api/auth/me') return jsonResponse(currentUser())
      if (path.endsWith('/calendar/events')) return jsonResponse({
        events: [{
          id: 'event-1', sourceId: 'source-1', calendarName: 'School', title: 'Fall concert',
          isAllDay: false, start: '2026-08-20T22:00:00Z', end: '2026-08-21T00:00:00Z',
          timeZone: 'America/New_York', location: 'Auditorium', color: '#4285f4',
        }],
        nextCursor: null, isStale: false, warnings: [],
      })
      throw new Error(`Unexpected request: ${path}`)
    }))
    render(<MemoryRouter initialEntries={['/calendar']}><AuthenticationProvider><App /></AuthenticationProvider></MemoryRouter>)

    expect(await screen.findByRole('heading', { name: 'Family calendar' })).toBeInTheDocument()
    expect(await screen.findByText('Fall concert')).toBeInTheDocument()
    expect(screen.getByText('Auditorium')).toBeInTheDocument()
    expect(screen.getByText(/School/)).toBeInTheDocument()
  })

  it('loads and saves the calendars selected for a household', async () => {
    const user = userEvent.setup()
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = new URL(String(input)).pathname
      if (path === '/api/auth/me') return jsonResponse(currentUser())
      if (path === '/api/auth/antiforgery') return jsonResponse({ requestToken: 'csrf', headerName: 'X-CSRF-TOKEN' })
      if (path.endsWith('/calendar/connection')) return jsonResponse({
        isAvailable: true, connectionId: '40000000-0000-0000-0000-000000000001',
        status: 'connected', providerEmail: 'calendar@example.test', connectedAt: '2026-08-18T12:00:00Z',
        canManageConnection: true, activeSourceCount: 1,
      })
      if (path.endsWith('/calendar/provider-calendars')) return jsonResponse([
        { id: 'primary@example.test', name: 'Family', timeZone: 'America/New_York', color: '#4285f4', isPrimary: true, isSelected: true },
        { id: 'school@example.test', name: 'School', timeZone: 'America/New_York', color: '#73b49a', isPrimary: false, isSelected: false },
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
})

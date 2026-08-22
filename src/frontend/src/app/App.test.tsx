import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { AuthenticationProvider } from '../features/authentication/AuthenticationContext'
import type { CurrentUser } from '../lib/api'
import { App } from './App'

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

function currentUser(overrides: Partial<CurrentUser> = {}): CurrentUser {
  return {
    user: {
      id: '10000000-0000-0000-0000-000000000001',
      displayName: 'Ryan Bamford',
      primaryEmail: 'ryan@example.test',
    },
    households: [{
      id: '20000000-0000-0000-0000-000000000001',
      name: 'Bamford-Fahie-Waltz Family',
      memberId: '30000000-0000-0000-0000-000000000001',
      role: 'adult',
    }],
    selectedHouseholdId: '20000000-0000-0000-0000-000000000001',
    session: {
      expiresAt: '2026-08-28T20:05:38Z',
      isSharedDisplay: false,
      deviceLabel: null,
      administrativeElevationHouseholdId: null,
      administrativeElevationExpiresAt: null,
    },
    ...overrides,
  }
}

function renderApp(path = '/') {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <AuthenticationProvider>
        <App />
      </AuthenticationProvider>
    </MemoryRouter>,
  )
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('App', () => {
  it('renders the authenticated touch-first dashboard with household context', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const path = new URL(String(input)).pathname
      if (path === '/api/auth/me') return jsonResponse(currentUser())
      if (path.endsWith('/chores/dashboard')) return jsonResponse({
        overdue: [],
        dueToday: [{
          id: 'chore-1',
          choreDefinitionId: 'definition-1',
          title: 'Feed Milo',
          description: null,
          assignedMember: { id: 'member-1', displayName: 'Zoey', role: 'child', avatarColor: 'mint' },
          dueLocalDate: '2026-08-22',
          dueLocalTime: null,
          dueAt: '2026-08-23T03:59:59Z',
          dueTimeZone: 'America/New_York',
          dueHasExplicitTime: false,
          status: 'pending',
          isOverdue: false,
          version: 1,
          pendingCompletion: null,
          createdAt: '2026-08-22T12:00:00Z',
          updatedAt: '2026-08-22T12:00:00Z',
        }],
        upcoming: [],
        awaitingReviewCount: 0,
      })
      if (path.endsWith('/calendar/events')) return jsonResponse({
        events: [{
          id: 'event-1',
          sourceId: '40000000-0000-0000-0000-000000000001',
          calendarName: 'Family',
          title: 'Dentist appointment',
          isAllDay: false,
          start: '2026-08-18T14:00:00Z',
          end: '2026-08-18T15:00:00Z',
          timeZone: 'America/New_York',
          location: null,
          color: '#73b49a',
        }],
        nextCursor: null,
        isStale: false,
        warnings: [],
      })
      throw new Error(`Unexpected request: ${path}`)
    }))
    renderApp()

    expect(await screen.findByRole('heading', { level: 1, name: 'Bamford-Fahie-Waltz Family' })).toBeInTheDocument()
    expect(screen.getByRole('main')).toBeInTheDocument()
    expect(screen.getByRole('navigation', { name: /primary/i })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Today' })).toBeInTheDocument()
    expect(await screen.findByText('Dentist appointment')).toBeInTheDocument()
    expect(screen.getAllByText('Oliver').length).toBeGreaterThan(0)
    expect(screen.getAllByText('Zoey').length).toBeGreaterThan(0)
    expect(screen.getByText('Feed Milo')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /account menu for Ryan Bamford/i })).toHaveTextContent('RB')

    const welcomeCard = screen.getByRole('region', { name: 'Ready for a good day?' })
    expect(welcomeCard.style.getPropertyValue('--household-photo')).toContain('/images/demo-family-photo.jpg')
  })

  it('shows the signed-out welcome state without flashing dashboard content', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(jsonResponse({
      title: 'Authentication is required.',
      status: 401,
      code: 'authentication_required',
    }, 401)))
    renderApp()

    expect(await screen.findByRole('heading', { name: /bring the whole family/i })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /continue with Google/i })).toHaveAttribute(
      'href',
      'http://localhost:8080/api/auth/login/google?returnUrl=%2F',
    )
    expect(screen.queryByText('Dentist appointment')).not.toBeInTheDocument()
  })

  it('shows safe Calendar callback failures to an authenticated adult', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(jsonResponse(currentUser())))
    renderApp('/auth/error?code=calendar_scope_missing')

    expect(await screen.findByRole('heading', {
      name: 'Google Calendar permissions were incomplete.',
    })).toBeInTheDocument()
    expect(screen.getByText(/no connection was saved/i)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Return to Calendar settings' })).toHaveAttribute(
      'href',
      '/households/20000000-0000-0000-0000-000000000001/calendars',
    )
    expect(screen.queryByText('calendar_scope_missing')).not.toBeInTheDocument()
  })

  it('routes an authenticated account with no household into atomic setup', async () => {
    const user = userEvent.setup()
    let hasHousehold = false
    const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input)
      if (url.endsWith('/api/auth/me')) {
        return jsonResponse(hasHousehold
          ? currentUser()
          : currentUser({ households: [], selectedHouseholdId: null }))
      }
      if (url.endsWith('/api/auth/antiforgery')) {
        return jsonResponse({ requestToken: 'request-token', headerName: 'X-CSRF-TOKEN' })
      }
      if (url.endsWith('/api/households')) {
        hasHousehold = true
        return jsonResponse({
          id: '20000000-0000-0000-0000-000000000001',
          name: 'Bamford-Fahie-Waltz Family',
          timeZone: 'America/New_York',
          locale: 'en-US',
          weekStartsOn: 'sunday',
          access: {
            memberId: '30000000-0000-0000-0000-000000000001',
            role: 'adult',
            canAdminister: true,
          },
        }, 201)
      }
      throw new Error(`Unexpected request: ${url}`)
    })
    vi.stubGlobal('fetch', fetchMock)
    renderApp()

    expect(await screen.findByRole('heading', { name: /welcome, Ryan Bamford/i })).toBeInTheDocument()
    await user.type(screen.getByLabelText('Household name'), 'Bamford-Fahie-Waltz Family')
    await user.click(screen.getByRole('button', { name: 'Create household' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Bamford-Fahie-Waltz Family' })).toBeInTheDocument()
    expect(fetchMock).toHaveBeenCalledWith(
      'http://localhost:8080/api/households',
      expect.objectContaining({ method: 'POST', credentials: 'include' }),
    )
  })

  it('lets an adult select between multiple households', async () => {
    const user = userEvent.setup()
    const multipleHouseholds = currentUser({
      selectedHouseholdId: null,
      households: [
        ...currentUser().households,
        {
          id: '20000000-0000-0000-0000-000000000002',
          name: 'Lake House Family',
          memberId: '30000000-0000-0000-0000-000000000002',
          role: 'adult',
        },
      ],
    })
    const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input)
      if (url.endsWith('/api/auth/me')) return jsonResponse(multipleHouseholds)
      if (url.endsWith('/api/auth/antiforgery')) {
        return jsonResponse({ requestToken: 'request-token', headerName: 'X-CSRF-TOKEN' })
      }
      if (url.endsWith('/api/auth/session/household')) {
        return jsonResponse({ selectedHouseholdId: '20000000-0000-0000-0000-000000000002' })
      }
      throw new Error(`Unexpected request: ${url}`)
    })
    vi.stubGlobal('fetch', fetchMock)
    renderApp()

    expect(await screen.findByRole('heading', { name: /which household/i })).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: /Lake House Family/i }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Lake House Family' })).toBeInTheDocument()
  })

  it('provides keyboard and pointer navigation for the ready dashboard', async () => {
    const user = userEvent.setup()
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const path = new URL(String(input)).pathname
      if (path === '/api/auth/me') return jsonResponse(currentUser())
      if (path.endsWith('/calendar/events')) return jsonResponse({
        events: [], nextCursor: null, isStale: false, warnings: [],
      })
      throw new Error(`Unexpected request: ${path}`)
    }))
    renderApp()
    const calendarLink = await screen.findByRole('link', { name: 'Calendar' })

    await user.tab()
    expect(screen.getByRole('link', { name: /skip to content/i })).toHaveFocus()
    await user.click(calendarLink)

    await waitFor(() => expect(screen.getByRole('link', { name: 'Calendar' })).toHaveAttribute('aria-current', 'page'))
    expect(screen.getByRole('link', { name: 'Home' })).not.toHaveAttribute('aria-current')
  })
})

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
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(jsonResponse(currentUser())))
    renderApp()

    expect(await screen.findByRole('heading', { level: 1, name: 'Bamford-Fahie-Waltz Family' })).toBeInTheDocument()
    expect(screen.getByRole('main')).toBeInTheDocument()
    expect(screen.getByRole('navigation', { name: /primary/i })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Today' })).toBeInTheDocument()
    expect(screen.getByText('Dentist appointment')).toBeInTheDocument()
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
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(jsonResponse(currentUser())))
    renderApp()
    const calendarLink = await screen.findByRole('link', { name: 'Calendar' })

    await user.tab()
    expect(screen.getByRole('link', { name: /skip to content/i })).toHaveFocus()
    await user.click(calendarLink)

    await waitFor(() => expect(calendarLink).toHaveAttribute('aria-current', 'location'))
    expect(screen.getByRole('link', { name: 'Home' })).not.toHaveAttribute('aria-current')
  })
})

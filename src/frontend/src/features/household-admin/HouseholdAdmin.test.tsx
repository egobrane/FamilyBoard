import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { App } from '../../app/App'
import { AuthenticationProvider } from '../authentication/AuthenticationContext'

const householdId = '20000000-0000-0000-0000-000000000001'
const adultMemberId = '30000000-0000-0000-0000-000000000001'

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

function authenticatedUser(householdName = 'Bamford-Fahie-Waltz Family') {
  return {
    user: {
      id: '10000000-0000-0000-0000-000000000001',
      displayName: 'Ryan Bamford',
      primaryEmail: 'ryan@example.test',
    },
    households: [{
      id: householdId,
      name: householdName,
      memberId: adultMemberId,
      role: 'adult',
    }],
    selectedHouseholdId: householdId,
    session: {
      expiresAt: '2026-08-28T20:05:38Z',
      isSharedDisplay: false,
      deviceLabel: null,
      administrativeElevationHouseholdId: null,
      administrativeElevationExpiresAt: null,
    },
  }
}

function household(name = 'Bamford-Fahie-Waltz Family') {
  return {
    id: householdId,
    name,
    timeZone: 'America/New_York',
    locale: 'en-US',
    weekStartsOn: 'Sunday',
    access: { memberId: adultMemberId, role: 'adult', canAdminister: true },
  }
}

function renderPath(path: string) {
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

describe('household administration', () => {
  it('saves regional settings and refreshes the authenticated household heading', async () => {
    const user = userEvent.setup()
    let savedName = 'Bamford-Fahie-Waltz Family'
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = new URL(String(input)).pathname
      if (path === '/api/auth/me') return jsonResponse(authenticatedUser(savedName))
      if (path === '/api/auth/antiforgery') {
        return jsonResponse({ requestToken: 'request-token', headerName: 'X-CSRF-TOKEN' })
      }
      if (path === `/api/households/${householdId}` && init?.method === 'PATCH') {
        const body = JSON.parse(String(init.body)) as { name: string }
        savedName = body.name
        return jsonResponse(household(savedName))
      }
      if (path === `/api/households/${householdId}`) return jsonResponse(household(savedName))
      throw new Error(`Unexpected request: ${path}`)
    })
    vi.stubGlobal('fetch', fetchMock)
    renderPath(`/households/${householdId}/settings`)

    expect(await screen.findByRole('heading', { name: 'Household settings' })).toBeInTheDocument()
    const name = screen.getByLabelText('Household name')
    await user.clear(name)
    await user.type(name, 'Updated Family')
    await user.selectOptions(screen.getByLabelText('Week starts on'), 'Monday')
    await user.click(screen.getByRole('button', { name: 'Save settings' }))

    expect(await screen.findByText('Household settings saved.')).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 1, name: 'Updated Family' })).toBeInTheDocument()
    expect(fetchMock).toHaveBeenCalledWith(
      `http://localhost:8080/api/households/${householdId}`,
      expect.objectContaining({
        method: 'PATCH',
        credentials: 'include',
        headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'request-token' }),
      }),
    )
  })

  it('adds a child profile and protects the current adult from deactivation', async () => {
    const user = userEvent.setup()
    const members = [{
      id: adultMemberId,
      displayName: 'Ryan Bamford',
      role: 'adult',
      avatarColor: 'mint',
      isActive: true,
    }]
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = new URL(String(input)).pathname
      if (path === '/api/auth/me') return jsonResponse(authenticatedUser())
      if (path === '/api/auth/antiforgery') {
        return jsonResponse({ requestToken: 'request-token', headerName: 'X-CSRF-TOKEN' })
      }
      if (path === `/api/households/${householdId}/members` && init?.method === 'POST') {
        const child = {
          id: '30000000-0000-0000-0000-000000000002',
          displayName: 'Zoey',
          role: 'child',
          avatarColor: 'coral',
          isActive: true,
        }
        members.push(child)
        return jsonResponse(child, 201)
      }
      if (path.endsWith('/30000000-0000-0000-0000-000000000002') && init?.method === 'PATCH') {
        const body = JSON.parse(String(init.body)) as { isActive: boolean }
        const child = members.find((member) => member.id.endsWith('0002'))!
        child.isActive = body.isActive
        return jsonResponse(child)
      }
      if (path === `/api/households/${householdId}/members`) return jsonResponse(members)
      throw new Error(`Unexpected request: ${path}`)
    })
    vi.stubGlobal('fetch', fetchMock)
    renderPath(`/households/${householdId}/members`)

    expect(await screen.findByRole('heading', { name: 'Household members' })).toBeInTheDocument()
    expect(screen.getByText('Adult · You')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Deactivate' })).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Add child' }))
    const dialog = screen.getByRole('dialog', { name: 'Add a child' })
    await user.type(screen.getByLabelText('Display name'), 'Zoey')
    await user.click(screen.getByLabelText('Coral'))
    await user.click(within(dialog).getByRole('button', { name: 'Add child' }))

    await waitFor(() => expect(dialog).not.toBeInTheDocument())
    expect(screen.getByRole('heading', { name: 'Zoey' })).toBeInTheDocument()
    expect(screen.getByText('Zoey was added.')).toBeInTheDocument()
    expect(fetchMock).toHaveBeenCalledWith(
      `http://localhost:8080/api/households/${householdId}/members`,
      expect.objectContaining({
        method: 'POST',
        credentials: 'include',
        body: JSON.stringify({ displayName: 'Zoey', avatarColor: 'coral' }),
      }),
    )

    await user.click(screen.getByRole('button', { name: 'Deactivate' }))
    const deactivateDialog = screen.getByRole('dialog', { name: 'Deactivate Zoey?' })
    await user.click(within(deactivateDialog).getByRole('button', { name: 'Deactivate profile' }))
    expect(await screen.findByText('Zoey is now inactive.')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Reactivate' }))
    const reactivateDialog = screen.getByRole('dialog', { name: 'Reactivate Zoey?' })
    await user.click(within(reactivateDialog).getByRole('button', { name: 'Reactivate profile' }))
    expect(await screen.findByText('Zoey is now active.')).toBeInTheDocument()
  })
})

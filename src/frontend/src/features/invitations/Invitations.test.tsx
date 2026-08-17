import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { App } from '../../app/App'
import { AuthenticationProvider } from '../authentication/AuthenticationContext'

const householdId = '20000000-0000-0000-0000-000000000001'

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
}

function userResponse(authenticated = true) {
  return authenticated ? {
    user: { id: '10000000-0000-0000-0000-000000000001', displayName: 'Ryan', primaryEmail: 'ryan@example.test' },
    households: [{ id: householdId, name: 'Test Family', memberId: '30000000-0000-0000-0000-000000000001', role: 'adult' }],
    selectedHouseholdId: householdId,
    session: { expiresAt: '2026-08-28T20:05:38Z', isSharedDisplay: false, deviceLabel: null, administrativeElevationHouseholdId: null, administrativeElevationExpiresAt: null },
  } : { title: 'Authentication is required.', status: 401, code: 'authentication_required' }
}

function renderPath(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <AuthenticationProvider><App /></AuthenticationProvider>
    </MemoryRouter>,
  )
}

afterEach(() => {
  vi.unstubAllGlobals()
  window.history.replaceState(null, '', '/')
})

describe('adult invitations', () => {
  it('creates a one-time copyable invitation link and lists only metadata', async () => {
    const user = userEvent.setup()
    const invitation = {
      id: '40000000-0000-0000-0000-000000000001', householdId,
      intendedEmail: 'adult@example.test', status: 'pending',
      createdAt: '2026-08-15T12:00:00Z', expiresAt: '2026-08-22T12:00:00Z',
      acceptedAt: null, revokedAt: null,
    }
    let invitations: typeof invitation[] = []
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = new URL(String(input)).pathname
      if (path === '/api/auth/me') return jsonResponse(userResponse())
      if (path === '/api/auth/antiforgery') return jsonResponse({ requestToken: 'csrf', headerName: 'X-CSRF-TOKEN' })
      if (path.endsWith('/invitations') && init?.method === 'POST') {
        invitations = [invitation]
        return jsonResponse({ invitation, token: 'a'.repeat(43) }, 201)
      }
      if (path.endsWith('/invitations')) return jsonResponse(invitations)
      throw new Error(`Unexpected request: ${path}`)
    })
    vi.stubGlobal('fetch', fetchMock)
    renderPath(`/households/${householdId}/invitations`)

    expect(await screen.findByRole('heading', { name: 'Invitation links' })).toBeInTheDocument()
    await user.type(screen.getByLabelText('Adult email address'), 'adult@example.test')
    await user.click(screen.getByRole('button', { name: 'Create invitation' }))

    expect(await screen.findByLabelText('Invitation link')).toHaveValue(
      `${window.location.origin}/invite#token=${'a'.repeat(43)}`,
    )
    expect(screen.getByText('adult@example.test')).toBeInTheDocument()
    expect(fetchMock).toHaveBeenCalledWith(
      `http://localhost:8080/api/households/${householdId}/invitations`,
      expect.objectContaining({ method: 'POST', credentials: 'include' }),
    )
  })

  it('removes the raw token from the address before showing the signed-out invitation', async () => {
    const token = 'b'.repeat(43)
    window.history.replaceState(null, '', `/invite#token=${token}`)
    const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      const path = new URL(String(input)).pathname
      if (path === '/api/auth/me') return jsonResponse(userResponse(false), 401)
      if (path === '/api/invitations/prepare') return jsonResponse({
        householdName: 'Inviting Family', intendedEmailMasked: 'a•••••@example.test',
        expiresAt: '2026-08-22T12:00:00Z',
      })
      throw new Error(`Unexpected request: ${path}`)
    })
    vi.stubGlobal('fetch', fetchMock)
    renderPath('/invite')

    expect(await screen.findByRole('heading', { name: 'Join Inviting Family' })).toBeInTheDocument()
    expect(window.location.hash).toBe('')
    expect(screen.getByRole('link', { name: /sign in with the invited/i })).toHaveAttribute(
      'href',
      'http://localhost:8080/api/auth/login/google?returnUrl=%2Finvite&chooseAccount=true',
    )
    await waitFor(() => expect(fetchMock).toHaveBeenCalledWith(
      'http://localhost:8080/api/invitations/prepare',
      expect.objectContaining({ method: 'POST', credentials: 'include', body: JSON.stringify({ token }) }),
    ))
  })
})

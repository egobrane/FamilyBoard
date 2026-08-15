import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  ApiError,
  createHousehold,
  getCurrentUser,
  googleLoginUrl,
  listHouseholdMembers,
  selectHousehold,
  updateHousehold,
} from './api'

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('API client', () => {
  it('always includes browser credentials for current-user requests', async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ households: [] }))
    vi.stubGlobal('fetch', fetchMock)

    await getCurrentUser()

    expect(fetchMock).toHaveBeenCalledWith(
      'http://localhost:8080/api/auth/me',
      expect.objectContaining({ credentials: 'include' }),
    )
  })

  it('fetches antiforgery material before unsafe household requests', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(jsonResponse({ requestToken: 'token-value', headerName: 'X-CSRF-TOKEN' }))
      .mockResolvedValueOnce(jsonResponse({ selectedHouseholdId: 'household-id' }))
    vi.stubGlobal('fetch', fetchMock)

    await selectHousehold('household-id')

    expect(fetchMock).toHaveBeenNthCalledWith(
      2,
      'http://localhost:8080/api/auth/session/household',
      expect.objectContaining({
        method: 'PUT',
        credentials: 'include',
        headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'token-value' }),
        body: JSON.stringify({ householdId: 'household-id' }),
      }),
    )
  })

  it('preserves validation ProblemDetails for setup forms', async () => {
    vi.stubGlobal('fetch', vi.fn()
      .mockResolvedValueOnce(jsonResponse({ requestToken: 'token-value', headerName: 'X-CSRF-TOKEN' }))
      .mockResolvedValueOnce(jsonResponse({
        title: 'One or more values are invalid.',
        status: 400,
        code: 'validation_failed',
        errors: { name: ['A value is required.'] },
      }, 400)))

    await expect(createHousehold({
      name: '',
      timeZone: 'UTC',
      locale: 'en-US',
      weekStartsOn: 'Sunday',
    })).rejects.toSatisfy((error: unknown) => error instanceof ApiError && error.status === 400)
  })

  it('uses credentialed reads and antiforgery-protected household administration writes', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(jsonResponse([]))
      .mockResolvedValueOnce(jsonResponse({ requestToken: 'token-value', headerName: 'X-CSRF-TOKEN' }))
      .mockResolvedValueOnce(jsonResponse({ id: 'household/id', name: 'Updated Family' }))
    vi.stubGlobal('fetch', fetchMock)

    await listHouseholdMembers('household/id')
    await updateHousehold('household/id', { name: 'Updated Family' })

    expect(fetchMock).toHaveBeenNthCalledWith(
      1,
      'http://localhost:8080/api/households/household%2Fid/members',
      expect.objectContaining({ credentials: 'include' }),
    )
    expect(fetchMock).toHaveBeenNthCalledWith(
      3,
      'http://localhost:8080/api/households/household%2Fid',
      expect.objectContaining({
        method: 'PATCH',
        credentials: 'include',
        headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'token-value' }),
        body: JSON.stringify({ name: 'Updated Family' }),
      }),
    )
  })

  it('only constructs Google login URLs with local return paths', () => {
    expect(googleLoginUrl('/households/select')).toContain('returnUrl=%2Fhouseholds%2Fselect')
    expect(googleLoginUrl('//evil.example')).toBe(
      'http://localhost:8080/api/auth/login/google?returnUrl=%2F',
    )
  })
})

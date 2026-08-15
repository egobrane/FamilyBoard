import { configuration } from './configuration'

export interface ApiProblem {
  type?: string
  title: string
  status: number
  detail?: string
  code?: string
  traceId?: string
  errors?: Record<string, string[]>
}

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly problem: ApiProblem,
  ) {
    super(problem.title)
    this.name = 'ApiError'
  }
}

export interface HouseholdSummary {
  id: string
  name: string
  memberId: string
  role: 'adult' | 'child'
}

export interface CurrentUser {
  user: {
    id: string
    displayName: string
    primaryEmail: string
  }
  households: HouseholdSummary[]
  selectedHouseholdId: string | null
  session: {
    expiresAt: string
    isSharedDisplay: boolean
    administrativeElevationExpiresAt: string | null
  } | null
}

export interface CreateHouseholdRequest {
  name: string
  timeZone: string
  locale: string
  weekStartsOn: string
}

export interface HouseholdResponse {
  id: string
  name: string
  timeZone: string
  locale: string
  weekStartsOn: string
  access: {
    memberId: string
    role: 'adult' | 'child'
    canAdminister: boolean
  }
}

export interface UpdateHouseholdRequest {
  name?: string
  timeZone?: string
  locale?: string
  weekStartsOn?: string
}

export interface HouseholdMemberResponse {
  id: string
  displayName: string
  role: 'adult' | 'child'
  avatarColor: string | null
  isActive: boolean
}

export interface CreateChildMemberRequest {
  displayName: string
  avatarColor: string | null
}

export interface UpdateHouseholdMemberRequest {
  displayName?: string
  avatarColor?: string
  isActive?: boolean
}

interface AntiforgeryTokenResponse {
  requestToken: string
  headerName: string
}

interface SelectedHouseholdResponse {
  selectedHouseholdId: string
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${configuration.apiBaseUrl}${path}`, {
    ...init,
    credentials: 'include',
    headers: {
      Accept: 'application/json',
      ...init?.headers,
    },
  })

  if (!response.ok) {
    let problem: ApiProblem
    try {
      problem = await response.json() as ApiProblem
    } catch {
      problem = {
        title: 'The request could not be completed.',
        status: response.status,
      }
    }
    throw new ApiError(response.status, problem)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return await response.json() as T
}

async function unsafeRequest<T>(
  path: string,
  method: 'POST' | 'PUT' | 'PATCH',
  body?: unknown,
): Promise<T> {
  const antiforgery = await request<AntiforgeryTokenResponse>('/api/auth/antiforgery')
  return await request<T>(path, {
    method,
    headers: {
      'Content-Type': 'application/json',
      [antiforgery.headerName]: antiforgery.requestToken,
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
}

export function getCurrentUser() {
  return request<CurrentUser>('/api/auth/me')
}

export function createHousehold(body: CreateHouseholdRequest) {
  return unsafeRequest<HouseholdResponse>('/api/households', 'POST', body)
}

export function getHousehold(householdId: string) {
  return request<HouseholdResponse>(`/api/households/${encodeURIComponent(householdId)}`)
}

export function updateHousehold(householdId: string, body: UpdateHouseholdRequest) {
  return unsafeRequest<HouseholdResponse>(
    `/api/households/${encodeURIComponent(householdId)}`,
    'PATCH',
    body,
  )
}

export function listHouseholdMembers(householdId: string) {
  return request<HouseholdMemberResponse[]>(
    `/api/households/${encodeURIComponent(householdId)}/members`,
  )
}

export function createChildMember(householdId: string, body: CreateChildMemberRequest) {
  return unsafeRequest<HouseholdMemberResponse>(
    `/api/households/${encodeURIComponent(householdId)}/members`,
    'POST',
    body,
  )
}

export function updateHouseholdMember(
  householdId: string,
  memberId: string,
  body: UpdateHouseholdMemberRequest,
) {
  return unsafeRequest<HouseholdMemberResponse>(
    `/api/households/${encodeURIComponent(householdId)}/members/${encodeURIComponent(memberId)}`,
    'PATCH',
    body,
  )
}

export function selectHousehold(householdId: string) {
  return unsafeRequest<SelectedHouseholdResponse>(
    '/api/auth/session/household',
    'PUT',
    { householdId },
  )
}

export function logout() {
  return unsafeRequest<void>('/api/auth/logout', 'POST')
}

export function googleLoginUrl(returnUrl = '/') {
  const safeReturnUrl = returnUrl.startsWith('/') && !returnUrl.startsWith('//')
    ? returnUrl
    : '/'
  return `${configuration.apiBaseUrl}/api/auth/login/google?returnUrl=${encodeURIComponent(safeReturnUrl)}`
}

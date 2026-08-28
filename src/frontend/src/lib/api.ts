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
    deviceLabel: string | null
    administrativeElevationHouseholdId: string | null
    administrativeElevationExpiresAt: string | null
  } | null
}

export interface ParentAccessState {
  householdId: string
  isPinConfigured: boolean
  pinLength: number
  isSharedDisplay: boolean
  isElevated: boolean
  elevationExpiresAt: string | null
  lockedUntil: string | null
}

export interface CalendarConnectionResponse {
  isAvailable: boolean
  connectionId: string | null
  status: 'connected' | 'reauthorizationRequired' | 'disconnected'
  providerEmail: string | null
  connectedAt: string | null
  canManageConnection: boolean
  activeSourceCount: number
  eventCreationAvailable: boolean
  eventCreationAuthorized: boolean
}

export interface ProviderCalendarResponse {
  id: string
  name: string
  timeZone: string | null
  color: string | null
  isPrimary: boolean
  isSelected: boolean
  accessRole: string
  canCreateEvents: boolean
  isEventCreationTarget: boolean
}

export interface CalendarSourceResponse {
  id: string
  connectionId: string
  externalCalendarId: string
  name: string
  timeZone: string | null
  color: string | null
  isActive: boolean
  isOwnedByCurrentAdult: boolean
  isEventCreationTarget: boolean
}

export interface CalendarEventCreationTargetResponse {
  isAvailable: boolean
  isAuthorized: boolean
  sourceId: string | null
  name: string | null
  timeZone: string | null
  color: string | null
}

export interface CreateCalendarEventRequest {
  sourceId: string
  idempotencyKey: string
  attributedMemberId: string | null
  title: string
  location: string | null
  notes: string | null
  isAllDay: boolean
  start: string
  end: string
  timeZone: string | null
}

export interface CreatedCalendarEventResponse extends CalendarEventResponse {
  attributedMemberId: string
  recoveredExistingEvent: boolean
}

export interface CalendarEventResponse {
  id: string
  sourceId: string
  calendarName: string
  title: string
  isAllDay: boolean
  start: string
  end: string
  timeZone: string | null
  location: string | null
  color: string | null
  canEdit?: boolean
  canDelete?: boolean
  managementId?: string | null
  providerVersion?: string | null
  managementUnavailableReason?: string | null
}

export interface ManagedCalendarEventResponse {
  managementId: string
  sourceId: string
  calendarName: string
  title: string
  location: string | null
  notes: string | null
  isAllDay: boolean
  start: string
  end: string
  timeZone: string | null
  providerVersion: string
  canEdit: boolean
  canDelete: boolean
  managementUnavailableReason: string | null
}

export interface CalendarEventMutationResponse {
  operation: 'update' | 'delete'
  completedAt: string
  recoveredExistingMutation: boolean
  event: ManagedCalendarEventResponse | null
}

export interface CalendarEventsResponse {
  events: CalendarEventResponse[]
  nextCursor: string | null
  isStale: boolean
  warnings: { sourceId: string; code: string; message: string }[]
}

export interface TasksConnectionResponse {
  isAvailable: boolean
  connectionId: string | null
  status: 'active' | 'reauthorizationRequired' | 'disconnected'
  providerEmail: string | null
  connectedAt: string | null
  activeSourceCount: number
  activeHouseholdCount: number
  canRead: boolean
  canWrite: boolean
  writeAuthorizationRequired: boolean
  mutationsAvailable: boolean
}

export interface ProviderTaskListResponse {
  id: string
  name: string
  isSelected: boolean
  canWrite: boolean
  isWriteTarget: boolean
}

export interface TaskListSourceResponse {
  id: string
  connectionId: string
  externalTaskListId: string
  name: string
  isActive: boolean
  isOwnedByCurrentAdult: boolean
  canWrite: boolean
  isWriteTarget: boolean
}

export interface GoogleTaskResponse {
  id: string
  sourceId: string
  taskListName: string
  title: string
  notes: string | null
  status: string
  dueDate: string | null
  completedAt: string | null
  parentTaskId: string | null
  position: string
  isSubtask: boolean
  isAssigned: boolean
  canChangeStatus: boolean
  mutationVersion: string | null
}

export interface GoogleTaskMutationResponse {
  operation: 'create' | 'complete' | 'reopen'
  taskId: string
  sourceId: string
  status: string
  dueDate: string | null
  mutationVersion: string
  attributedMemberId: string
  recoveredExistingMutation: boolean
}

export interface GoogleTasksResponse {
  tasks: GoogleTaskResponse[]
  nextCursor: string | null
  isStale: boolean
  warnings: { sourceId: string; code: string; message: string }[]
  canCreateTasks: boolean
}

export type CurrentSession = NonNullable<CurrentUser['session']>

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

export interface ChoreParticipantResponse {
  id: string
  displayName: string
  role: 'adult' | 'child'
  avatarColor: string | null
}

export interface ChoreCompletionResponse {
  id: string
  assignmentId: string
  completedByMember: ChoreParticipantResponse
  status: 'pendingReview' | 'approved' | 'rejected'
  wasSharedDisplay: boolean
  pointValue: number
  completedAt: string
  reviewedByMember: ChoreParticipantResponse | null
  reviewedAt: string | null
  reviewNote: string | null
  version: number
  award: { transactionId: string; amount: number } | null
}

export interface ChoreAssignmentResponse {
  id: string
  choreDefinitionId: string
  title: string
  description: string | null
  pointValue: number
  assignedMember: ChoreParticipantResponse
  dueLocalDate: string | null
  dueLocalTime: string | null
  dueAt: string | null
  dueTimeZone: string | null
  dueHasExplicitTime: boolean
  status: 'pending' | 'awaitingReview' | 'completed' | 'skipped'
  isOverdue: boolean
  version: number
  pendingCompletion: ChoreCompletionResponse | null
  createdAt: string
  updatedAt: string
}

export interface ChoreDashboardResponse {
  overdue: ChoreAssignmentResponse[]
  dueToday: ChoreAssignmentResponse[]
  upcoming: ChoreAssignmentResponse[]
  awaitingReviewCount: number
}

export interface ChoreDefinitionResponse {
  id: string
  title: string
  description: string | null
  defaultPointValue: number
  isActive: boolean
  version: number
  createdAt: string
  updatedAt: string
}

export interface PointMemberResponse {
  id: string
  displayName: string
  role: 'adult' | 'child'
  avatarColor: string | null
  isActive: boolean
}

export interface PointMemberBalanceResponse {
  memberId: string
  displayName: string
  role: 'adult' | 'child'
  avatarColor: string | null
  isActive: boolean
  balance: number
}

export interface PointTransactionResponse {
  id: string
  householdMember: PointMemberResponse
  amount: number
  type: 'choreCompletion' | 'rewardRedemption' | 'adjustment' | 'reversal'
  description: string
  choreCompletionId: string | null
  rewardRedemptionId: string | null
  reversesPointTransactionId: string | null
  createdByMember: PointMemberResponse | null
  createdAt: string
  isReversed: boolean
}

export interface RewardResponse {
  id: string; title: string; description: string | null; pointCost: number
  isActive: boolean; version: number; createdAt: string; updatedAt: string
}
export interface RewardRedemptionResponse {
  id: string; rewardId: string; rewardTitle: string; rewardDescription: string | null
  pointCost: number; householdMember: PointMemberResponse
  status: 'requested' | 'approved' | 'fulfilled' | 'rejected' | 'cancelled'
  requestedByMember: PointMemberResponse | null; wasSharedDisplay: boolean; requestedAt: string
  reviewedByMember: PointMemberResponse | null; reviewedAt: string | null; reviewNote: string | null
  fulfilledByMember: PointMemberResponse | null; fulfilledAt: string | null
  cancelledByMember: PointMemberResponse | null; cancelledAt: string | null
  cancellationReason: string | null; version: number
}
export interface RewardCatalogResponse { rewards: RewardResponse[]; members: PointMemberBalanceResponse[] }
export interface RewardRedemptionListResponse { items: RewardRedemptionResponse[]; nextCursor: string | null }

export interface HouseholdPointSummaryResponse {
  householdBalance: number
  members: PointMemberBalanceResponse[]
  recentTransactions: PointTransactionResponse[]
}

export interface PointTransactionListResponse {
  items: PointTransactionResponse[]
  nextCursor: string | null
}

export interface ChoreRecurrenceRequest {
  kind: 'daily' | 'weekly'
  interval: number
  daysOfWeek: string[]
}

export interface ChoreScheduleResponse {
  id: string
  definition: ChoreDefinitionResponse
  assignedMember: ChoreParticipantResponse
  recurrence: ChoreRecurrenceRequest
  startLocalDate: string
  endLocalDate: string | null
  dueLocalTime: string | null
  timeZone: string
  status: 'active' | 'paused' | 'blocked' | 'completed'
  blockedReason: string | null
  nextOccurrenceLocalDate: string | null
  lastGeneratedOccurrenceLocalDate: string | null
  lastEvaluatedAt: string | null
  version: number
  createdAt: string
  updatedAt: string
}

export interface ChoreScheduleWriteRequest {
  choreDefinitionId: string
  assignedMemberId: string
  recurrence: ChoreRecurrenceRequest
  startLocalDate: string
  endLocalDate: string | null
  dueLocalTime: string | null
}

export type InvitationStatus = 'pending' | 'accepted' | 'revoked' | 'expired'

export interface HouseholdInvitationResponse {
  id: string
  householdId: string
  intendedEmail: string
  status: InvitationStatus
  createdAt: string
  expiresAt: string
  acceptedAt: string | null
  revokedAt: string | null
}

export interface CreatedInvitationResponse {
  invitation: HouseholdInvitationResponse
  token: string
}

export interface PendingInvitationResponse {
  householdName: string
  intendedEmailMasked: string
  expiresAt: string
}

export interface AcceptedInvitationResponse {
  household: HouseholdSummary
  selectedHouseholdId: string
  reusedExistingMembership: boolean
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

function publicJsonRequest<T>(path: string, body: unknown) {
  return request<T>(path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
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

export function getChoreDashboard(householdId: string) {
  return request<ChoreDashboardResponse>(`/api/households/${encodeURIComponent(householdId)}/chores/dashboard`)
}

export function listChoreParticipants(householdId: string) {
  return request<ChoreParticipantResponse[]>(`/api/households/${encodeURIComponent(householdId)}/chores/participants`)
}

export function listChoreAssignments(householdId: string, view: 'active' | 'history' = 'active') {
  return request<{ items: ChoreAssignmentResponse[]; nextCursor: string | null }>(
    `/api/households/${encodeURIComponent(householdId)}/chore-assignments?view=${view}`,
  )
}

export function completeChore(householdId: string, assignmentId: string, body: {
  clientRequestId: string
  expectedAssignmentVersion: number
  completedByMemberId: string | null
}) {
  return unsafeRequest<ChoreCompletionResponse>(
    `/api/households/${encodeURIComponent(householdId)}/chore-assignments/${encodeURIComponent(assignmentId)}/completions`,
    'POST', body,
  )
}

export function listChoreDefinitions(householdId: string, includeInactive = true) {
  return request<ChoreDefinitionResponse[]>(
    `/api/households/${encodeURIComponent(householdId)}/chore-definitions?includeInactive=${includeInactive}`,
  )
}

export function createChoreDefinition(householdId: string, body: {
  clientRequestId: string; title: string; description: string | null; defaultPointValue: number
}) {
  return unsafeRequest<ChoreDefinitionResponse>(
    `/api/households/${encodeURIComponent(householdId)}/chore-definitions`, 'POST', body,
  )
}

export function updateChoreDefinition(householdId: string, definitionId: string, body: {
  expectedVersion: number; title: string; description: string | null; defaultPointValue: number
}) {
  return unsafeRequest<ChoreDefinitionResponse>(
    `/api/households/${encodeURIComponent(householdId)}/chore-definitions/${encodeURIComponent(definitionId)}`,
    'PATCH', body,
  )
}

export function setChoreDefinitionActive(householdId: string, definition: ChoreDefinitionResponse, active: boolean) {
  return unsafeRequest<ChoreDefinitionResponse>(
    `/api/households/${encodeURIComponent(householdId)}/chore-definitions/${encodeURIComponent(definition.id)}/${active ? 'activate' : 'deactivate'}`,
    'POST', { expectedVersion: definition.version },
  )
}

export function createChoreAssignment(householdId: string, body: {
  clientRequestId: string
  choreDefinitionId: string
  assignedMemberId: string
  dueLocalDate: string
  dueLocalTime: string | null
}) {
  return unsafeRequest<ChoreAssignmentResponse>(
    `/api/households/${encodeURIComponent(householdId)}/chore-assignments`, 'POST', body,
  )
}

export function skipChoreAssignment(householdId: string, assignment: ChoreAssignmentResponse, reason: string | null) {
  return unsafeRequest<ChoreAssignmentResponse>(
    `/api/households/${encodeURIComponent(householdId)}/chore-assignments/${encodeURIComponent(assignment.id)}/skip`,
    'POST', { expectedVersion: assignment.version, reason },
  )
}

export function listPendingChoreReviews(householdId: string) {
  return request<ChoreCompletionResponse[]>(
    `/api/households/${encodeURIComponent(householdId)}/chore-completions?status=pendingReview`,
  )
}

export function reviewChoreCompletion(householdId: string, completion: ChoreCompletionResponse,
  decision: 'approved' | 'rejected', note: string | null) {
  return unsafeRequest<ChoreCompletionResponse>(
    `/api/households/${encodeURIComponent(householdId)}/chore-completions/${encodeURIComponent(completion.id)}/review`,
    'POST', { expectedVersion: completion.version, decision, note },
  )
}

export function listChoreSchedules(householdId: string, includeInactive = true) {
  return request<ChoreScheduleResponse[]>(
    `/api/households/${encodeURIComponent(householdId)}/chore-schedules?includeInactive=${includeInactive}`,
  )
}

export function createChoreSchedule(householdId: string, body: ChoreScheduleWriteRequest) {
  return unsafeRequest<ChoreScheduleResponse>(
    `/api/households/${encodeURIComponent(householdId)}/chore-schedules`,
    'POST', { ...body, clientRequestId: crypto.randomUUID() },
  )
}

export function updateChoreSchedule(householdId: string, schedule: ChoreScheduleResponse,
  body: ChoreScheduleWriteRequest) {
  return unsafeRequest<ChoreScheduleResponse>(
    `/api/households/${encodeURIComponent(householdId)}/chore-schedules/${encodeURIComponent(schedule.id)}`,
    'PATCH', { ...body, expectedVersion: schedule.version },
  )
}

export function setChoreScheduleActive(householdId: string, schedule: ChoreScheduleResponse,
  active: boolean) {
  return unsafeRequest<ChoreScheduleResponse>(
    `/api/households/${encodeURIComponent(householdId)}/chore-schedules/${encodeURIComponent(schedule.id)}/${active ? 'resume' : 'pause'}`,
    'POST', { expectedVersion: schedule.version },
  )
}

export function getPointSummary(householdId: string) {
  return request<HouseholdPointSummaryResponse>(
    `/api/households/${encodeURIComponent(householdId)}/points/summary`,
  )
}

export function listPointTransactions(householdId: string, memberId?: string, cursor?: string) {
  const parameters = new URLSearchParams()
  if (memberId) parameters.set('memberId', memberId)
  if (cursor) parameters.set('cursor', cursor)
  const query = parameters.size > 0 ? `?${parameters.toString()}` : ''
  return request<PointTransactionListResponse>(
    `/api/households/${encodeURIComponent(householdId)}/point-transactions${query}`,
  )
}

export function createPointAdjustment(householdId: string, body: {
  clientRequestId: string
  householdMemberId: string
  amount: number
  reason: string
}) {
  return unsafeRequest<PointTransactionResponse>(
    `/api/households/${encodeURIComponent(householdId)}/point-adjustments`, 'POST', body,
  )
}

export function reversePointTransaction(householdId: string, transactionId: string,
  clientRequestId: string, reason: string) {
  return unsafeRequest<PointTransactionResponse>(
    `/api/households/${encodeURIComponent(householdId)}/point-transactions/${encodeURIComponent(transactionId)}/reverse`,
    'POST', { clientRequestId, reason },
  )
}

export function getRewardCatalog(householdId: string) {
  return request<RewardCatalogResponse>(`/api/households/${encodeURIComponent(householdId)}/rewards`)
}
export function listRewardDefinitions(householdId: string) {
  return request<RewardResponse[]>(`/api/households/${encodeURIComponent(householdId)}/reward-definitions`)
}
export function createRewardDefinition(householdId: string, body: {
  clientRequestId: string; title: string; description: string | null; pointCost: number
}) { return unsafeRequest<RewardResponse>(`/api/households/${encodeURIComponent(householdId)}/reward-definitions`, 'POST', body) }
export function updateRewardDefinition(householdId: string, reward: RewardResponse, body: {
  title: string; description: string | null; pointCost: number
}) { return unsafeRequest<RewardResponse>(`/api/households/${encodeURIComponent(householdId)}/reward-definitions/${encodeURIComponent(reward.id)}`,
  'PATCH', { ...body, expectedVersion: reward.version }) }
export function setRewardActive(householdId: string, reward: RewardResponse, active: boolean) {
  return unsafeRequest<RewardResponse>(`/api/households/${encodeURIComponent(householdId)}/reward-definitions/${encodeURIComponent(reward.id)}/${active ? 'activate' : 'deactivate'}`,
    'POST', { expectedVersion: reward.version })
}
export function requestRewardRedemption(householdId: string, rewardId: string,
  householdMemberId: string | null, clientRequestId: string) {
  return unsafeRequest<RewardRedemptionResponse>(`/api/households/${encodeURIComponent(householdId)}/reward-redemptions`,
    'POST', { clientRequestId, rewardId, householdMemberId })
}
export function listRewardRedemptions(householdId: string, status?: string) {
  const query = status ? `?status=${encodeURIComponent(status)}` : ''
  return request<RewardRedemptionListResponse>(`/api/households/${encodeURIComponent(householdId)}/reward-redemptions${query}`)
}
export function reviewRewardRedemption(householdId: string, item: RewardRedemptionResponse,
  decision: 'approved' | 'rejected', note: string | null) {
  return unsafeRequest<RewardRedemptionResponse>(`/api/households/${encodeURIComponent(householdId)}/reward-redemptions/${encodeURIComponent(item.id)}/review`,
    'POST', { expectedVersion: item.version, decision, note })
}
export function fulfillRewardRedemption(householdId: string, item: RewardRedemptionResponse) {
  return unsafeRequest<RewardRedemptionResponse>(`/api/households/${encodeURIComponent(householdId)}/reward-redemptions/${encodeURIComponent(item.id)}/fulfill`,
    'POST', { expectedVersion: item.version })
}
export function cancelRewardRedemption(householdId: string, item: RewardRedemptionResponse, reason: string) {
  return unsafeRequest<RewardRedemptionResponse>(`/api/households/${encodeURIComponent(householdId)}/reward-redemptions/${encodeURIComponent(item.id)}/cancel`,
    'POST', { expectedVersion: item.version, reason })
}

export function listHouseholdInvitations(householdId: string) {
  return request<HouseholdInvitationResponse[]>(
    `/api/households/${encodeURIComponent(householdId)}/invitations`,
  )
}

export function createHouseholdInvitation(householdId: string, intendedEmail: string) {
  return unsafeRequest<CreatedInvitationResponse>(
    `/api/households/${encodeURIComponent(householdId)}/invitations`,
    'POST',
    { intendedEmail },
  )
}

export function revokeHouseholdInvitation(householdId: string, invitationId: string) {
  return unsafeRequest<HouseholdInvitationResponse>(
    `/api/households/${encodeURIComponent(householdId)}/invitations/${encodeURIComponent(invitationId)}/revoke`,
    'POST',
  )
}

export function prepareInvitation(token: string) {
  return publicJsonRequest<PendingInvitationResponse>('/api/invitations/prepare', { token })
}

export function getPendingInvitation() {
  return request<PendingInvitationResponse>('/api/invitations/pending')
}

export function acceptPendingInvitation() {
  return unsafeRequest<AcceptedInvitationResponse>('/api/invitations/pending/accept', 'POST')
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

export function getParentAccessState(householdId: string) {
  return request<ParentAccessState>(
    `/api/households/${encodeURIComponent(householdId)}/parent-access`,
  )
}

export function setParentPin(householdId: string, pin: string) {
  return unsafeRequest<ParentAccessState>(
    `/api/households/${encodeURIComponent(householdId)}/parent-access/pin`,
    'PUT',
    { pin },
  )
}

export function recoverParentPin(householdId: string, pin: string) {
  return unsafeRequest<ParentAccessState>(
    `/api/households/${encodeURIComponent(householdId)}/parent-access/pin/recover`,
    'POST',
    { pin },
  )
}

export function verifyParentPin(householdId: string, pin: string) {
  return unsafeRequest<ParentAccessState>(
    `/api/households/${encodeURIComponent(householdId)}/parent-access/verify`,
    'POST',
    { pin },
  )
}

export function lockParentAccess(householdId: string) {
  return unsafeRequest<void>(
    `/api/households/${encodeURIComponent(householdId)}/parent-access/lock`,
    'POST',
  )
}

export function updateSharedDisplay(
  householdId: string,
  isSharedDisplay: boolean,
  deviceLabel?: string,
) {
  return unsafeRequest<CurrentSession>('/api/auth/session/shared-display', 'PUT', {
    householdId,
    isSharedDisplay,
    deviceLabel,
  })
}

export function getCalendarConnection(householdId: string) {
  return request<CalendarConnectionResponse>(
    `/api/households/${encodeURIComponent(householdId)}/calendar/connection`,
  )
}

export function beginCalendarAuthorization(
  householdId: string,
  returnPath: string,
  capability: 'readOnly' | 'eventCreation' = 'readOnly',
) {
  return unsafeRequest<{ authorizationUrl: string; expiresAt: string }>(
    `/api/households/${encodeURIComponent(householdId)}/calendar/authorization`,
    'POST',
    { returnPath, capability },
  )
}

export function listProviderCalendars(householdId: string) {
  return request<ProviderCalendarResponse[]>(
    `/api/households/${encodeURIComponent(householdId)}/calendar/provider-calendars`,
  )
}

export function listCalendarSources(householdId: string) {
  return request<CalendarSourceResponse[]>(
    `/api/households/${encodeURIComponent(householdId)}/calendar/sources`,
  )
}

export function updateCalendarSources(
  householdId: string,
  connectionId: string,
  externalCalendarIds: string[],
) {
  return unsafeRequest<CalendarSourceResponse[]>(
    `/api/households/${encodeURIComponent(householdId)}/calendar/sources`,
    'PUT',
    { connectionId, externalCalendarIds },
  )
}

export function getCalendarEventCreationTarget(householdId: string) {
  return request<CalendarEventCreationTargetResponse>(
    `/api/households/${encodeURIComponent(householdId)}/calendar/event-creation-target`,
  )
}

export function updateCalendarEventCreationTarget(
  householdId: string,
  sourceId: string | null,
) {
  return unsafeRequest<CalendarEventCreationTargetResponse>(
    `/api/households/${encodeURIComponent(householdId)}/calendar/event-creation-target`,
    'PUT',
    { sourceId },
  )
}

export function createCalendarEvent(
  householdId: string,
  body: CreateCalendarEventRequest,
) {
  return unsafeRequest<CreatedCalendarEventResponse>(
    `/api/households/${encodeURIComponent(householdId)}/calendar/events`,
    'POST',
    body,
  )
}

export function getManagedCalendarEvent(householdId: string, managementId: string) {
  return request<ManagedCalendarEventResponse>(
    `/api/households/${encodeURIComponent(householdId)}/calendar/managed-events/${encodeURIComponent(managementId)}`,
  )
}

export function updateManagedCalendarEvent(
  householdId: string,
  managementId: string,
  body: {
    idempotencyKey: string
    expectedProviderVersion: string
    title: string
    location: string | null
    notes: string | null
    isAllDay: boolean
    start: string
    end: string
    timeZone: string | null
  },
) {
  return unsafeRequest<CalendarEventMutationResponse>(
    `/api/households/${encodeURIComponent(householdId)}/calendar/managed-events/${encodeURIComponent(managementId)}`,
    'PUT', body,
  )
}

export function deleteManagedCalendarEvent(
  householdId: string,
  managementId: string,
  body: { idempotencyKey: string; expectedProviderVersion: string; confirmDelete: boolean },
) {
  return unsafeRequest<CalendarEventMutationResponse>(
    `/api/households/${encodeURIComponent(householdId)}/calendar/managed-events/${encodeURIComponent(managementId)}/delete`,
    'POST', body,
  )
}

export function disconnectCalendar(householdId: string, connectionId: string) {
  return unsafeRequest<void>(
    `/api/households/${encodeURIComponent(householdId)}/calendar/disconnect`,
    'POST',
    { connectionId, confirmGlobalDisconnect: true },
  )
}

export function listCalendarEvents(
  householdId: string,
  from: string,
  to: string,
  cursor?: string,
) {
  const parameters = new URLSearchParams({ from, to })
  if (cursor) parameters.set('cursor', cursor)
  return request<CalendarEventsResponse>(
    `/api/households/${encodeURIComponent(householdId)}/calendar/events?${parameters.toString()}`,
  )
}

export function getTasksConnection(householdId: string) {
  return request<TasksConnectionResponse>(
    `/api/households/${encodeURIComponent(householdId)}/tasks/connection`,
  )
}

export function beginTasksAuthorization(householdId: string, returnPath: string, capability = 'read') {
  return unsafeRequest<{ authorizationUrl: string; expiresAt: string }>(
    `/api/households/${encodeURIComponent(householdId)}/tasks/authorization`,
    'POST', { returnPath, capability },
  )
}

export function listProviderTaskLists(householdId: string) {
  return request<ProviderTaskListResponse[]>(
    `/api/households/${encodeURIComponent(householdId)}/tasks/provider-task-lists`,
  )
}

export function listTaskListSources(householdId: string) {
  return request<TaskListSourceResponse[]>(
    `/api/households/${encodeURIComponent(householdId)}/tasks/sources`,
  )
}

export function updateTaskListSources(householdId: string, connectionId: string, externalTaskListIds: string[]) {
  return unsafeRequest<TaskListSourceResponse[]>(
    `/api/households/${encodeURIComponent(householdId)}/tasks/sources`,
    'PUT', { connectionId, externalTaskListIds },
  )
}

export function updateTaskWriteTarget(householdId: string, sourceId: string | null) {
  return unsafeRequest<{ isAvailable: boolean; isAuthorized: boolean; sourceId: string | null; name: string | null }>(
    `/api/households/${encodeURIComponent(householdId)}/tasks/write-target`,
    'PUT', { sourceId },
  )
}

export function disconnectTasks(householdId: string, connectionId: string) {
  return unsafeRequest<void>(
    `/api/households/${encodeURIComponent(householdId)}/tasks/disconnect`,
    'POST', { connectionId, confirmGlobalDisconnect: true },
  )
}

export function listGoogleTasks(householdId: string, includeCompleted = false, cursor?: string) {
  const parameters = new URLSearchParams({ includeCompleted: String(includeCompleted) })
  if (cursor) parameters.set('cursor', cursor)
  return request<GoogleTasksResponse>(
    `/api/households/${encodeURIComponent(householdId)}/tasks?${parameters.toString()}`,
  )
}

export function createGoogleTask(householdId: string, body: {
  idempotencyKey: string; attributedMemberId: string | null; title: string; notes: string | null; dueDate: string | null
}) {
  return unsafeRequest<GoogleTaskMutationResponse>(
    `/api/households/${encodeURIComponent(householdId)}/tasks`, 'POST', body,
  )
}

export function updateGoogleTaskStatus(householdId: string, body: {
  sourceId: string; taskId: string; idempotencyKey: string; attributedMemberId: string | null
  targetStatus: 'completed' | 'needsAction'; mutationVersion: string
}) {
  return unsafeRequest<GoogleTaskMutationResponse>(
    `/api/households/${encodeURIComponent(householdId)}/tasks/status`, 'PUT', body,
  )
}

export function googleLoginUrl(returnUrl = '/', chooseAccount = false) {
  const safeReturnUrl = returnUrl.startsWith('/') && !returnUrl.startsWith('//')
    ? returnUrl
    : '/'
  const parameters = new URLSearchParams({ returnUrl: safeReturnUrl })
  if (chooseAccount) parameters.set('chooseAccount', 'true')
  return `${configuration.apiBaseUrl}/api/auth/login/google?${parameters.toString()}`
}

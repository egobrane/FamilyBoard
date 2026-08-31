import AxeBuilder from '@axe-core/playwright'
import { expect, test } from '@playwright/test'

const authenticatedUser = {
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
}

let householdName = authenticatedUser.households[0].name
let currentSession = { ...authenticatedUser.session }
let parentAccess = {
  householdId: authenticatedUser.households[0].id,
  isPinConfigured: true,
  pinLength: 6,
  isSharedDisplay: false,
  isElevated: false,
  elevationExpiresAt: null as string | null,
  lockedUntil: null as string | null,
}
let householdMembers: Array<{
  id: string
  displayName: string
  role: 'adult' | 'child'
  avatarColor: string | null
  isActive: boolean
}> = []
let householdInvitations: Array<{
  id: string
  householdId: string
  intendedEmail: string
  status: 'pending' | 'revoked'
  createdAt: string
  expiresAt: string
  acceptedAt: null
  revokedAt: string | null
}> = []
let choreSchedules: Array<Record<string, unknown>> = []
let rewardRedemptions: Array<Record<string, unknown>> = []
let dashboardAppearance = {
  householdId: authenticatedUser.households[0].id,
  timeZone: 'America/New_York',
  greetingTitle: null as string | null,
  greetingMessage: null as string | null,
  photoFocalX: 0.5,
  photoFocalY: 0.4,
  version: 1,
  photo: null,
}
let weatherSettings: Record<string, unknown> | undefined

test.beforeEach(async ({ page }) => {
  householdName = authenticatedUser.households[0].name
  currentSession = { ...authenticatedUser.session }
  parentAccess = { ...parentAccess, isSharedDisplay: false, isElevated: false, elevationExpiresAt: null, lockedUntil: null }
  householdMembers = [{
    id: authenticatedUser.households[0].memberId,
    displayName: authenticatedUser.user.displayName,
    role: 'adult',
    avatarColor: 'mint',
    isActive: true,
  }]
  householdInvitations = []
  choreSchedules = []
  rewardRedemptions = []
  dashboardAppearance = { ...dashboardAppearance, greetingTitle: null, greetingMessage: null, version: 1 }
  weatherSettings = undefined
  await page.route('http://localhost:8080/api/**', async (route) => {
    const url = new URL(route.request().url())
    if (url.pathname === '/api/auth/me') {
      await route.fulfill({
        json: {
          ...authenticatedUser,
          session: currentSession,
          households: [{ ...authenticatedUser.households[0], name: householdName }],
        },
      })
      return
    }
    if (url.pathname === '/api/auth/antiforgery') {
      await route.fulfill({
        json: { requestToken: 'e2e-request-token', headerName: 'X-CSRF-TOKEN' },
      })
      return
    }
    if (url.pathname === '/api/auth/logout') {
      await route.fulfill({ status: 204 })
      return
    }
    if (url.pathname === `/api/households/${authenticatedUser.households[0].id}/dashboard-appearance`) {
      if (route.request().method() === 'PUT') {
        const body = route.request().postDataJSON() as { greetingTitle: string | null; greetingMessage: string | null; photoFocalX: number; photoFocalY: number }
        dashboardAppearance = { ...dashboardAppearance, ...body, version: dashboardAppearance.version + 1 }
      }
      await route.fulfill({ json: dashboardAppearance })
      return
    }
    if (url.pathname === `/api/households/${authenticatedUser.households[0].id}/weather-settings`) {
      if (route.request().method() === 'PUT') {
        const body = route.request().postDataJSON() as Record<string, unknown>
        weatherSettings = { householdId: authenticatedUser.households[0].id, ...body, version: 2 }
        await route.fulfill({ json: weatherSettings })
        return
      }
      if (route.request().method() === 'DELETE') {
        weatherSettings = undefined
        await route.fulfill({ status: 204 })
        return
      }
      await route.fulfill(weatherSettings ? { json: weatherSettings } : { status: 204 })
      return
    }
    if (url.pathname === `/api/households/${authenticatedUser.households[0].id}/weather`) {
      await route.fulfill({ json: weatherSettings ? {
        status: 'fresh', locationLabel: weatherSettings.locationLabel, temperatureUnit: 'fahrenheit',
        current: { temperature: 72, summary: 'Sunny', icon: 'clear' },
        forecast: [{ name: 'Today', start: '2026-08-31T12:00:00Z', end: '2026-09-01T00:00:00Z',
          temperature: 76, temperatureUnit: 'fahrenheit', summary: 'Sunny', icon: 'clear', isDaytime: true }],
        observedAt: '2026-08-31T16:00:00Z', retrievedAt: '2026-08-31T16:05:00Z', isStale: false,
        attribution: 'Weather data from the National Weather Service',
      } : { status: 'locationRequired', attribution: 'Weather data from the National Weather Service' } })
      return
    }
    if (url.pathname === `/api/households/${authenticatedUser.households[0].id}/calendar/events`) {
      if (route.request().method() === 'POST') {
        const body = route.request().postDataJSON() as {
          sourceId: string
          title: string
          start: string
          end: string
          timeZone: string
        }
        await route.fulfill({ status: 201, json: {
          id: 'created-event',
          sourceId: body.sourceId,
          calendarName: 'Family',
          title: body.title,
          isAllDay: false,
          start: body.start,
          end: body.end,
          timeZone: body.timeZone,
          location: null,
          color: '#73b49a',
          canEdit: true,
          canDelete: true,
          managementId: '60000000-0000-0000-0000-000000000001',
          providerVersion: 'etag-one',
          managementUnavailableReason: null,
          attributedMemberId: authenticatedUser.households[0].memberId,
          recoveredExistingEvent: false,
        } })
        return
      }
      await route.fulfill({ json: {
        events: [{
          id: 'school-drop-off',
          sourceId: '50000000-0000-0000-0000-000000000001',
          calendarName: 'Family',
          title: 'School drop-off',
          isAllDay: false,
          start: '2026-08-18T12:00:00Z',
          end: '2026-08-18T12:30:00Z',
          timeZone: 'America/New_York',
          location: null,
          color: '#73b49a',
          canEdit: true,
          canDelete: true,
          managementId: '60000000-0000-0000-0000-000000000001',
          providerVersion: 'etag-one',
          managementUnavailableReason: null,
        }],
        nextCursor: null,
        isStale: false,
        warnings: [],
      } })
      return
    }
    if (url.pathname === `/api/households/${authenticatedUser.households[0].id}/calendar/managed-events/60000000-0000-0000-0000-000000000001`) {
      if (route.request().method() === 'PUT') {
        const body = route.request().postDataJSON() as { title: string }
        await route.fulfill({ json: {
          operation: 'update', completedAt: new Date().toISOString(), recoveredExistingMutation: false,
          event: { managementId: '60000000-0000-0000-0000-000000000001', sourceId: '50000000-0000-0000-0000-000000000001', calendarName: 'Family', title: body.title, location: null, notes: null, isAllDay: false, start: '2026-08-18T12:00:00Z', end: '2026-08-18T12:30:00Z', timeZone: 'America/New_York', providerVersion: 'etag-two', canEdit: true, canDelete: true, managementUnavailableReason: null },
        } })
        return
      }
      await route.fulfill({ json: {
        managementId: '60000000-0000-0000-0000-000000000001',
        sourceId: '50000000-0000-0000-0000-000000000001', calendarName: 'Family',
        title: 'School drop-off', location: null, notes: null, isAllDay: false,
        start: '2026-08-18T12:00:00Z', end: '2026-08-18T12:30:00Z',
        timeZone: 'America/New_York', providerVersion: 'etag-one', canEdit: true,
        canDelete: true, managementUnavailableReason: null,
      } })
      return
    }
    if (url.pathname === `/api/households/${authenticatedUser.households[0].id}/calendar/managed-events/60000000-0000-0000-0000-000000000001/delete`) {
      await route.fulfill({ json: {
        operation: 'delete', completedAt: new Date().toISOString(),
        recoveredExistingMutation: false, event: null,
      } })
      return
    }
    if (url.pathname === `/api/households/${authenticatedUser.households[0].id}/calendar/connection`) {
      await route.fulfill({ json: {
        isAvailable: true,
        connectionId: '40000000-0000-0000-0000-000000000001',
        status: 'connected',
        providerEmail: 'calendar@example.test',
        connectedAt: '2026-08-18T12:00:00Z',
        canManageConnection: true,
        activeSourceCount: 1,
        eventCreationAvailable: true,
        eventCreationAuthorized: true,
      } })
      return
    }
    if (url.pathname === `/api/households/${authenticatedUser.households[0].id}/calendar/event-creation-target`) {
      await route.fulfill({ json: {
        isAvailable: true,
        isAuthorized: true,
        sourceId: '50000000-0000-0000-0000-000000000001',
        name: 'Family',
        timeZone: 'America/New_York',
        color: '#73b49a',
      } })
      return
    }
    if (url.pathname === `/api/households/${authenticatedUser.households[0].id}/calendar/provider-calendars`) {
      await route.fulfill({ json: [
        { id: 'family@example.test', name: 'Family', timeZone: 'America/New_York', color: '#73b49a', isPrimary: true, isSelected: true, accessRole: 'owner', canCreateEvents: true, isEventCreationTarget: true },
        { id: 'school@example.test', name: 'School', timeZone: 'America/New_York', color: '#4285f4', isPrimary: false, isSelected: false, accessRole: 'reader', canCreateEvents: false, isEventCreationTarget: false },
      ] })
      return
    }
    if (url.pathname === `/api/households/${authenticatedUser.households[0].id}/calendar/sources`) {
      await route.fulfill({ json: route.request().method() === 'PUT'
        ? [{ id: '50000000-0000-0000-0000-000000000001', connectionId: '40000000-0000-0000-0000-000000000001', externalCalendarId: 'family@example.test', name: 'Family', isActive: true, isOwnedByCurrentAdult: true, isEventCreationTarget: true }]
        : [{ id: '50000000-0000-0000-0000-000000000001', connectionId: '40000000-0000-0000-0000-000000000001', externalCalendarId: 'family@example.test', name: 'Family', isActive: true, isOwnedByCurrentAdult: true, isEventCreationTarget: true }] })
      return
    }
    if (url.pathname === `/api/households/${authenticatedUser.households[0].id}/tasks`) {
      if (route.request().method() === 'POST') {
        await route.fulfill({ json: { operation: 'create', taskId: 'task-created', sourceId: 'task-source-1',
          status: 'needsAction', dueDate: null, mutationVersion: 'task-created-version',
          attributedMemberId: authenticatedUser.households[0].memberId, recoveredExistingMutation: false } })
        return
      }
      await route.fulfill({ json: { tasks: [{ id: 'task-1', sourceId: 'task-source-1', taskListName: 'Family tasks',
        title: 'Pack lunches', notes: null, status: 'needsAction', dueDate: '2026-08-27', completedAt: null,
        parentTaskId: null, position: '1', isSubtask: false, isAssigned: false, canChangeStatus: true,
        mutationVersion: 'task-version' }], nextCursor: null,
        isStale: false, warnings: [], canCreateTasks: true } })
      return
    }
    if (url.pathname === `/api/households/${authenticatedUser.households[0].id}/tasks/status`) {
      await route.fulfill({ json: { operation: 'complete', taskId: 'task-1', sourceId: 'task-source-1',
        status: 'completed', dueDate: '2026-08-27', mutationVersion: 'task-version-2',
        attributedMemberId: authenticatedUser.households[0].memberId, recoveredExistingMutation: false } })
      return
    }
    if (url.pathname === `/api/households/${authenticatedUser.households[0].id}/tasks/connection`) {
      await route.fulfill({ json: { isAvailable: true, connectionId: 'tasks-connection-1', status: 'active',
        providerEmail: 'tasks@example.test', connectedAt: '2026-08-26T12:00:00Z', activeSourceCount: 1,
        activeHouseholdCount: 1, canRead: true, canWrite: true, writeAuthorizationRequired: false,
        mutationsAvailable: true } })
      return
    }
    if (url.pathname === `/api/households/${authenticatedUser.households[0].id}/tasks/provider-task-lists`) {
      await route.fulfill({ json: [{ id: 'list-1', name: 'Family tasks', isSelected: true, canWrite: true, isWriteTarget: true },
        { id: 'list-2', name: 'School tasks', isSelected: false, canWrite: true, isWriteTarget: false }] })
      return
    }
    if (url.pathname === `/api/households/${authenticatedUser.households[0].id}/tasks/sources`) {
      await route.fulfill({ json: [{ id: 'task-source-1', connectionId: 'tasks-connection-1',
        externalTaskListId: 'list-1', name: 'Family tasks', isActive: true, isOwnedByCurrentAdult: true,
        canWrite: true, isWriteTarget: true }] })
      return
    }
    if (url.pathname === `/api/households/${authenticatedUser.households[0].id}/parent-access`) {
      await route.fulfill({ json: parentAccess })
      return
    }
    if (url.pathname === `/api/households/${authenticatedUser.households[0].id}/parent-access/verify`) {
      const body = route.request().postDataJSON() as { pin: string }
      if (body.pin !== '482913') {
        await route.fulfill({ status: 403, json: { title: 'The parent PIN could not be verified.', status: 403, code: 'parent_pin_invalid' } })
        return
      }
      const expires = new Date(Date.now() + 300_000).toISOString()
      parentAccess = { ...parentAccess, isElevated: true, elevationExpiresAt: expires }
      currentSession = {
        ...currentSession,
        isSharedDisplay: true,
        deviceLabel: 'Kitchen display',
        administrativeElevationHouseholdId: authenticatedUser.households[0].id,
        administrativeElevationExpiresAt: expires,
      }
      await route.fulfill({ json: parentAccess })
      return
    }
    if (url.pathname === '/api/auth/session/shared-display') {
      const body = route.request().postDataJSON() as { isSharedDisplay: boolean; deviceLabel?: string }
      currentSession = {
        ...currentSession,
        isSharedDisplay: body.isSharedDisplay,
        deviceLabel: body.isSharedDisplay ? body.deviceLabel ?? null : null,
        administrativeElevationHouseholdId: null,
        administrativeElevationExpiresAt: null,
      }
      parentAccess = { ...parentAccess, isSharedDisplay: body.isSharedDisplay, isElevated: false, elevationExpiresAt: null }
      await route.fulfill({ json: currentSession })
      return
    }
    if (url.pathname === `/api/households/${authenticatedUser.households[0].id}`) {
      if (route.request().method() === 'PATCH') {
        const body = route.request().postDataJSON() as { name: string }
        householdName = body.name
      }
      await route.fulfill({
        json: {
          id: authenticatedUser.households[0].id,
          name: householdName,
          timeZone: 'America/New_York',
          locale: 'en-US',
          weekStartsOn: 'Sunday',
          access: {
            memberId: authenticatedUser.households[0].memberId,
            role: 'adult',
            canAdminister: true,
          },
        },
      })
      return
    }
    if (url.pathname === `/api/households/${authenticatedUser.households[0].id}/members`) {
      if (route.request().method() === 'POST') {
        const body = route.request().postDataJSON() as { displayName: string; avatarColor: string }
        const child = {
          id: '30000000-0000-0000-0000-000000000002',
          displayName: body.displayName,
          role: 'child' as const,
          avatarColor: body.avatarColor,
          isActive: true,
        }
        householdMembers.push(child)
        await route.fulfill({ json: child, status: 201 })
        return
      }
      await route.fulfill({ json: householdMembers })
      return
    }
    const choreAssignment = {
      id: '60000000-0000-0000-0000-000000000001',
      choreDefinitionId: '61000000-0000-0000-0000-000000000001',
      title: 'Feed Milo', description: 'Before dinner', pointValue: 10,
      assignedMember: householdMembers[0], dueLocalDate: '2026-08-22', dueLocalTime: '18:00:00',
      dueAt: '2026-08-22T22:00:00Z', dueTimeZone: 'America/New_York', dueHasExplicitTime: true,
      status: 'pending', isOverdue: false, version: 1, pendingCompletion: null,
      createdAt: '2026-08-22T12:00:00Z', updatedAt: '2026-08-22T12:00:00Z',
    }
    if (url.pathname === `/api/households/${authenticatedUser.households[0].id}/chores/dashboard`) {
      await route.fulfill({ json: { overdue: [], dueToday: [choreAssignment], upcoming: [], awaitingReviewCount: 0 } })
      return
    }
    if (url.pathname === `/api/households/${authenticatedUser.households[0].id}/chores/participants`) {
      await route.fulfill({ json: householdMembers })
      return
    }
    if (url.pathname === `/api/households/${authenticatedUser.households[0].id}/chore-assignments`) {
      await route.fulfill({ json: { items: [choreAssignment], nextCursor: null } })
      return
    }
    if (url.pathname === `/api/households/${authenticatedUser.households[0].id}/chore-assignments/${choreAssignment.id}/completions`) {
      await route.fulfill({ json: { id: '62000000-0000-0000-0000-000000000001', assignmentId: choreAssignment.id,
        completedByMember: householdMembers[0], status: 'pendingReview', wasSharedDisplay: currentSession.isSharedDisplay,
        pointValue: 10, completedAt: '2026-08-22T18:00:00Z', reviewedByMember: null,
        reviewedAt: null, reviewNote: null, version: 1, award: null } })
      return
    }
    const choreDefinition = { id: choreAssignment.choreDefinitionId, title: 'Feed Milo', description: 'Before dinner',
      defaultPointValue: 10, isActive: true, version: 1, createdAt: '2026-08-22T12:00:00Z', updatedAt: '2026-08-22T12:00:00Z' }
    if (url.pathname === `/api/households/${authenticatedUser.households[0].id}/chore-definitions`) {
      await route.fulfill({ json: [choreDefinition] })
      return
    }
    if (url.pathname === `/api/households/${authenticatedUser.households[0].id}/chore-completions`) {
      await route.fulfill({ json: [] })
      return
    }
    if (url.pathname === `/api/households/${authenticatedUser.households[0].id}/chore-schedules`) {
      if (route.request().method() === 'POST') {
        const body = route.request().postDataJSON() as { startLocalDate: string; dueLocalTime: string; recurrence: Record<string, unknown> }
        const schedule = { id: '63000000-0000-0000-0000-000000000001', definition: choreDefinition,
          assignedMember: householdMembers[0], recurrence: body.recurrence, startLocalDate: body.startLocalDate,
          endLocalDate: null, dueLocalTime: `${body.dueLocalTime}:00`, timeZone: 'America/New_York', status: 'active',
          blockedReason: null, nextOccurrenceLocalDate: body.startLocalDate, lastGeneratedOccurrenceLocalDate: null,
          lastEvaluatedAt: null, version: 1, createdAt: '2026-08-22T12:00:00Z', updatedAt: '2026-08-22T12:00:00Z' }
        choreSchedules.push(schedule)
        await route.fulfill({ status: 201, json: schedule })
        return
      }
      await route.fulfill({ json: choreSchedules })
      return
    }
    if (url.pathname === `/api/households/${authenticatedUser.households[0].id}/points/summary`) {
      await route.fulfill({ json: { householdBalance: 35, members: householdMembers.map((member, index) => ({
        memberId: member.id, displayName: member.displayName, role: member.role, avatarColor: member.avatarColor,
        isActive: member.isActive, balance: index === 0 ? 25 : 10,
      })), recentTransactions: [] } })
      return
    }
    if (url.pathname === `/api/households/${authenticatedUser.households[0].id}/rewards`) {
      await route.fulfill({ json: { rewards: [{ id: 'reward-1', title: 'Movie night', description: 'Choose the movie',
        pointCost: 20, isActive: true, version: 1, createdAt: '2026-08-25T12:00:00Z', updatedAt: '2026-08-25T12:00:00Z' }],
        members: householdMembers.map((member, index) => ({ memberId: member.id, displayName: member.displayName,
          role: member.role, avatarColor: member.avatarColor, isActive: member.isActive, balance: index === 0 ? 25 : 10 })) } })
      return
    }
    if (url.pathname === `/api/households/${authenticatedUser.households[0].id}/reward-redemptions`) {
      if (route.request().method() === 'POST') {
        const body = route.request().postDataJSON() as { rewardId: string; householdMemberId: string }
        const item = { id: 'redemption-1', rewardId: body.rewardId, rewardTitle: 'Movie night',
          rewardDescription: 'Choose the movie', pointCost: 20, householdMember: householdMembers[0],
          status: 'requested', requestedByMember: householdMembers[0], wasSharedDisplay: currentSession.isSharedDisplay,
          requestedAt: '2026-08-25T12:00:00Z', reviewedByMember: null, reviewedAt: null, reviewNote: null,
          fulfilledByMember: null, fulfilledAt: null, cancelledByMember: null, cancelledAt: null,
          cancellationReason: null, version: 1 }
        rewardRedemptions.unshift(item); await route.fulfill({ status: 201, json: item }); return
      }
      await route.fulfill({ json: { items: rewardRedemptions, nextCursor: null } })
      return
    }
    if (url.pathname === `/api/households/${authenticatedUser.households[0].id}/point-transactions`) {
      await route.fulfill({ json: { items: [], nextCursor: null } })
      return
    }
    if (url.pathname === `/api/households/${authenticatedUser.households[0].id}/invitations`) {
      if (route.request().method() === 'POST') {
        const body = route.request().postDataJSON() as { intendedEmail: string }
        const invitation = {
          id: '40000000-0000-0000-0000-000000000001',
          householdId: authenticatedUser.households[0].id,
          intendedEmail: body.intendedEmail,
          status: 'pending' as const,
          createdAt: '2026-08-15T12:00:00Z',
          expiresAt: '2026-08-22T12:00:00Z',
          acceptedAt: null,
          revokedAt: null,
        }
        householdInvitations.unshift(invitation)
        await route.fulfill({ json: { invitation, token: 'a'.repeat(43) }, status: 201 })
        return
      }
      await route.fulfill({ json: householdInvitations })
      return
    }
    if (url.pathname === '/api/invitations/prepare') {
      await route.fulfill({ json: {
        householdName: authenticatedUser.households[0].name,
        intendedEmailMasked: 'a•••••@example.test',
        expiresAt: '2026-08-22T12:00:00Z',
      } })
      return
    }
    if (url.pathname === '/api/invitations/pending/accept') {
      await route.fulfill({ json: {
        household: authenticatedUser.households[0],
        selectedHouseholdId: authenticatedUser.households[0].id,
        reusedExistingMembership: false,
      } })
      return
    }
    await route.abort()
  })
})

test('dashboard shell is readable and fits the viewport', async ({ page }) => {
  await page.goto('/')

  await expect(page.getByRole('heading', { level: 1, name: 'Bamford-Fahie-Waltz Family' })).toBeVisible()
  await expect(page.getByRole('heading', { name: 'Today' })).toBeVisible()
  await expect(page.getByRole('navigation', { name: 'Primary navigation' })).toBeVisible()
  await expect(page.getByText('School drop-off')).toBeVisible()
  await expect(page.getByText('Feed Milo')).toBeVisible()
  await expect(page.getByRole('region', { name: 'Tasks' })).toBeVisible()
  await expect(page.getByRole('region', { name: 'Ready for a good day?' }))
    .toHaveCSS('background-image', /demo-family-photo\.jpg/)

  const navigationRows = await page.getByRole('navigation', { name: 'Primary navigation' })
    .getByRole('link').evaluateAll((links) => links.map((link) => Math.round(link.getBoundingClientRect().top)))
  expect(new Set(navigationRows).size).toBe(1)

  const tasksWidth = await page.getByRole('region', { name: 'Tasks' })
    .evaluate((element) => element.getBoundingClientRect().width)
  expect(tasksWidth).toBeGreaterThan(280)

  const horizontalOverflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  )
  expect(horizontalOverflow).toBeLessThanOrEqual(1)
})

test('dashboard shell has no automatically detectable serious accessibility issues', async ({ page }) => {
  await page.goto('/')

  const results = await new AxeBuilder({ page }).analyze()

  expect(results.violations.filter((violation) => ['critical', 'serious'].includes(violation.impact ?? '')))
    .toEqual([])
})

test('primary navigation opens the reward catalog with a pointer click', async ({ page }) => {
  await page.goto('/')

  const rewardsLink = page.getByRole('link', { name: 'Rewards', exact: true })
  await rewardsLink.click()

  await expect(page).toHaveURL(/\/rewards$/)
  await expect(rewardsLink).toHaveAttribute('aria-current', 'page')
  await expect(page.getByRole('heading', { name: 'Reward catalog' })).toBeVisible()
})

test('workspace supports mouse dragging between adjacent primary views', async ({ page }) => {
  await page.goto('/')
  const viewport = page.locator('.workspace-viewport')
  const bounds = await viewport.boundingBox()
  expect(bounds).not.toBeNull()

  await page.mouse.move(bounds!.x + bounds!.width * 0.75, bounds!.y + 120)
  await page.mouse.down()
  await page.mouse.move(bounds!.x + bounds!.width * 0.5, bounds!.y + 124, { steps: 6 })
  await page.mouse.up()

  await expect(page).toHaveURL(/\/calendar$/)
  await expect(page.getByRole('heading', { name: 'Family calendar' })).toBeVisible()
  await expect(page.locator('#main-content')).toBeFocused()
  await expect(page.locator('#main-content')).toHaveCSS('outline-style', 'none')
})

test('primary feature tabs share the same bordered surface treatment', async ({ page }) => {
  for (const path of ['/calendar', '/tasks', '/chores', '/rewards']) {
    await page.goto(path)
    const surface = page.locator('#main-content')
    await expect(surface).toHaveCSS('background-color', 'rgb(255, 253, 248)')
    await expect(surface).toHaveCSS('border-top-width', '1px')
    await expect(surface).toHaveCSS('border-top-left-radius', /^(22\.4|32)px$/)
  }
})

test('reward redemption requires explicit member attribution and confirms the request', async ({ page }) => {
  await page.goto('/rewards')
  await page.getByLabel('Points belong to').selectOption(authenticatedUser.households[0].memberId)
  await page.getByRole('button', { name: 'Redeem reward' }).click()
  await expect(page.getByRole('dialog')).toBeVisible()
  await page.getByRole('dialog').getByRole('button', { name: 'Request for 20 points' }).click()
  await expect(page.getByText('Reward request sent for adult review.')).toBeVisible()
  await expect(page.getByText(/requested/i)).toBeVisible()
})

test('chore board supports explicit household-member completion', async ({ page }) => {
  await page.goto('/chores')
  await expect(page.getByRole('heading', { name: 'Chores' })).toBeVisible()
  await page.getByRole('listitem').getByRole('button', { name: 'Mark done' }).click()
  await expect(page.getByRole('heading', { name: /Mark “Feed Milo” done/ })).toBeVisible()
  await page.getByLabel('Who completed it?').selectOption(authenticatedUser.households[0].memberId)
  await page.getByRole('dialog').getByRole('button', { name: 'Mark done' }).click()
  await expect(page.getByRole('dialog')).toBeHidden()
  const results = await new AxeBuilder({ page }).analyze()
  expect(results.violations.filter((violation) => ['critical', 'serious'].includes(violation.impact ?? ''))).toEqual([])
})

test('an adult can schedule a daily household-local chore with touch-sized controls', async ({ page }) => {
  await page.goto(`/households/${authenticatedUser.households[0].id}/chores`)
  await expect(page.getByRole('heading', { name: 'Schedule a chore' })).toBeVisible()
  const form = page.getByRole('heading', { name: 'Schedule a chore' }).locator('..')
  await form.locator('select').nth(0).selectOption('61000000-0000-0000-0000-000000000001')
  await form.getByLabel('Assigned to').selectOption(authenticatedUser.households[0].memberId)
  await form.getByLabel('Starts').fill('2026-08-24')
  await form.getByLabel('Due time').fill('08:00')
  await form.getByRole('button', { name: 'Save schedule' }).click()
  await expect(page.getByText('Every day · Due 08:00')).toBeVisible()
  await expect(page.getByText('Next: 2026-08-24')).toBeVisible()
  const results = await new AxeBuilder({ page }).analyze()
  expect(results.violations.filter((violation) => ['critical', 'serious'].includes(violation.impact ?? ''))).toEqual([])
})

test('calendar navigation and household source selection work with touch-sized controls', async ({ page }) => {
  await page.goto('/')
  await page.getByRole('link', { name: 'Calendar', exact: true }).click()
  await expect(page).toHaveURL(/\/calendar$/)
  await expect(page.getByRole('heading', { name: 'Family calendar' })).toBeVisible()
  await expect(page.getByText('School drop-off')).toBeVisible()

  await page.getByRole('link', { name: 'Calendar settings' }).click()
  await expect(page.getByRole('heading', { name: 'Google Calendar' })).toBeVisible()
  await page.getByRole('checkbox', { name: /School/ }).check()
  await page.getByRole('button', { name: 'Save visible calendars' }).click()
  await expect(page.getByText('Household calendars saved.')).toBeVisible()

  const results = await new AxeBuilder({ page }).analyze()
  expect(results.violations.filter((violation) => ['critical', 'serious'].includes(violation.impact ?? '')))
    .toEqual([])
})

test('Google Tasks navigation and household list selection are accessible', async ({ page }) => {
  await page.goto('/')
  await page.getByRole('link', { name: 'Tasks', exact: true }).click()
  await expect(page).toHaveURL(/\/tasks$/)
  await expect(page.getByText('Pack lunches')).toBeVisible()
  await page.getByRole('button', { name: 'Complete', exact: true }).click()
  await expect(page.getByText('Task completed in Google Tasks.')).toBeVisible()
  await page.getByRole('link', { name: 'Add task' }).click()
  await page.getByLabel('Task title').fill('Prepare backpacks')
  await page.getByRole('button', { name: 'Add task' }).click()
  await expect(page.getByText('Task added to Google Tasks.')).toBeVisible()
  await page.getByRole('link', { name: 'Task settings' }).click()
  await expect(page.getByRole('heading', { name: 'Google Tasks' })).toBeVisible()
  await page.getByRole('checkbox', { name: /School tasks/ }).check()
  await page.getByRole('button', { name: 'Save visible task lists' }).click()
  await expect(page.getByText('Visible Google task lists saved.')).toBeVisible()
  const results = await new AxeBuilder({ page }).analyze()
  expect(results.violations.filter((violation) => ['critical', 'serious'].includes(violation.impact ?? ''))).toEqual([])
})

test('controlled event creation is keyboard accessible and returns to the calendar', async ({ page }) => {
  await page.goto('/calendar')
  await page.getByRole('link', { name: 'Add event' }).click()
  await expect(page.getByRole('heading', { name: 'Add a family event' })).toBeVisible()
  await page.getByLabel('Event title').fill('Dentist appointment')
  await page.getByRole('button', { name: 'Add to calendar' }).click()
  await expect(page).toHaveURL(/\/calendar$/)
  await expect(page.getByText('Event added to Google Calendar.')).toBeVisible()

  const results = await new AxeBuilder({ page }).analyze()
  expect(results.violations.filter((violation) => ['critical', 'serious'].includes(violation.impact ?? '')))
    .toEqual([])
})

test('a Family Dashboard-created event has accessible edit and explicit delete controls', async ({ page }) => {
  await page.goto('/calendar')
  await page.getByRole('link', { name: 'Manage' }).click()
  await expect(page.getByRole('heading', { name: 'Manage event' })).toBeVisible()
  await page.getByLabel('Event title').fill('Updated school drop-off')
  await page.getByRole('button', { name: 'Save to Google Calendar' }).click()
  await expect(page).toHaveURL(/\/calendar$/)
  await expect(page.getByText('Event updated in Google Calendar.')).toBeVisible()

  await page.getByRole('link', { name: 'Manage' }).click()
  await page.getByRole('button', { name: 'Delete event' }).click()
  await expect(page.getByRole('dialog', { name: 'Delete from Google Calendar?' })).toBeVisible()
  await page.getByRole('button', { name: 'Delete from Google Calendar' }).click()
  await expect(page).toHaveURL(/\/calendar$/)
  await expect(page.getByText('Event deleted from Google Calendar.')).toBeVisible()
})

test('a waiting PWA update cannot interrupt an active form', async ({ page }) => {
  await page.goto('/calendar/new')
  await expect(page.getByRole('heading', { name: 'Add a family event' })).toBeVisible()
  await page.evaluate(() => {
    window.dispatchEvent(new CustomEvent('family-dashboard:update-ready', {
      detail: async () => undefined,
    }))
  })

  await expect(page.locator('.update-banner')).toContainText('Finish or leave this form before updating.')
  await expect(page.getByRole('button', { name: 'Update now' })).toBeDisabled()
})

test('account menu supports keyboard access and signs out through the API', async ({ page }) => {
  await page.goto('/')

  const accountMenu = page.getByRole('button', { name: 'Account menu for Ryan Bamford' })
  await accountMenu.focus()
  await page.keyboard.press('Enter')
  await page.getByRole('menuitem', { name: 'Sign out' }).click()

  await expect(page.getByRole('heading', { name: /bring the whole family/i })).toBeVisible()
})

test('an adult can update settings and add a child with keyboard-accessible controls', async ({ page }) => {
  await page.goto('/')
  await page.getByRole('button', { name: 'Account menu for Ryan Bamford' }).click()
  await page.getByRole('menuitem', { name: 'Household settings' }).click()

  await expect(page.getByRole('heading', { name: 'Household settings' })).toBeVisible()
  await page.getByLabel('Household name').fill('Updated Family')
  await page.getByRole('button', { name: 'Save settings' }).click()
  await expect(page.getByText('Household settings saved.')).toBeVisible()
  await expect(page.getByRole('heading', { level: 1, name: 'Updated Family' })).toBeVisible()

  await page.getByRole('link', { name: 'Members' }).click()
  await page.getByRole('button', { name: 'Add child' }).click()
  const editor = page.getByRole('dialog', { name: 'Add a child' })
  await editor.getByLabel('Display name').fill('Zoey')
  await editor.getByLabel('Coral').check()
  await editor.getByRole('button', { name: 'Add child' }).click()

  await expect(editor).toBeHidden()
  await expect(page.getByRole('heading', { name: 'Zoey' })).toBeVisible()
  await expect(page.getByText('Zoey was added.')).toBeVisible()
  await expect(page.getByText('Adult · You')).toBeVisible()
  await expect(page.getByRole('button', { name: 'Deactivate' })).toHaveCount(1)

  const results = await new AxeBuilder({ page }).analyze()
  expect(results.violations.filter((violation) => ['critical', 'serious'].includes(violation.impact ?? '')))
    .toEqual([])
})

test('an adult can personalize the dashboard and configure an accessible weather forecast', async ({ page }) => {
  await page.goto(`/households/${authenticatedUser.households[0].id}/settings/appearance`)
  await page.getByLabel('Custom greeting title (optional)').fill('Welcome home')
  await page.getByLabel('Family message (optional)').fill('Dinner is at six.')
  await page.getByRole('button', { name: 'Save appearance' }).click()
  await expect(page.getByText('Dashboard appearance saved.')).toBeVisible()

  await page.goto(`/households/${authenticatedUser.households[0].id}/settings/weather`)
  await page.getByLabel('Location label').fill('Near home')
  await page.getByLabel('Latitude').fill('40.7128')
  await page.getByLabel('Longitude').fill('-74.0060')
  await page.getByRole('button', { name: 'Save weather location' }).click()
  await expect(page.getByText('Weather location saved.')).toBeVisible()

  await page.goto('/')
  await expect(page.getByText('Welcome home')).toBeVisible()
  await expect(page.getByText('Dinner is at six.')).toBeVisible()
  await page.getByRole('button', { name: 'Open weather forecast: 72 degrees, Sunny' }).click()
  const dialog = page.getByRole('dialog')
  await expect(dialog.getByRole('heading', { name: 'Household forecast' })).toBeVisible()
  await expect(dialog.getByText('Weather data from the National Weather Service')).toBeVisible()
  const results = await new AxeBuilder({ page }).include('.weather-dialog').analyze()
  expect(results.violations.filter((violation) => ['critical', 'serious'].includes(violation.impact ?? ''))).toEqual([])
  await page.keyboard.press('Escape')
  await expect(dialog).toBeHidden()
})

test('an adult can create a copyable email-bound invitation', async ({ page }) => {
  await page.goto(`/households/${authenticatedUser.households[0].id}/invitations`)

  await expect(page.getByRole('heading', { name: 'Invitation links' })).toBeVisible()
  await page.getByLabel('Adult email address').fill('adult@example.test')
  await page.getByRole('button', { name: 'Create invitation' }).click()

  await expect(page.getByRole('textbox', { name: 'Invitation link', exact: true }))
    .toHaveValue(/\/invite#token=/)
  await expect(page.getByText('adult@example.test')).toBeVisible()
  const results = await new AxeBuilder({ page }).analyze()
  expect(results.violations.filter((violation) => ['critical', 'serious'].includes(violation.impact ?? '')))
    .toEqual([])
})

test('an authenticated invited adult can accept without retaining the raw URL fragment', async ({ page }) => {
  await page.goto(`/invite#token=${'b'.repeat(43)}`)

  await expect(page.getByRole('heading', { name: 'Join Bamford-Fahie-Waltz Family' })).toBeVisible()
  await expect(page).toHaveURL(/\/invite$/)
  await page.getByRole('button', { name: 'Join household' }).click()

  await expect(page).toHaveURL(/\/$/)
  await expect(page.getByRole('heading', { level: 1, name: 'Bamford-Fahie-Waltz Family' })).toBeVisible()
})

test('a shared display keeps the dashboard available and gates administration with the parent PIN', async ({ page }) => {
  currentSession = { ...currentSession, isSharedDisplay: true, deviceLabel: 'Kitchen display' }
  parentAccess = { ...parentAccess, isSharedDisplay: true, isElevated: false, elevationExpiresAt: null }
  await page.goto(`/households/${authenticatedUser.households[0].id}/settings`)

  await expect(page.getByRole('heading', { name: 'Unlock parent controls' })).toBeVisible()
  await page.getByLabel('6-digit parent PIN').fill('000000')
  await page.getByRole('button', { name: 'Unlock' }).click()
  await expect(page.getByRole('alert')).toContainText('did not work')

  await page.getByLabel('6-digit parent PIN').fill('482913')
  await page.getByRole('button', { name: 'Unlock' }).click()
  await expect(page.getByRole('heading', { name: 'Household settings' })).toBeVisible()

  await page.goto('/')
  await expect(page.getByRole('heading', { name: 'Today' })).toBeVisible()
  await page.getByRole('button', { name: 'Account menu for Ryan Bamford' }).click()
  await expect(page.getByText('Shared display')).toBeVisible()
})

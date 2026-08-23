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
        }],
        nextCursor: null,
        isStale: false,
        warnings: [],
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
      title: 'Feed Milo', description: 'Before dinner',
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
        completedAt: '2026-08-22T18:00:00Z', reviewedByMember: null, reviewedAt: null, reviewNote: null, version: 1 } })
      return
    }
    const choreDefinition = { id: choreAssignment.choreDefinitionId, title: 'Feed Milo', description: 'Before dinner',
      isActive: true, version: 1, createdAt: '2026-08-22T12:00:00Z', updatedAt: '2026-08-22T12:00:00Z' }
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
  await expect(page.getByRole('region', { name: 'Ready for a good day?' }))
    .toHaveCSS('background-image', /demo-family-photo\.jpg/)

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

test('primary navigation responds to pointer clicks', async ({ page }) => {
  await page.goto('/')

  const rewardsLink = page.getByRole('link', { name: 'Rewards' })
  await rewardsLink.click()

  await expect(page).toHaveURL(/#rewards-preview$/)
  await expect(rewardsLink).toHaveAttribute('aria-current', 'location')
  await expect(page.locator('#rewards-preview')).toBeVisible()
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

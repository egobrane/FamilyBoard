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
    administrativeElevationExpiresAt: null,
  },
}

let householdName = authenticatedUser.households[0].name
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

test.beforeEach(async ({ page }) => {
  householdName = authenticatedUser.households[0].name
  householdMembers = [{
    id: authenticatedUser.households[0].memberId,
    displayName: authenticatedUser.user.displayName,
    role: 'adult',
    avatarColor: 'mint',
    isActive: true,
  }]
  householdInvitations = []
  await page.route('http://localhost:8080/api/**', async (route) => {
    const url = new URL(route.request().url())
    if (url.pathname === '/api/auth/me') {
      await route.fulfill({
        json: {
          ...authenticatedUser,
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

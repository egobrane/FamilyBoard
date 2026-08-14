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

test.beforeEach(async ({ page }) => {
  await page.route('http://localhost:8080/api/**', async (route) => {
    const url = new URL(route.request().url())
    if (url.pathname === '/api/auth/me') {
      await route.fulfill({ json: authenticatedUser })
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

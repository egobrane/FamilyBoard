import AxeBuilder from '@axe-core/playwright'
import { expect, test } from '@playwright/test'

test('dashboard shell is readable and fits the viewport', async ({ page }) => {
  await page.goto('/')

  await expect(page.getByRole('heading', { name: 'Today' })).toBeVisible()
  await expect(page.getByRole('navigation', { name: 'Primary navigation' })).toBeVisible()
  await expect(page.getByText('School drop-off')).toBeVisible()

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

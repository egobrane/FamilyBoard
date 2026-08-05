import { render, screen } from '@testing-library/react'
import { expect, it } from 'vitest'
import { DashboardCard } from './DashboardCard'

it('provides a named section for dashboard content', () => {
  render(<DashboardCard title="Family note">Hello</DashboardCard>)

  expect(screen.getByRole('region', { name: 'Family note' })).toHaveTextContent('Hello')
})

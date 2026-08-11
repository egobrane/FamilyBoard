import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { App } from './App'

describe('App', () => {
  it('renders the touch-first dashboard landmarks and mock content', () => {
    render(<App />)

    expect(screen.getByRole('banner')).toBeInTheDocument()
    expect(screen.getByRole('main')).toBeInTheDocument()
    expect(screen.getByRole('navigation', { name: /primary/i })).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 1, name: 'Bamford-Fahie-Waltz Family' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Today' })).toBeInTheDocument()
    expect(screen.getByText('Dentist appointment')).toBeInTheDocument()
    expect(screen.getAllByText('Oliver').length).toBeGreaterThan(0)
    expect(screen.getAllByText('Zoey').length).toBeGreaterThan(0)
    expect(screen.getByText('Feed Milo')).toBeInTheDocument()
    expect(screen.getByText(/chore actions arrive/i)).toBeInTheDocument()

    const welcomeCard = screen.getByRole('region', { name: 'Ready for a good day?' })
    expect(welcomeCard.style.getPropertyValue('--household-photo')).toContain('/images/demo-family-photo.jpg')
  })

  it('offers a keyboard-accessible skip link', async () => {
    const user = userEvent.setup()
    render(<App />)

    await user.tab()

    expect(screen.getByRole('link', { name: /skip to dashboard/i })).toHaveFocus()
    expect(screen.getByRole('link', { name: /skip to dashboard/i })).toHaveAttribute('href', '#main-content')
  })

  it('provides mouse and keyboard-accessible links to each dashboard preview', async () => {
    const user = userEvent.setup()
    render(<App />)

    const calendarLink = screen.getByRole('link', { name: 'Calendar' })

    expect(calendarLink).toHaveAttribute('href', '#calendar-preview')
    expect(screen.getByRole('link', { name: 'Chores' })).toHaveAttribute('href', '#chores-preview')
    expect(screen.getByRole('link', { name: 'Rewards' })).toHaveAttribute('href', '#rewards-preview')

    await user.click(calendarLink)

    expect(calendarLink).toHaveAttribute('aria-current', 'location')
    expect(screen.getByRole('link', { name: 'Home' })).not.toHaveAttribute('aria-current')
  })
})

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
    expect(screen.getByRole('heading', { name: 'Today' })).toBeInTheDocument()
    expect(screen.getByText('Dentist appointment')).toBeInTheDocument()
    expect(screen.getByText(/chore actions arrive/i)).toBeInTheDocument()
  })

  it('offers a keyboard-accessible skip link', async () => {
    const user = userEvent.setup()
    render(<App />)

    await user.tab()

    expect(screen.getByRole('link', { name: /skip to dashboard/i })).toHaveFocus()
    expect(screen.getByRole('link', { name: /skip to dashboard/i })).toHaveAttribute('href', '#main-content')
  })
})

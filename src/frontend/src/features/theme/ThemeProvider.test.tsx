import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ThemeProvider, themeStorageKey, useTheme } from './ThemeProvider'

function ThemeProbe() {
  const { effectiveTheme, preference, setPreference } = useTheme()
  return <button onClick={() => setPreference('dark')} type="button">{preference}:{effectiveTheme}</button>
}

afterEach(() => {
  vi.unstubAllGlobals()
  window.localStorage.clear()
  delete document.documentElement.dataset.theme
  document.documentElement.removeAttribute('style')
})

describe('ThemeProvider', () => {
  it('uses the system preference and persists an explicit selection', async () => {
    const listeners: Array<() => void> = []
    let dark = false
    vi.stubGlobal('matchMedia', vi.fn(() => ({
      get matches() { return dark },
      addEventListener: (_: string, listener: () => void) => listeners.push(listener),
      removeEventListener: vi.fn(),
    })))
    const user = userEvent.setup()
    render(<ThemeProvider><ThemeProbe /></ThemeProvider>)

    expect(screen.getByRole('button')).toHaveTextContent('system:light')
    dark = true
    listeners.forEach((listener) => listener())
    expect(await screen.findByText('system:dark')).toBeInTheDocument()

    await user.click(screen.getByRole('button'))
    expect(window.localStorage.getItem(themeStorageKey)).toBe('dark')
    expect(document.documentElement.dataset.theme).toBe('dark')
  })
})

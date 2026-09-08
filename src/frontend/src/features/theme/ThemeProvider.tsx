import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'

export type ThemePreference = 'system' | 'light' | 'dark'
export type EffectiveTheme = 'light' | 'dark'

interface ThemeContextValue {
  preference: ThemePreference
  effectiveTheme: EffectiveTheme
  setPreference: (preference: ThemePreference) => void
}

export const themeStorageKey = 'family-dashboard:theme'
const ThemeContext = createContext<ThemeContextValue | null>(null)

function storedPreference(): ThemePreference {
  const stored = window.localStorage.getItem(themeStorageKey)
  return stored === 'light' || stored === 'dark' ? stored : 'system'
}

function systemTheme(): EffectiveTheme {
  return typeof window.matchMedia === 'function' && window.matchMedia('(prefers-color-scheme: dark)').matches
    ? 'dark'
    : 'light'
}

function applyTheme(theme: EffectiveTheme) {
  document.documentElement.dataset.theme = theme
  document.documentElement.style.colorScheme = theme
  document.querySelector<HTMLMetaElement>('meta[name="theme-color"]')
    ?.setAttribute('content', theme === 'dark' ? '#151b1a' : '#f6f4ee')
}

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [preference, setPreferenceState] = useState<ThemePreference>(storedPreference)
  const [system, setSystem] = useState<EffectiveTheme>(systemTheme)
  const effectiveTheme = preference === 'system' ? system : preference

  useEffect(() => {
    if (typeof window.matchMedia !== 'function') return
    const media = window.matchMedia('(prefers-color-scheme: dark)')
    const update = () => setSystem(media.matches ? 'dark' : 'light')
    update()
    media.addEventListener('change', update)
    return () => media.removeEventListener('change', update)
  }, [])

  useEffect(() => applyTheme(effectiveTheme), [effectiveTheme])

  const setPreference = (value: ThemePreference) => {
    setPreferenceState(value)
    window.localStorage.setItem(themeStorageKey, value)
  }
  const context = useMemo(
    () => ({ preference, effectiveTheme, setPreference }),
    [preference, effectiveTheme],
  )

  return <ThemeContext.Provider value={context}>{children}</ThemeContext.Provider>
}

// The hook intentionally shares the provider module's private context.
// eslint-disable-next-line react-refresh/only-export-components
export function useTheme() {
  const context = useContext(ThemeContext)
  if (!context) throw new Error('useTheme must be used within ThemeProvider.')
  return context
}

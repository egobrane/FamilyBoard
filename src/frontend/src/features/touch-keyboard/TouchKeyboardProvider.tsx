import { createContext, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { useAuthentication } from '../authentication/AuthenticationContext'
import { TouchKeyboard } from './TouchKeyboard'
import { isTouchKeyboardTarget, type TouchKeyboardTarget } from './keyboardTarget'

export type TouchKeyboardPreference = 'auto' | 'on' | 'off'

interface TouchKeyboardContextValue {
  preference: TouchKeyboardPreference
  setPreference: (value: TouchKeyboardPreference) => void
}

const storageKey = 'family-dashboard:touch-keyboard'
const TouchKeyboardContext = createContext<TouchKeyboardContextValue | null>(null)

function initialPreference(): TouchKeyboardPreference {
  const stored = window.localStorage.getItem(storageKey)
  return stored === 'on' || stored === 'off' ? stored : 'auto'
}

function useAutomaticEligibility(isSharedDisplay: boolean) {
  const [eligible, setEligible] = useState(false)
  useEffect(() => {
    if (typeof window.matchMedia !== 'function') return
    const touch = window.matchMedia('(any-pointer: coarse)')
    const wide = window.matchMedia('(min-width: 56rem)')
    const update = () => setEligible(isSharedDisplay && touch.matches && wide.matches)
    update()
    touch.addEventListener('change', update)
    wide.addEventListener('change', update)
    return () => {
      touch.removeEventListener('change', update)
      wide.removeEventListener('change', update)
    }
  }, [isSharedDisplay])
  return eligible
}

export function TouchKeyboardProvider({ children }: { children: ReactNode }) {
  const { state } = useAuthentication()
  const isSharedDisplay = state.status === 'authenticated'
    && state.currentUser.session?.isSharedDisplay === true
  const automatic = useAutomaticEligibility(isSharedDisplay)
  const [preference, setPreferenceState] = useState<TouchKeyboardPreference>(initialPreference)
  const [target, setTarget] = useState<{ element: TouchKeyboardTarget; key: number } | null>(null)
  const targetKey = useRef(0)
  const enabled = preference === 'on' || (preference === 'auto' && automatic)

  const setPreference = (value: TouchKeyboardPreference) => {
    setPreferenceState(value)
    window.localStorage.setItem(storageKey, value)
    if (value === 'off') setTarget(null)
  }

  useEffect(() => {
    if (!enabled) return
    const focus = (event: FocusEvent) => {
      if (!isTouchKeyboardTarget(event.target)) return
      setTarget({ element: event.target, key: ++targetKey.current })
    }
    const keydown = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && target) setTarget(null)
    }
    document.addEventListener('focusin', focus)
    window.addEventListener('keydown', keydown)
    return () => {
      document.removeEventListener('focusin', focus)
      window.removeEventListener('keydown', keydown)
    }
  }, [enabled, target])

  useEffect(() => {
    if (!enabled || !target?.element.isConnected) {
      delete document.body.dataset.touchKeyboardOpen
      return
    }

    document.body.dataset.touchKeyboardOpen = 'true'
    let scrollFrame = 0
    const layoutFrame = window.requestAnimationFrame(() => {
      scrollFrame = window.requestAnimationFrame(() => target.element.scrollIntoView({
        behavior: typeof window.matchMedia === 'function' && window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth',
        block: 'center',
      }))
    })
    return () => {
      window.cancelAnimationFrame(layoutFrame)
      window.cancelAnimationFrame(scrollFrame)
      delete document.body.dataset.touchKeyboardOpen
    }
  }, [enabled, target])

  const context = useMemo(() => ({ preference, setPreference }), [preference])
  return <TouchKeyboardContext.Provider value={context}>
    {children}
    {enabled && target?.element.isConnected && <TouchKeyboard key={target.key} onClose={() => setTarget(null)} target={target.element} />}
  </TouchKeyboardContext.Provider>
}

// The hook intentionally shares the provider module's private context.
// eslint-disable-next-line react-refresh/only-export-components
export function useTouchKeyboard() {
  const context = useContext(TouchKeyboardContext)
  if (!context) throw new Error('useTouchKeyboard must be used within TouchKeyboardProvider.')
  return context
}

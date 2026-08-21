import { type ReactNode, useCallback, useEffect, useRef, useState } from 'react'
import { registerSW } from 'virtual:pwa-register'

const updateCheckIntervalMs = 15 * 60 * 1000
const idleActivationDelayMs = 5 * 60 * 1000
const idleCheckIntervalMs = 30 * 1000

type ApplyUpdate = () => Promise<void>

function hasMountedForm() {
  return document.querySelector('form') !== null
}

export function PwaUpdateProvider({ children }: { children: ReactNode }) {
  const [applyUpdate, setApplyUpdate] = useState<ApplyUpdate | null>(null)
  const [isApplying, setIsApplying] = useState(false)
  const [isOnline, setIsOnline] = useState(() => navigator.onLine)
  const [formMounted, setFormMounted] = useState(false)
  const registrationRef = useRef<ServiceWorkerRegistration | undefined>(undefined)
  const lastActivityAtRef = useRef(0)
  const reloadStartedRef = useRef(false)

  const requestUpdateCheck = useCallback(() => {
    if (navigator.onLine) {
      void registrationRef.current?.update()
    }
  }, [])

  const activateUpdate = useCallback(async () => {
    if (!applyUpdate || isApplying || !navigator.onLine || hasMountedForm()) {
      return
    }

    setIsApplying(true)
    reloadStartedRef.current = true
    try {
      await applyUpdate()
    } catch {
      reloadStartedRef.current = false
      setIsApplying(false)
    }
  }, [applyUpdate, isApplying])

  useEffect(() => {
    const updateServiceWorker = registerSW({
      immediate: true,
      onNeedRefresh() {
        setApplyUpdate(() => () => updateServiceWorker(true))
      },
      onRegisteredSW(_serviceWorkerUrl, registration) {
        registrationRef.current = registration
      },
    })

    const acceptTestOrCompatibilityUpdate = (event: Event) => {
      const callback = (event as CustomEvent<ApplyUpdate>).detail
      if (typeof callback === 'function') {
        setApplyUpdate(() => callback)
      }
    }

    window.addEventListener('family-dashboard:update-ready', acceptTestOrCompatibilityUpdate)
    return () => window.removeEventListener('family-dashboard:update-ready', acceptTestOrCompatibilityUpdate)
  }, [])

  useEffect(() => {
    lastActivityAtRef.current = Date.now()
    const recordActivity = () => {
      lastActivityAtRef.current = Date.now()
    }
    const updateOnlineState = () => {
      setIsOnline(navigator.onLine)
      if (navigator.onLine) requestUpdateCheck()
    }
    const checkWhenVisible = () => {
      if (document.visibilityState === 'visible') requestUpdateCheck()
    }

    window.addEventListener('pointerdown', recordActivity, { passive: true })
    window.addEventListener('keydown', recordActivity)
    window.addEventListener('online', updateOnlineState)
    window.addEventListener('offline', updateOnlineState)
    window.addEventListener('focus', requestUpdateCheck)
    document.addEventListener('visibilitychange', checkWhenVisible)
    const updateTimer = window.setInterval(requestUpdateCheck, updateCheckIntervalMs)

    return () => {
      window.removeEventListener('pointerdown', recordActivity)
      window.removeEventListener('keydown', recordActivity)
      window.removeEventListener('online', updateOnlineState)
      window.removeEventListener('offline', updateOnlineState)
      window.removeEventListener('focus', requestUpdateCheck)
      document.removeEventListener('visibilitychange', checkWhenVisible)
      window.clearInterval(updateTimer)
    }
  }, [requestUpdateCheck])

  useEffect(() => {
    const detectForms = () => setFormMounted(hasMountedForm())
    detectForms()
    const observer = new MutationObserver(detectForms)
    observer.observe(document.body, { childList: true, subtree: true })
    return () => observer.disconnect()
  }, [])

  useEffect(() => {
    if (!applyUpdate) return

    const idleTimer = window.setInterval(() => {
      const isIdle = Date.now() - lastActivityAtRef.current >= idleActivationDelayMs
      if (isIdle && navigator.onLine && !hasMountedForm() && !reloadStartedRef.current) {
        void activateUpdate()
      }
    }, idleCheckIntervalMs)

    return () => window.clearInterval(idleTimer)
  }, [activateUpdate, applyUpdate])

  const updateBlocked = formMounted || !isOnline
  const message = !isOnline
    ? 'A fresh version is ready and will be available when this display is online.'
    : formMounted
      ? 'A fresh version is ready. Finish or leave this form before updating.'
      : 'A fresh version is ready. Update now or let this idle display refresh safely.'

  return (
    <>
      {applyUpdate && (
        <aside aria-live="polite" className="update-banner" role="status">
          <span>{message}</span>
          <button
            disabled={updateBlocked || isApplying}
            onClick={() => void activateUpdate()}
            type="button"
          >
            {isApplying ? 'Updating…' : 'Update now'}
          </button>
        </aside>
      )}
      {children}
    </>
  )
}

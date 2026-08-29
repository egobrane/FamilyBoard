import { useEffect, useLayoutEffect, useRef, useState, type PointerEvent as ReactPointerEvent } from 'react'
import { Outlet, useLocation, useNavigate, useOutlet } from 'react-router'
import { NavigationBar } from './NavigationBar'
import { workspaceIndex, workspaceNavigationItems } from './workspaceNavigation'

const gestureIgnoreSelector = [
  'a',
  'button',
  'input',
  'select',
  'textarea',
  'label',
  '[contenteditable="true"]',
  '[role="dialog"]',
  '[data-workspace-swipe-ignore]',
].join(',')

type DragState = {
  pointerId: number
  startX: number
  startY: number
  lastX: number
  lastTime: number
  velocityX: number
  horizontal: boolean
}

function isGestureIgnored(target: EventTarget | null) {
  return target instanceof Element && target.closest(gestureIgnoreSelector) !== null
}

export function WorkspaceLayout() {
  const location = useLocation()
  const navigate = useNavigate()
  const outlet = useOutlet()
  const index = workspaceIndex(location.pathname)
  const previousIndex = useRef(index)
  const drag = useRef<DragState | null>(null)
  const suppressClick = useRef(false)
  const [motion, setMotion] = useState<{ path: string; direction: 'forward' | 'backward' | 'none' }>({
    path: location.pathname,
    direction: 'none',
  })
  const [dragOffset, setDragOffset] = useState<number | null>(null)

  useLayoutEffect(() => {
    if (motion.path === location.pathname) return
    const direction = index > previousIndex.current ? 'forward' : index < previousIndex.current ? 'backward' : 'none'
    previousIndex.current = index
    setMotion({ path: location.pathname, direction })
    setDragOffset(null)
  }, [index, location.pathname, motion.path])

  useEffect(() => {
    if (motion.path !== location.pathname || motion.direction === 'none') return
    const frame = window.requestAnimationFrame(() => {
      const main = document.querySelector<HTMLElement>('#main-content')
      main?.focus({ preventScroll: true })
    })
    return () => window.cancelAnimationFrame(frame)
  }, [location.pathname, motion])

  function resetDrag() {
    drag.current = null
    setDragOffset(null)
  }

  function onPointerDown(event: ReactPointerEvent<HTMLDivElement>) {
    if (event.button !== 0 || !event.isPrimary || isGestureIgnored(event.target)) return
    drag.current = {
      pointerId: event.pointerId,
      startX: event.clientX,
      startY: event.clientY,
      lastX: event.clientX,
      lastTime: event.timeStamp,
      velocityX: 0,
      horizontal: false,
    }
  }

  function onPointerMove(event: ReactPointerEvent<HTMLDivElement>) {
    const current = drag.current
    if (!current || current.pointerId !== event.pointerId) return
    const distanceX = event.clientX - current.startX
    const distanceY = event.clientY - current.startY

    if (!current.horizontal) {
      if (Math.abs(distanceY) > 12 && Math.abs(distanceY) >= Math.abs(distanceX)) {
        resetDrag()
        return
      }
      if (Math.abs(distanceX) < 12 || Math.abs(distanceX) <= Math.abs(distanceY) * 1.2) return
      current.horizontal = true
      event.currentTarget.setPointerCapture?.(event.pointerId)
    }

    event.preventDefault()
    const elapsed = Math.max(event.timeStamp - current.lastTime, 1)
    current.velocityX = (event.clientX - current.lastX) / elapsed
    current.lastX = event.clientX
    current.lastTime = event.timeStamp

    const atFirstPage = index === 0 && distanceX > 0
    const atLastPage = index === workspaceNavigationItems.length - 1 && distanceX < 0
    setDragOffset((atFirstPage || atLastPage) ? distanceX * 0.18 : distanceX)
  }

  function finishPointer(event: ReactPointerEvent<HTMLDivElement>) {
    const current = drag.current
    if (!current || current.pointerId !== event.pointerId) return
    if (!current.horizontal) {
      resetDrag()
      return
    }

    const distanceX = event.clientX - current.startX
    const threshold = Math.max(72, event.currentTarget.clientWidth * 0.12)
    const fastSwipe = Math.abs(current.velocityX) >= 0.45 && Math.abs(distanceX) >= 36
    const targetIndex = distanceX < 0 ? index + 1 : index - 1
    const shouldNavigate = (Math.abs(distanceX) >= threshold || fastSwipe)
      && targetIndex >= 0
      && targetIndex < workspaceNavigationItems.length

    suppressClick.current = true
    window.setTimeout(() => { suppressClick.current = false }, 0)
    resetDrag()
    if (shouldNavigate) navigate(workspaceNavigationItems[targetIndex].to)
  }

  return (
    <>
      <div
        className="workspace-viewport"
        data-dragging={dragOffset !== null ? 'true' : undefined}
        onClickCapture={(event) => {
          if (!suppressClick.current) return
          event.preventDefault()
          event.stopPropagation()
        }}
        onPointerCancel={resetDrag}
        onPointerDown={onPointerDown}
        onPointerMove={onPointerMove}
        onPointerUp={finishPointer}
      >
        <div
          className={`workspace-page workspace-page--${motion.direction}`}
          key={motion.path}
          style={dragOffset === null ? undefined : { transform: `translate3d(${dragOffset}px, 0, 0)` }}
        >
          {outlet ?? <Outlet />}
        </div>
      </div>
      <NavigationBar />
    </>
  )
}

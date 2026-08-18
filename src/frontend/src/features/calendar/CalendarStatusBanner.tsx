import type { ReactNode } from 'react'

export function CalendarStatusBanner({ kind = 'info', children }: {
  kind?: 'info' | 'warning' | 'error' | 'success'
  children: ReactNode
}) {
  return <div className={`calendar-status calendar-status--${kind}`} role={kind === 'error' ? 'alert' : 'status'}>{children}</div>
}

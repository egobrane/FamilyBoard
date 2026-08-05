import { useId, type PropsWithChildren, type ReactNode } from 'react'

interface DashboardCardProps extends PropsWithChildren {
  title: string
  eyebrow?: string
  action?: ReactNode
  className?: string
}

export function DashboardCard({ title, eyebrow, action, className = '', children }: DashboardCardProps) {
  const titleId = useId()

  return (
    <section className={`dashboard-card ${className}`.trim()} aria-labelledby={titleId}>
      <div className="dashboard-card__header">
        <div>
          {eyebrow && <p className="eyebrow">{eyebrow}</p>}
          <h2 id={titleId}>{title}</h2>
        </div>
        {action}
      </div>
      {children}
    </section>
  )
}

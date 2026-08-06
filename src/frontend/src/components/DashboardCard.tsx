import { useId, type CSSProperties, type PropsWithChildren, type ReactNode } from 'react'

interface DashboardCardProps extends PropsWithChildren {
  title: string
  eyebrow?: string
  action?: ReactNode
  className?: string
  id?: string
  style?: CSSProperties
}

export function DashboardCard({ title, eyebrow, action, className = '', id, style, children }: DashboardCardProps) {
  const titleId = useId()

  return (
    <section className={`dashboard-card ${className}`.trim()} aria-labelledby={titleId} id={id} style={style}>
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

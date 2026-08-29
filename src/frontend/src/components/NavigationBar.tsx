import { Link, useLocation } from 'react-router'
import { workspaceNavigationItems } from './workspaceNavigation'

export function NavigationBar() {
  const location = useLocation()
  return (
    <nav className="navigation" aria-label="Primary navigation">
      {workspaceNavigationItems.map((item) => {
        const current = item.id === 'home'
          ? location.pathname === '/' && location.hash === ''
          : location.pathname === `/${item.id}`
        return (
          <Link
            aria-current={current ? 'page' : undefined}
            className={`navigation__item ${current ? 'navigation__item--current' : ''}`}
            key={item.label}
            to={item.to}
          >
            <span className="navigation__icon" aria-hidden="true">{item.icon}</span>
            <span>{item.label}</span>
          </Link>
        )
      })}
    </nav>
  )
}

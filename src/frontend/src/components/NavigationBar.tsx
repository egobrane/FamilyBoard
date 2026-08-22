import { Link, useLocation } from 'react-router'

const navigationItems = [
  { id: 'home', label: 'Home', icon: '⌂', to: '/' },
  { id: 'calendar', label: 'Calendar', icon: '□', to: '/calendar' },
  { id: 'chores', label: 'Chores', icon: '✓', to: '/chores' },
  { id: 'rewards', label: 'Rewards', icon: '★', to: '/#rewards-preview' },
] as const

export function NavigationBar() {
  const location = useLocation()
  return (
    <nav className="navigation" aria-label="Primary navigation">
      {navigationItems.map((item) => {
        const current = item.id === 'calendar' || item.id === 'chores'
          ? location.pathname === `/${item.id}`
          : item.id === 'home'
            ? location.pathname === '/' && location.hash === ''
            : location.pathname === '/' && location.hash === `#${item.id}-preview`
        return (
          <Link
            aria-current={current ? item.id === 'chores' || item.id === 'rewards' ? 'location' : 'page' : undefined}
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

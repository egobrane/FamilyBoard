const navigationItems = [
  { label: 'Home', icon: '⌂', current: true },
  { label: 'Calendar', icon: '□', current: false },
  { label: 'Chores', icon: '✓', current: false },
  { label: 'Rewards', icon: '★', current: false },
] as const

export function NavigationBar() {
  return (
    <nav className="navigation" aria-label="Primary navigation">
      {navigationItems.map((item) => (
        <span
          className={`navigation__item ${item.current ? 'navigation__item--current' : ''}`}
          aria-current={item.current ? 'page' : undefined}
          aria-disabled={item.current ? undefined : true}
          key={item.label}
        >
          <span className="navigation__icon" aria-hidden="true">{item.icon}</span>
          <span>{item.label}</span>
          {!item.current && <span className="sr-only"> — coming soon</span>}
        </span>
      ))}
    </nav>
  )
}

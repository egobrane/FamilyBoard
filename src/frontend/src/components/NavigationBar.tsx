import { useEffect, useState } from 'react'

const navigationItems = [
  { id: 'home', label: 'Home', icon: '⌂', href: '#main-content' },
  { id: 'calendar', label: 'Calendar', icon: '□', href: '#calendar-preview' },
  { id: 'chores', label: 'Chores', icon: '✓', href: '#chores-preview' },
  { id: 'rewards', label: 'Rewards', icon: '★', href: '#rewards-preview' },
] as const

type NavigationItemId = (typeof navigationItems)[number]['id']

function getItemIdFromHash(): NavigationItemId {
  return navigationItems.find((item) => item.href === window.location.hash)?.id ?? 'home'
}

export function NavigationBar() {
  const [currentItemId, setCurrentItemId] = useState<NavigationItemId>(getItemIdFromHash)

  useEffect(() => {
    const updateCurrentItem = () => setCurrentItemId(getItemIdFromHash())
    window.addEventListener('hashchange', updateCurrentItem)

    return () => window.removeEventListener('hashchange', updateCurrentItem)
  }, [])

  return (
    <nav className="navigation" aria-label="Primary navigation">
      {navigationItems.map((item) => (
        <a
          className={`navigation__item ${item.id === currentItemId ? 'navigation__item--current' : ''}`}
          aria-current={item.id === currentItemId ? 'location' : undefined}
          href={item.href}
          key={item.label}
          onClick={() => setCurrentItemId(item.id)}
        >
          <span className="navigation__icon" aria-hidden="true">{item.icon}</span>
          <span>{item.label}</span>
        </a>
      ))}
    </nav>
  )
}

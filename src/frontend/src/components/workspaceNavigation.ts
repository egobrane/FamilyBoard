export const workspaceNavigationItems = [
  { id: 'home', label: 'Home', icon: '⌂', to: '/' },
  { id: 'calendar', label: 'Calendar', icon: '□', to: '/calendar' },
  { id: 'tasks', label: 'Tasks', icon: '☑', to: '/tasks' },
  { id: 'chores', label: 'Chores', icon: '✓', to: '/chores' },
  { id: 'rewards', label: 'Rewards', icon: '★', to: '/rewards' },
] as const

export function workspaceIndex(pathname: string) {
  return workspaceNavigationItems.findIndex((item) => item.to === pathname)
}

import { useRef, useState, type KeyboardEvent } from 'react'
import { Link } from 'react-router'

function initials(displayName: string) {
  const parts = displayName.trim().split(/\s+/).filter(Boolean)
  return parts.slice(0, 2).map((part) => part.charAt(0).toUpperCase()).join('') || '?'
}

interface AccountMenuProps {
  displayName: string
  canSwitchHouseholds: boolean
  householdSettingsPath?: string
  isBusy: boolean
  onLogout: () => Promise<void>
}

export function AccountMenu({
  displayName,
  canSwitchHouseholds,
  householdSettingsPath,
  isBusy,
  onLogout,
}: AccountMenuProps) {
  const [isOpen, setIsOpen] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const buttonRef = useRef<HTMLButtonElement>(null)

  const handleKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.key === 'Escape') {
      setIsOpen(false)
      buttonRef.current?.focus()
    }
  }

  const handleLogout = async () => {
    setError(null)
    try {
      await onLogout()
      setIsOpen(false)
    } catch {
      setError('Sign out could not be completed. Try again.')
    }
  }

  return (
    <div className="account-menu" onKeyDown={handleKeyDown}>
      <button
        aria-expanded={isOpen}
        aria-haspopup="menu"
        aria-label={`Account menu for ${displayName}`}
        className="avatar account-menu__trigger"
        onClick={() => setIsOpen((open) => !open)}
        ref={buttonRef}
        type="button"
      >
        {initials(displayName)}
      </button>
      {isOpen && (
        <div aria-label="Account actions" className="account-menu__panel" role="menu">
          <strong>{displayName}</strong>
          {error && <span className="account-menu__error" role="alert">{error}</span>}
          {canSwitchHouseholds && (
            <Link onClick={() => setIsOpen(false)} role="menuitem" to="/households/select">
              Switch household
            </Link>
          )}
          {householdSettingsPath && (
            <Link onClick={() => setIsOpen(false)} role="menuitem" to={householdSettingsPath}>
              Household settings
            </Link>
          )}
          <button
            disabled={isBusy}
            onClick={() => void handleLogout()}
            role="menuitem"
            type="button"
          >
            {isBusy ? 'Signing out…' : 'Sign out'}
          </button>
        </div>
      )}
    </div>
  )
}

import { useRef, useState, type KeyboardEvent } from 'react'
import { Link } from 'react-router'
import { MemberAvatar } from './MemberAvatar'
import type { HouseholdMemberPhotoResponse } from '../lib/api'
import { useTouchKeyboard, type TouchKeyboardPreference } from '../features/touch-keyboard/TouchKeyboardProvider'

const keyboardPreferenceLabels: Record<TouchKeyboardPreference, string> = {
  auto: 'Auto',
  on: 'On',
  off: 'Off',
}

function nextKeyboardPreference(current: TouchKeyboardPreference): TouchKeyboardPreference {
  if (current === 'auto') return 'on'
  if (current === 'on') return 'off'
  return 'auto'
}

interface AccountMenuProps {
  displayName: string
  avatarColor: string | null
  photo: HouseholdMemberPhotoResponse | null
  canSwitchHouseholds: boolean
  householdSettingsPath?: string
  parentAccessPath?: string
  isSharedDisplay: boolean
  isParentElevated: boolean
  isBusy: boolean
  onLogout: () => Promise<void>
  onLockParentAccess: () => Promise<void>
}

export function AccountMenu({
  displayName,
  avatarColor,
  photo,
  canSwitchHouseholds,
  householdSettingsPath,
  parentAccessPath,
  isSharedDisplay,
  isParentElevated,
  isBusy,
  onLogout,
  onLockParentAccess,
}: AccountMenuProps) {
  const { preference: keyboardPreference, setPreference: setKeyboardPreference } = useTouchKeyboard()
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
        <MemberAvatar member={{ displayName, avatarColor, photo }} />
      </button>
      {isOpen && (
        <div aria-label="Account actions" className="account-menu__panel" role="menu">
          <strong>{displayName}</strong>
          {isSharedDisplay && <span className="shared-display-badge">Shared display</span>}
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
          {parentAccessPath && (
            <Link onClick={() => setIsOpen(false)} role="menuitem" to={parentAccessPath}>
              Parent access
            </Link>
          )}
          <button
            aria-label={`On-screen keyboard setting: ${keyboardPreferenceLabels[keyboardPreference]}. Activate to change.`}
            onClick={() => setKeyboardPreference(nextKeyboardPreference(keyboardPreference))}
            role="menuitem"
            type="button"
          >
            On-screen keyboard: {keyboardPreferenceLabels[keyboardPreference]}
          </button>
          {isSharedDisplay && isParentElevated && (
            <button
              disabled={isBusy}
              onClick={() => void onLockParentAccess().then(() => setIsOpen(false))}
              role="menuitem"
              type="button"
            >
              Lock parent controls
            </button>
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

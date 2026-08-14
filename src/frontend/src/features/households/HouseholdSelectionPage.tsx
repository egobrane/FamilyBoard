import { useState } from 'react'
import { useNavigate } from 'react-router'
import { useAuthentication } from '../authentication/AuthenticationContext'

export function HouseholdSelectionPage() {
  const { state, selectHousehold, isMutating } = useAuthentication()
  const navigate = useNavigate()
  const [error, setError] = useState<string | null>(null)

  if (state.status !== 'authenticated') {
    return null
  }

  const choose = async (householdId: string) => {
    setError(null)
    try {
      await selectHousehold(householdId)
      navigate('/', { replace: true })
    } catch {
      setError('That household could not be opened. Refresh your memberships and try again.')
    }
  }

  return (
    <main className="setup-page" id="main-content">
      <section className="setup-card household-selector" aria-labelledby="household-selector-title">
        <p className="eyebrow">Choose your view</p>
        <h1 id="household-selector-title">Which household are you opening?</h1>
        <p className="setup-card__lede">This choice stays with this signed-in device.</p>
        {error && <p className="form-error-summary" role="alert">{error}</p>}
        <div className="household-options">
          {state.currentUser.households.map((household) => (
            <button
              aria-current={household.id === state.currentUser.selectedHouseholdId ? 'true' : undefined}
              className="household-option"
              disabled={isMutating}
              key={household.id}
              onClick={() => void choose(household.id)}
              type="button"
            >
              <span className="household-option__mark" aria-hidden="true">⌂</span>
              <span>
                <strong>{household.name}</strong>
                <small>{household.role === 'adult' ? 'Adult access' : 'Household member'}</small>
              </span>
              <span aria-hidden="true">›</span>
            </button>
          ))}
        </div>
      </section>
    </main>
  )
}

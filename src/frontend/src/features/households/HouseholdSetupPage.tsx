import { useRef, useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router'
import { ApiError, type CreateHouseholdRequest } from '../../lib/api'
import { useAuthentication } from '../authentication/AuthenticationContext'

const weekDays = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']

function browserDefaults() {
  const locale = navigator.language || 'en-US'
  const timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC'
  const weekStartsOn = locale.toLowerCase().startsWith('en-us') ? 'Sunday' : 'Monday'
  return { locale, timeZone, weekStartsOn }
}

export function HouseholdSetupPage() {
  const defaults = browserDefaults()
  const { createHousehold, isMutating, state } = useAuthentication()
  const navigate = useNavigate()
  const errorSummaryRef = useRef<HTMLDivElement>(null)
  const [name, setName] = useState('')
  const [timeZone, setTimeZone] = useState(defaults.timeZone)
  const [locale, setLocale] = useState(defaults.locale)
  const [weekStartsOn, setWeekStartsOn] = useState(defaults.weekStartsOn)
  const [errors, setErrors] = useState<Record<string, string[]>>({})
  const [generalError, setGeneralError] = useState<string | null>(null)

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setErrors({})
    setGeneralError(null)
    const request: CreateHouseholdRequest = { name, timeZone, locale, weekStartsOn }

    try {
      await createHousehold(request)
      navigate('/', { replace: true })
    } catch (error) {
      if (error instanceof ApiError && error.problem.errors) {
        setErrors(error.problem.errors)
      } else {
        setGeneralError('Your household could not be created. Check your connection and try again.')
      }
      window.requestAnimationFrame(() => errorSummaryRef.current?.focus())
    }
  }

  const displayName = state.status === 'authenticated' ? state.currentUser.user.displayName : 'there'

  return (
    <main className="setup-page" id="main-content">
      <form className="setup-card" noValidate onSubmit={(event) => void submit(event)}>
        <p className="eyebrow">First household</p>
        <h1>Welcome, {displayName}.</h1>
        <p className="setup-card__lede">Give your family dashboard a name. You can refine regional settings later.</p>

        {(generalError || Object.keys(errors).length > 0) && (
          <div className="form-error-summary" ref={errorSummaryRef} role="alert" tabIndex={-1}>
            <strong>We could not save the household.</strong>
            <span>{generalError ?? 'Review the highlighted fields and try again.'}</span>
          </div>
        )}

        <label className="form-field">
          <span>Household name</span>
          <input
            aria-describedby={errors.name ? 'household-name-error' : undefined}
            aria-invalid={errors.name ? 'true' : undefined}
            autoComplete="organization"
            autoFocus
            maxLength={120}
            onChange={(event) => setName(event.target.value)}
            placeholder="Bamford-Fahie-Waltz Family"
            required
            value={name}
          />
          {errors.name && <small className="field-error" id="household-name-error">{errors.name[0]}</small>}
        </label>

        <div className="regional-fields">
          <label className="form-field">
            <span>Time zone</span>
            <input
              aria-describedby={errors.timeZone ? 'time-zone-error' : undefined}
              aria-invalid={errors.timeZone ? 'true' : undefined}
              onChange={(event) => setTimeZone(event.target.value)}
              required
              value={timeZone}
            />
            {errors.timeZone && <small className="field-error" id="time-zone-error">{errors.timeZone[0]}</small>}
          </label>

          <label className="form-field">
            <span>Locale</span>
            <input
              aria-describedby={errors.locale ? 'locale-error' : undefined}
              aria-invalid={errors.locale ? 'true' : undefined}
              onChange={(event) => setLocale(event.target.value)}
              required
              value={locale}
            />
            {errors.locale && <small className="field-error" id="locale-error">{errors.locale[0]}</small>}
          </label>

          <label className="form-field">
            <span>Week starts on</span>
            <select
              aria-describedby={errors.weekStartsOn ? 'week-start-error' : undefined}
              aria-invalid={errors.weekStartsOn ? 'true' : undefined}
              onChange={(event) => setWeekStartsOn(event.target.value)}
              value={weekStartsOn}
            >
              {weekDays.map((day) => <option key={day}>{day}</option>)}
            </select>
            {errors.weekStartsOn && <small className="field-error" id="week-start-error">{errors.weekStartsOn[0]}</small>}
          </label>
        </div>

        <button className="primary-action" disabled={isMutating} type="submit">
          {isMutating ? 'Creating your household…' : 'Create household'}
        </button>
      </form>
    </main>
  )
}

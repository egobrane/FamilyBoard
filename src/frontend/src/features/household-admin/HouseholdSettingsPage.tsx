import { useCallback, useEffect, useRef, useState, type FormEvent, type RefObject } from 'react'
import { useParams } from 'react-router'
import {
  ApiError,
  getHousehold,
  updateHousehold,
  type HouseholdResponse,
  type UpdateHouseholdRequest,
} from '../../lib/api'
import { useAuthentication } from '../authentication/AuthenticationContext'

const weekDays = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']

type LoadState =
  | { status: 'loading' }
  | { status: 'ready'; household: HouseholdResponse }
  | { status: 'notFound' | 'forbidden' | 'failed' }

export function HouseholdSettingsPage() {
  const { householdId = '' } = useParams()
  const { refreshSilently } = useAuthentication()
  const errorSummaryRef = useRef<HTMLDivElement>(null)
  const [loadState, setLoadState] = useState<LoadState>({ status: 'loading' })
  const [isSaving, setIsSaving] = useState(false)
  const [errors, setErrors] = useState<Record<string, string[]>>({})
  const [success, setSuccess] = useState<string | null>(null)

  const load = useCallback(async () => {
    setLoadState({ status: 'loading' })
    try {
      setLoadState({ status: 'ready', household: await getHousehold(householdId) })
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        await refreshSilently()
        return
      }
      if (error instanceof ApiError && error.status === 404) {
        setLoadState({ status: 'notFound' })
      } else if (error instanceof ApiError && error.status === 403) {
        setLoadState({ status: 'forbidden' })
      } else {
        setLoadState({ status: 'failed' })
      }
    }
  }, [householdId, refreshSilently])

  useEffect(() => {
    const timer = window.setTimeout(() => void load(), 0)
    return () => window.clearTimeout(timer)
  }, [load])

  if (loadState.status !== 'ready') {
    const content = {
      loading: ['Loading settings…', 'Retrieving the selected household configuration.'],
      notFound: ['Household not found', 'This household is unavailable to the current account.'],
      forbidden: ['Adult access required', 'An adult household account must edit these settings.'],
      failed: ['Settings could not be loaded', 'Check your connection and try again.'],
    }[loadState.status]
    return (
      <section className="admin-panel admin-status" role={loadState.status === 'loading' ? 'status' : 'alert'}>
        <h3>{content[0]}</h3>
        <p>{content[1]}</p>
        {loadState.status === 'failed' && <button className="secondary-action" onClick={() => void load()} type="button">Try again</button>}
      </section>
    )
  }

  return (
    <HouseholdSettingsForm
      key={`${loadState.household.name}:${loadState.household.timeZone}:${loadState.household.locale}:${loadState.household.weekStartsOn}`}
      errorSummaryRef={errorSummaryRef}
      errors={errors}
      household={loadState.household}
      isSaving={isSaving}
      onSubmit={async (request) => {
        setErrors({})
        setSuccess(null)
        setIsSaving(true)
        try {
          const household = await updateHousehold(householdId, request)
          setLoadState({ status: 'ready', household })
          setSuccess('Household settings saved.')
          await refreshSilently()
        } catch (error) {
          if (error instanceof ApiError && error.status === 401) {
            await refreshSilently()
          } else if (error instanceof ApiError && error.problem.errors) {
            setErrors(error.problem.errors)
            window.requestAnimationFrame(() => errorSummaryRef.current?.focus())
          } else if (error instanceof ApiError && error.status === 404) {
            setLoadState({ status: 'notFound' })
          } else if (error instanceof ApiError && error.status === 403) {
            setLoadState({ status: 'forbidden' })
          } else {
            setErrors({ request: ['Settings could not be saved. Check your connection and try again.'] })
            window.requestAnimationFrame(() => errorSummaryRef.current?.focus())
          }
        } finally {
          setIsSaving(false)
        }
      }}
      success={success}
    />
  )
}

interface HouseholdSettingsFormProps {
  household: HouseholdResponse
  errors: Record<string, string[]>
  errorSummaryRef: RefObject<HTMLDivElement | null>
  isSaving: boolean
  success: string | null
  onSubmit: (request: UpdateHouseholdRequest) => Promise<void>
}

function HouseholdSettingsForm({
  household,
  errors,
  errorSummaryRef,
  isSaving,
  success,
  onSubmit,
}: HouseholdSettingsFormProps) {
  const [name, setName] = useState(household.name)
  const [timeZone, setTimeZone] = useState(household.timeZone)
  const [locale, setLocale] = useState(household.locale)
  const [weekStartsOn, setWeekStartsOn] = useState(
    household.weekStartsOn.charAt(0).toUpperCase() + household.weekStartsOn.slice(1),
  )

  const submit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    void onSubmit({ name, timeZone, locale, weekStartsOn })
  }

  return (
    <section className="admin-panel" aria-labelledby="household-settings-title">
      <div className="admin-panel__heading">
        <div>
          <p className="eyebrow">Display and region</p>
          <h3 id="household-settings-title">Household settings</h3>
        </div>
        {success && <p className="save-success" role="status">{success}</p>}
      </div>
      <form className="admin-form" noValidate onSubmit={submit}>
        {Object.keys(errors).length > 0 && (
          <div className="form-error-summary" ref={errorSummaryRef} role="alert" tabIndex={-1}>
            <strong>We could not save these settings.</strong>
            <span>{errors.request?.[0] ?? 'Review the highlighted fields and try again.'}</span>
          </div>
        )}
        <label className="form-field">
          <span>Household name</span>
          <input aria-describedby={errors.name ? 'settings-name-error' : undefined} aria-invalid={errors.name ? 'true' : undefined} maxLength={120} onChange={(event) => setName(event.target.value)} required value={name} />
          {errors.name && <small className="field-error" id="settings-name-error">{errors.name[0]}</small>}
        </label>
        <div className="regional-fields">
          <label className="form-field">
            <span>Time zone</span>
            <input aria-describedby={errors.timeZone ? 'settings-time-zone-error' : undefined} aria-invalid={errors.timeZone ? 'true' : undefined} maxLength={100} onChange={(event) => setTimeZone(event.target.value)} required value={timeZone} />
            {errors.timeZone && <small className="field-error" id="settings-time-zone-error">{errors.timeZone[0]}</small>}
          </label>
          <label className="form-field">
            <span>Locale</span>
            <input aria-describedby={errors.locale ? 'settings-locale-error' : undefined} aria-invalid={errors.locale ? 'true' : undefined} maxLength={20} onChange={(event) => setLocale(event.target.value)} required value={locale} />
            {errors.locale && <small className="field-error" id="settings-locale-error">{errors.locale[0]}</small>}
          </label>
          <label className="form-field">
            <span>Week starts on</span>
            <select aria-describedby={errors.weekStartsOn ? 'settings-week-start-error' : undefined} aria-invalid={errors.weekStartsOn ? 'true' : undefined} onChange={(event) => setWeekStartsOn(event.target.value)} value={weekStartsOn}>
              {weekDays.map((day) => <option key={day} value={day}>{day}</option>)}
            </select>
            {errors.weekStartsOn && <small className="field-error" id="settings-week-start-error">{errors.weekStartsOn[0]}</small>}
          </label>
        </div>
        <div className="form-actions">
          <button className="primary-action" disabled={isSaving} type="submit">{isSaving ? 'Saving…' : 'Save settings'}</button>
        </div>
      </form>
    </section>
  )
}

import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { Link, useParams } from 'react-router'
import {
  ApiError, getDashboardAppearance, removeDashboardPhoto, resolveApiUrl,
  updateDashboardAppearance, uploadDashboardPhoto, type DashboardAppearanceResponse,
} from '../../lib/api'

export function HouseholdAppearancePage() {
  const { householdId = '' } = useParams()
  const [appearance, setAppearance] = useState<DashboardAppearanceResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const load = useCallback(async () => {
    setError(null)
    try { setAppearance(await getDashboardAppearance(householdId)) }
    catch { setError('Appearance settings could not be loaded.') }
  }, [householdId])
  useEffect(() => {
    const timer = window.setTimeout(() => void load(), 0)
    return () => window.clearTimeout(timer)
  }, [load])

  if (!appearance) return <section className="admin-panel admin-status" role={error ? 'alert' : 'status'}><h3>{error ?? 'Loading appearance…'}</h3>{error && <button className="secondary-action" onClick={() => void load()} type="button">Try again</button>}</section>

  const save = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    setBusy(true); setError(null); setSuccess(null)
    try {
      const updated = await updateDashboardAppearance(householdId, {
        greetingTitle: String(form.get('greetingTitle') ?? '') || null,
        greetingMessage: String(form.get('greetingMessage') ?? '') || null,
        photoFocalX: Number(form.get('photoFocalX')),
        photoFocalY: Number(form.get('photoFocalY')),
        expectedVersion: appearance.version,
      })
      setAppearance(updated); setSuccess('Dashboard appearance saved.')
    } catch (reason) { setError(message(reason, 'Appearance could not be saved.')) }
    finally { setBusy(false) }
  }

  return (
    <section className="admin-panel appearance-settings" aria-labelledby="appearance-title">
      <div className="admin-panel__heading"><div><p className="eyebrow">Household appearance</p><h3 id="appearance-title">Dashboard welcome card</h3></div><Link className="secondary-action" to={`/households/${householdId}/settings`}>Back to settings</Link></div>
      <p className="admin-panel__lede">Choose the private family photo and greeting shown for this household. The time-of-day greeting is automatic unless you provide a custom title.</p>
      {error && <p className="form-error-summary" role="alert">{error}</p>}
      {success && <p className="save-success" role="status">{success}</p>}
      <div className="appearance-preview" style={appearance.photo ? {
        backgroundImage: `linear-gradient(100deg, rgb(25 36 33 / 78%), rgb(25 36 33 / 58%)), url("${resolveApiUrl(appearance.photo.mediumUrl)}")`,
        backgroundPosition: `center, ${appearance.photoFocalX * 100}% ${appearance.photoFocalY * 100}%`,
      } : undefined}>
        <strong>{appearance.greetingTitle || 'Automatic time-of-day greeting'}</strong>
        <span>{appearance.greetingMessage || 'Your family message will appear here.'}</span>
      </div>
      <div className="appearance-photo-actions">
        <label className="form-field"><span>Family photo</span><input accept="image/jpeg,image/png,image/webp" disabled={busy} type="file" onChange={async (event) => {
          const photo = event.target.files?.[0]; if (!photo) return
          setBusy(true); setError(null); setSuccess(null)
          try { setAppearance(await uploadDashboardPhoto(householdId, photo)); setSuccess('Family photo uploaded privately.') }
          catch (reason) { setError(message(reason, 'The photo could not be uploaded.')) }
          finally { setBusy(false); event.target.value = '' }
        }} /><small>JPEG, PNG, or WebP; up to 10 MB.</small></label>
        {appearance.photo && <button className="danger-action" disabled={busy} onClick={async () => {
          if (!window.confirm('Remove this household photo and restore the demonstration fallback?')) return
          setBusy(true); setError(null)
          try { setAppearance(await removeDashboardPhoto(householdId)); setSuccess('Family photo removed.') }
          catch { setError('The photo could not be removed.') } finally { setBusy(false) }
        }} type="button">Remove photo</button>}
      </div>
      <form className="admin-form" onSubmit={save}>
        <label className="form-field"><span>Custom greeting title (optional)</span><input defaultValue={appearance.greetingTitle ?? ''} maxLength={80} name="greetingTitle" /></label>
        <label className="form-field"><span>Family message (optional)</span><textarea defaultValue={appearance.greetingMessage ?? ''} maxLength={240} name="greetingMessage" rows={3} /></label>
        <div className="regional-fields">
          <label className="form-field"><span>Horizontal focus</span><input defaultValue={appearance.photoFocalX} max="1" min="0" name="photoFocalX" step="0.01" type="range" /></label>
          <label className="form-field"><span>Vertical focus</span><input defaultValue={appearance.photoFocalY} max="1" min="0" name="photoFocalY" step="0.01" type="range" /></label>
        </div>
        <div className="form-actions"><button className="primary-action" disabled={busy} type="submit">{busy ? 'Saving…' : 'Save appearance'}</button></div>
      </form>
    </section>
  )
}

function message(error: unknown, fallback: string) {
  return error instanceof ApiError ? error.problem.errors?.photo?.[0] ?? error.problem.title : fallback
}

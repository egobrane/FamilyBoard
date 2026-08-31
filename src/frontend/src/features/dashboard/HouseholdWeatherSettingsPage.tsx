import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import { Link, useParams } from 'react-router'
import { ApiError, getWeatherSettings, removeWeatherSettings, updateWeatherSettings, type WeatherSettingsResponse } from '../../lib/api'

export function HouseholdWeatherSettingsPage() {
  const { householdId = '' } = useParams()
  const [settings, setSettings] = useState<WeatherSettingsResponse | undefined>()
  const [loaded, setLoaded] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const formRef = useRef<HTMLFormElement>(null)
  const load = useCallback(async () => {
    try { setSettings(await getWeatherSettings(householdId)); setLoaded(true) }
    catch { setError('Weather settings could not be loaded.'); setLoaded(true) }
  }, [householdId])
  useEffect(() => {
    const timer = window.setTimeout(() => void load(), 0)
    return () => window.clearTimeout(timer)
  }, [load])
  if (!loaded) return <section className="admin-panel admin-status" role="status"><h3>Loading weather settings…</h3></section>

  const save = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault(); const form = new FormData(event.currentTarget)
    setBusy(true); setError(null); setSuccess(null)
    try {
      const updated = await updateWeatherSettings(householdId, {
        latitude: Number(form.get('latitude')), longitude: Number(form.get('longitude')),
        locationLabel: String(form.get('locationLabel') ?? ''),
        temperatureUnit: String(form.get('temperatureUnit')) as 'auto' | 'fahrenheit' | 'celsius',
        expectedVersion: settings?.version ?? null,
      })
      setSettings(updated); setSuccess('Weather location saved.')
    } catch (reason) {
      setError(reason instanceof ApiError ? reason.problem.title : 'Weather settings could not be saved.')
    } finally { setBusy(false) }
  }

  return (
    <section className="admin-panel" aria-labelledby="weather-settings-title">
      <div className="admin-panel__heading"><div><p className="eyebrow">Local forecast</p><h3 id="weather-settings-title">Weather location</h3></div><Link className="secondary-action" to={`/households/${householdId}/settings`}>Back to settings</Link></div>
      <p className="admin-panel__lede">Use an approximate location. Coordinates are rounded and remain backend-only; Family Dashboard does not track background location.</p>
      {error && <p className="form-error-summary" role="alert">{error}</p>}{success && <p className="save-success" role="status">{success}</p>}
      <form className="admin-form" key={settings?.version ?? 'new'} onSubmit={save} ref={formRef}>
        <label className="form-field"><span>Location label</span><input defaultValue={settings?.locationLabel ?? ''} maxLength={100} name="locationLabel" placeholder="Home" required /></label>
        <div className="regional-fields">
          <label className="form-field"><span>Latitude</span><input defaultValue={settings?.latitude ?? ''} max="90" min="-90" name="latitude" required step="0.00001" type="number" /></label>
          <label className="form-field"><span>Longitude</span><input defaultValue={settings?.longitude ?? ''} max="180" min="-180" name="longitude" required step="0.00001" type="number" /></label>
          <label className="form-field"><span>Temperature</span><select defaultValue={settings?.temperatureUnit ?? 'auto'} name="temperatureUnit"><option value="auto">Household default (°F in US)</option><option value="fahrenheit">Fahrenheit</option><option value="celsius">Celsius</option></select></label>
        </div>
        <div className="form-actions form-actions--spread">
          <button className="secondary-action" disabled={busy} onClick={() => {
            if (!navigator.geolocation) { setError('This browser does not provide location access.'); return }
            navigator.geolocation.getCurrentPosition(position => {
              const form = formRef.current
              const latitude = form?.elements.namedItem('latitude') as HTMLInputElement | null
              const longitude = form?.elements.namedItem('longitude') as HTMLInputElement | null
              if (latitude) latitude.value = position.coords.latitude.toFixed(4)
              if (longitude) longitude.value = position.coords.longitude.toFixed(4)
              setSuccess('Approximate browser location added. Save to confirm it.')
            }, () => setError('Location permission was not granted.'), { enableHighAccuracy: false, maximumAge: 300000, timeout: 10000 })
          }} type="button">Use approximate location</button>
          <span><button className="primary-action" disabled={busy} type="submit">{busy ? 'Saving…' : 'Save weather location'}</button>{settings && <button className="danger-action" disabled={busy} onClick={async () => {
            if (!window.confirm('Remove the household weather location?')) return
            setBusy(true); try { await removeWeatherSettings(householdId); setSettings(undefined); setSuccess('Weather location removed.') } catch { setError('Weather location could not be removed.') } finally { setBusy(false) }
          }} type="button">Remove</button>}</span>
        </div>
      </form>
      <p className="weather-attribution">Forecast data is provided by the National Weather Service.</p>
    </section>
  )
}

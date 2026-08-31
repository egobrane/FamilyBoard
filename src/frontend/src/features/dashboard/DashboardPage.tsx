import { useEffect, useRef, useState, type CSSProperties } from 'react'
import { Link } from 'react-router'
import { DashboardCard } from '../../components/DashboardCard'
import { getDashboardAppearance, getHouseholdWeather, resolveApiUrl, type DashboardAppearanceResponse, type HouseholdWeatherResponse } from '../../lib/api'
import { useAuthentication } from '../authentication/AuthenticationContext'
import { DashboardCalendarCard } from '../calendar/DashboardCalendarCard'
import { DashboardChoresCard } from '../chores/DashboardChoresCard'
import { DashboardRewardsCard } from '../rewards/DashboardRewardsCard'
import { DashboardTasksCard } from '../tasks/DashboardTasksCard'
import { demoHouseholdPhotoUrl } from './mockDashboardData'

export function DashboardPage() {
  const { state } = useAuthentication()
  const householdId = state.status === 'authenticated' ? state.currentUser.selectedHouseholdId : null
  const [appearance, setAppearance] = useState<DashboardAppearanceResponse | null>(null)
  const [weather, setWeather] = useState<HouseholdWeatherResponse | null>(null)
  const [weatherFailed, setWeatherFailed] = useState(false)

  useEffect(() => {
    if (!householdId) return
    let active = true
    void getDashboardAppearance(householdId).then(value => { if (active) setAppearance(value) }).catch(() => undefined)
    void getHouseholdWeather(householdId).then(value => { if (active) { setWeather(value); setWeatherFailed(false) } }).catch(() => { if (active) setWeatherFailed(true) })
    return () => { active = false }
  }, [householdId])

  const greeting = appearance?.greetingTitle || automaticGreeting(appearance?.timeZone)
  const message = appearance?.greetingMessage || 'Here is what your family has coming up.'
  const uploadedPhoto = appearance?.photo ? resolveApiUrl(appearance.photo.largeUrl) : null
  const photoLayers = uploadedPhoto ? `url("${uploadedPhoto}"), url("${demoHouseholdPhotoUrl}")` : `url("${demoHouseholdPhotoUrl}")`
  const style = {
    '--household-photo': photoLayers,
    '--household-photo-position': `${(appearance?.photoFocalX ?? 0.5) * 100}% ${(appearance?.photoFocalY ?? 0.4) * 100}%`,
  } as CSSProperties

  return (
    <main className="dashboard" id="main-content" tabIndex={-1}>
      <DashboardCalendarCard />
      <DashboardCard className="welcome-card" eyebrow={greeting} title="Ready for a good day?" style={style}>
        <p>{message}</p>
        <WeatherWidget failed={weatherFailed} householdId={householdId} weather={weather} />
      </DashboardCard>
      <DashboardChoresCard />
      <DashboardTasksCard />
      <DashboardRewardsCard />
    </main>
  )
}

function WeatherWidget({ failed, householdId, weather }: { failed: boolean; householdId: string | null; weather: HouseholdWeatherResponse | null }) {
  const dialog = useRef<HTMLDialogElement>(null)
  if (failed) return <div className="weather-preview weather-preview--status" role="status"><WeatherIcon kind="cloudy" /><span><strong>Weather unavailable</strong><small>We’ll try again when this dashboard refreshes.</small></span></div>
  if (!weather) return <div className="weather-preview weather-preview--status" role="status"><WeatherIcon kind="cloudy" /><span><strong>Checking weather…</strong></span></div>
  if (weather.status === 'locationRequired') return <Link className="weather-preview weather-preview--status" to={`/households/${householdId}/settings/weather`}><WeatherIcon kind="clear" /><span><strong>Add local weather</strong><small>Choose an approximate household location.</small></span></Link>
  const current = weather.current
  const weatherLabel = current?.temperature === null || current?.temperature === undefined
    ? `Open weather forecast: ${current?.summary ?? 'forecast'}`
    : `Open weather forecast: ${current.temperature} degrees, ${current.summary ?? 'forecast'}`
  return <>
    <button aria-label={weatherLabel} className="weather-preview weather-preview--button" onClick={() => dialog.current?.showModal()} type="button">
      <WeatherIcon kind={current?.icon} /><span><strong>{current?.temperature === null || current?.temperature === undefined ? '—' : `${current.temperature}°`}</strong>{current?.summary ?? 'Forecast'}</span>{weather.isStale && <small>Last available</small>}
    </button>
    <dialog className="weather-dialog" onClick={(event) => { if (event.target === event.currentTarget) event.currentTarget.close() }} ref={dialog}>
      <div className="weather-dialog__content">
        <div className="weather-dialog__heading"><div><p className="eyebrow">{weather.locationLabel}</p><h3>Household forecast</h3></div><button aria-label="Close weather forecast" className="dialog-close" onClick={() => dialog.current?.close()} type="button">×</button></div>
        {weather.isStale && <p className="provider-warning" role="status">Showing the last available forecast while weather service recovers.</p>}
        <ol className="weather-forecast-list">{weather.forecast?.map(period => <li key={`${period.start}-${period.name}`}><WeatherIcon kind={period.icon} /><span><strong>{period.name}</strong><small>{period.summary}</small></span><b>{period.temperature === null ? '—' : `${period.temperature}°`}</b></li>)}</ol>
        <p className="weather-attribution">{weather.attribution}</p>
      </div>
    </dialog>
  </>
}

function WeatherIcon({ kind }: { kind?: string }) {
  const symbol = kind === 'rain' ? '☂' : kind === 'snow' ? '❄' : kind === 'thunderstorm' ? 'ϟ' : kind === 'cloudy' || kind === 'fog' ? '☁' : '☀'
  return <span aria-hidden="true" className="weather-preview__icon">{symbol}</span>
}

function automaticGreeting(timeZone?: string) {
  let hour = new Date().getHours()
  if (timeZone) {
    const value = new Intl.DateTimeFormat('en-US', { hour: 'numeric', hourCycle: 'h23', timeZone }).format(new Date())
    hour = Number.parseInt(value, 10)
  }
  return hour < 12 ? 'Good morning' : hour < 18 ? 'Good afternoon' : 'Good evening'
}

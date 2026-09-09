import { useEffect, useMemo, useRef, useState } from 'react'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'
import type { HouseholdWeatherRadarResponse } from '../../lib/api'

interface WeatherRadarProps {
  radar: HouseholdWeatherRadarResponse
}

interface TimeEnabledWmsOptions extends L.WMSOptions {
  time: string
}

interface TimeEnabledWmsParams extends L.WMSParams {
  time: string
}

export function WeatherRadar({ radar }: WeatherRadarProps) {
  const mapElement = useRef<HTMLDivElement>(null)
  const radarLayer = useRef<L.TileLayer.WMS | null>(null)
  const frames = useMemo(() => createRadarFrames(new Date()), [])
  const [frameIndex, setFrameIndex] = useState(frames.length - 1)
  const [playing, setPlaying] = useState(false)

  useEffect(() => {
    if (!mapElement.current || radar.status !== 'available' || radar.centerLatitude === null
      || radar.centerLongitude === null || !radar.wmsUrl || !radar.wmsLayer) return

    const center: L.LatLngExpression = [radar.centerLatitude, radar.centerLongitude]
    const map = L.map(mapElement.current, {
      center,
      zoom: 7,
      minZoom: 4,
      maxZoom: 12,
      zoomControl: true,
    })
    L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '&copy; OpenStreetMap contributors',
      className: 'weather-radar__base-tiles',
      maxZoom: 19,
    }).addTo(map)
    const layerOptions: TimeEnabledWmsOptions = {
      layers: radar.wmsLayer,
      format: 'image/png',
      transparent: true,
      opacity: 0.72,
      className: 'weather-radar__overlay',
      attribution: 'Radar: NOAA/National Weather Service',
      time: frames[frameIndex].toISOString(),
    }
    const layer = L.tileLayer.wms(radar.wmsUrl, layerOptions).addTo(map)
    radarLayer.current = layer
    L.circleMarker(center, {
      radius: 6,
      color: '#fffdf8',
      fillColor: '#26312f',
      fillOpacity: 1,
      weight: 2,
    }).addTo(map).bindTooltip('Approximate household area')

    const refresh = window.setInterval(() => layer.redraw(), radar.refreshIntervalSeconds * 1_000)
    window.setTimeout(() => map.invalidateSize(), 0)
    return () => {
      window.clearInterval(refresh)
      radarLayer.current = null
      map.remove()
    }
  // The map is deliberately mounted once per radar response. Frame changes update
  // the WMS TIME parameter without rebuilding the map or losing pan/zoom state.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [radar, frames])

  useEffect(() => {
    const timeParameter: TimeEnabledWmsParams = {
      layers: radar.wmsLayer ?? '',
      time: frames[frameIndex].toISOString(),
    }
    radarLayer.current?.setParams(timeParameter)
  }, [frameIndex, frames, radar.wmsLayer])

  useEffect(() => {
    if (!playing) return
    const prefersReducedMotion = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false
    const timer = window.setInterval(() => {
      setFrameIndex((current) => {
        if (current >= frames.length - 1) {
          setPlaying(false)
          return current
        }
        return current + 1
      })
    }, prefersReducedMotion ? 1_400 : 750)
    return () => window.clearInterval(timer)
  }, [frames.length, playing])

  const togglePlayback = () => {
    if (playing) {
      setPlaying(false)
      return
    }
    if (frameIndex === frames.length - 1) setFrameIndex(0)
    setPlaying(true)
  }

  return (
    <div className="weather-radar">
      <p className="weather-radar__notice">Radar history near the household’s approximate area. New imagery arrives about every five minutes.</p>
      <div
        aria-label="Interactive current weather radar map"
        className="weather-radar__map"
        data-workspace-swipe-ignore
        ref={mapElement}
        role="application"
      />
      <div className="weather-radar__timeline">
        <button aria-label={playing ? 'Pause radar animation' : 'Play radar animation'}
          aria-pressed={playing} className="weather-radar__play" onClick={togglePlayback} type="button">
          <span aria-hidden="true">{playing ? 'Ⅱ' : '▶'}</span>{playing ? 'Pause' : frameIndex === frames.length - 1 ? 'Play' : 'Resume'}
        </button>
        <label>
          <span>Radar time</span>
          <input aria-label="Radar animation time" max={frames.length - 1} min="0"
            onChange={(event) => { setPlaying(false); setFrameIndex(Number(event.target.value)) }}
            step="1" type="range" value={frameIndex} />
        </label>
        <time dateTime={frames[frameIndex].toISOString()}>{formatRadarTime(frames[frameIndex])}</time>
      </div>
      <p className="weather-attribution">{radar.attribution}. Map © OpenStreetMap contributors.</p>
    </div>
  )
}

function createRadarFrames(now: Date) {
  const frameIntervalMs = 10 * 60 * 1_000
  const latest = Math.floor(now.getTime() / frameIntervalMs) * frameIntervalMs
  return Array.from({ length: 13 }, (_, index) => new Date(latest - ((12 - index) * frameIntervalMs)))
}

function formatRadarTime(frame: Date) {
  return new Intl.DateTimeFormat(undefined, { hour: 'numeric', minute: '2-digit' }).format(frame)
}

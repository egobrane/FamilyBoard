import { useEffect, useRef } from 'react'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'
import type { HouseholdWeatherRadarResponse } from '../../lib/api'

interface WeatherRadarProps {
  radar: HouseholdWeatherRadarResponse
}

export function WeatherRadar({ radar }: WeatherRadarProps) {
  const mapElement = useRef<HTMLDivElement>(null)

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
    const radarLayer = L.tileLayer.wms(radar.wmsUrl, {
      layers: radar.wmsLayer,
      format: 'image/png',
      transparent: true,
      opacity: 0.72,
      className: 'weather-radar__overlay',
      attribution: 'Radar: NOAA/National Weather Service',
    }).addTo(map)
    L.circleMarker(center, {
      radius: 6,
      color: '#fffdf8',
      fillColor: '#26312f',
      fillOpacity: 1,
      weight: 2,
    }).addTo(map).bindTooltip('Approximate household area')

    const refresh = window.setInterval(() => radarLayer.redraw(), radar.refreshIntervalSeconds * 1_000)
    window.setTimeout(() => map.invalidateSize(), 0)
    return () => {
      window.clearInterval(refresh)
      map.remove()
    }
  }, [radar])

  return (
    <div className="weather-radar">
      <p className="weather-radar__notice">Current radar near the household’s approximate area. Radar refreshes about every five minutes.</p>
      <div
        aria-label="Interactive current weather radar map"
        className="weather-radar__map"
        data-workspace-swipe-ignore
        ref={mapElement}
        role="application"
      />
      <p className="weather-attribution">{radar.attribution}. Map © OpenStreetMap contributors.</p>
    </div>
  )
}

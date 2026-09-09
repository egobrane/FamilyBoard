# Household weather

Weather Increment 1 uses a provider-neutral `IWeatherProvider`; the first implementation targets the US National Weather Service. NWS requires no API key for this pilot, but it requires an identifying User-Agent. The provider remains replaceable without changing dashboard components or household persistence.

`HouseholdWeatherConfiguration` stores rounded coordinates, a user-facing label, unit preference, concurrency version, and update time. It does not store forecasts. Coordinates are returned only to an authorized adult using the administrative settings endpoint; routine dashboard responses contain the label and forecast but not coordinates. NWS remains the source of truth and in-memory cache entries are disposable.

Adults configure an approximate location at `/households/{householdId}/settings/weather`. Browser geolocation is optional, low-accuracy, one-time, explicitly consented, rounded to four decimal places before display, and never placed in browser storage or used for background tracking. Manual latitude/longitude avoids introducing a second geocoding provider. Administrative location changes require parent-PIN elevation on shared displays; routine forecast viewing does not reveal coordinates.

The API resolves the NWS grid, latest station observation, and daily forecast at request time. Fresh forecast data is cached for 30 minutes and last-known data can be served as stale for up to six hours after a provider failure. The HTTP timeout is eight seconds, and one bounded retry handles transient network or provider-server failures. Framework HTTP logging is disabled for this client so coordinate-bearing provider URLs are not written to application logs. The UI distinguishes loading, missing-location, stale, and unavailable states and exposes an accessible forecast dialog with native focus trapping, Escape handling, backdrop dismissal, and NWS attribution.

Weather is US-only in this increment. A future global requirement should select a replacement provider after reviewing coverage, commercial terms, attribution, caching rights, and cost. There are no weather credentials or new paid Azure services in this increment.

Set `Weather__Enabled=true`, `Weather__Provider=Nws`, and provide a descriptive `Weather__UserAgent`. Disabling the feature rolls back provider calls while retaining the household’s location configuration for a later forward fix.

Increment 1 is active and owner verified in Azure staging. The API retains `Weather__Enabled=true` and `Weather__Provider=Nws`; location configuration, compact current conditions, and the forecast presentation remained operational after the household-photo upload correction. NWS still requires no application secret, and forecast content remains disposable rather than persisted in PostgreSQL.

## Radar and playback

The radar view uses NOAA/NWS's official time-enabled base-reflectivity WMS. The default view shows the latest frame; a deliberate Play action steps through approximately the previous two hours in ten-minute increments and stops on the newest frame. Pause and an accessible range control allow manual inspection, and reduced-motion devices use a slower cadence. NOAA remains the source of truth and the application does not persist radar frames.

The authenticated API returns only a two-decimal, neighborhood-scale map center plus the reviewed public WMS configuration; it never returns the more precise stored weather coordinates through this routine endpoint.

The map loads only after a household member selects the Radar tab. Leaflet supplies touch, mouse, keyboard, zoom, and cleanup behavior; OpenStreetMap supplies the attributed basemap. NOAA and OpenStreetMap requests are made directly by the browser, so those providers receive the household device's IP address and map tile bounds. The application does not persist radar imagery, cache tiles for offline use, bulk-download tiles, or provide either service with account identifiers. Radar remains available for routine viewing on a locked shared display, while changing its center continues to use the parent-elevated weather-settings boundary.

Set `Weather__RadarEnabled=true`, `Weather__RadarWmsUrl` to the reviewed HTTPS NOAA time-enabled ImageServer WMS endpoint, and `Weather__RadarWmsLayer=radar_base_reflectivity_time`. Disabling the radar flag removes only the map; forecasts and saved location settings remain available. Radar adds no credential, Azure resource, database migration, polling job, or background synchronization.

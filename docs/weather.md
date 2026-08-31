# Household weather

Weather Increment 1 uses a provider-neutral `IWeatherProvider`; the first implementation targets the US National Weather Service. NWS requires no API key for this pilot, but it requires an identifying User-Agent. The provider remains replaceable without changing dashboard components or household persistence.

`HouseholdWeatherConfiguration` stores rounded coordinates, a user-facing label, unit preference, concurrency version, and update time. It does not store forecasts. Coordinates are returned only to an authorized adult using the administrative settings endpoint; routine dashboard responses contain the label and forecast but not coordinates. NWS remains the source of truth and in-memory cache entries are disposable.

Adults configure an approximate location at `/households/{householdId}/settings/weather`. Browser geolocation is optional, low-accuracy, one-time, explicitly consented, rounded to four decimal places before display, and never placed in browser storage or used for background tracking. Manual latitude/longitude avoids introducing a second geocoding provider. Administrative location changes require parent-PIN elevation on shared displays; routine forecast viewing does not reveal coordinates.

The API resolves the NWS grid, latest station observation, and daily forecast at request time. Fresh forecast data is cached for 30 minutes and last-known data can be served as stale for up to six hours after a provider failure. The HTTP timeout is eight seconds, and one bounded retry handles transient network or provider-server failures. Framework HTTP logging is disabled for this client so coordinate-bearing provider URLs are not written to application logs. The UI distinguishes loading, missing-location, stale, and unavailable states and exposes an accessible forecast dialog with native focus trapping, Escape handling, backdrop dismissal, and NWS attribution.

Weather is US-only in this increment. A future global requirement should select a replacement provider after reviewing coverage, commercial terms, attribution, caching rights, and cost. There are no weather credentials or new paid Azure services in this increment.

Set `Weather__Enabled=true`, `Weather__Provider=Nws`, and provide a descriptive `Weather__UserAgent`. Disabling the feature rolls back provider calls while retaining the household’s location configuration for a later forward fix.

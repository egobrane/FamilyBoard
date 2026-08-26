using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using System.Net.Http.Headers;
using FamilyDashboard.Api.Configuration;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth;
using Google.Apis.Calendar.v3;
using Google.Apis.Services;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace FamilyDashboard.Api.Features.Calendar;

public static class GoogleCalendarScopes
{
    public const string OpenId = "openid";
    public const string Email = "email";
    public const string CalendarListReadOnly = "https://www.googleapis.com/auth/calendar.calendarlist.readonly";
    public const string EventsReadOnly = "https://www.googleapis.com/auth/calendar.events.readonly";
    public const string EventsWrite = "https://www.googleapis.com/auth/calendar.events";
    public static readonly string[] Required = [OpenId, Email, CalendarListReadOnly, EventsReadOnly];
    public static readonly string[] RequiredCalendarDataAccess = [CalendarListReadOnly, EventsReadOnly];
    public static readonly string[] RequiredEventCreation = [CalendarListReadOnly, EventsWrite];

    public static string[] ForCapability(string capability) =>
        capability == CalendarAuthorizationCapabilities.EventCreation
            ? [OpenId, Email, CalendarListReadOnly, EventsWrite]
            : Required;
}

public sealed record GoogleCalendarTokenResult(
    string AccessToken,
    string? RefreshToken,
    DateTimeOffset ExpiresAt,
    string[] Scopes,
    string ProviderSubject,
    string ProviderEmail);

public sealed record GoogleCalendarRefreshResult(string AccessToken, DateTimeOffset ExpiresAt);
public sealed record GoogleProviderCalendar(
    string Id, string Name, string? TimeZone, string? Color, bool IsPrimary, string AccessRole);
public sealed record GoogleProviderEvent(
    string Id, string Title, bool IsAllDay, string Start, string End,
    string? TimeZone, string? Location, string? Notes = null, string ProviderVersion = "",
    bool IsRecurring = false, bool HasUnsupportedStructure = false);
public sealed record GoogleProviderEventPage(IReadOnlyList<GoogleProviderEvent> Events, string? NextPageToken);
public sealed record GoogleProviderCreateEvent(
    string Id,
    string Title,
    string? Location,
    string? Notes,
    bool IsAllDay,
    string Start,
    string End,
    string? TimeZone);
public sealed record GoogleProviderUpdateEvent(
    string Title, string? Location, string? Notes, bool IsAllDay,
    string Start, string End, string? TimeZone);

public enum GoogleCalendarProviderFailure
{
    Unavailable,
    RateLimited,
    ReauthorizationRequired,
    InvalidResponse,
    NotFound,
    VersionConflict,
    WriteForbidden,
}

public sealed class GoogleCalendarProviderException(
    GoogleCalendarProviderFailure failure,
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
    public GoogleCalendarProviderFailure Failure { get; } = failure;
}

public interface IGoogleCalendarProviderClient
{
    string CreateAuthorizationUrl(string state, string capability = CalendarAuthorizationCapabilities.ReadOnly);
    Task<GoogleCalendarTokenResult> ExchangeCodeAsync(string code, CancellationToken cancellationToken);
    Task<GoogleCalendarRefreshResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
    Task<IReadOnlyList<GoogleProviderCalendar>> ListCalendarsAsync(string accessToken, CancellationToken cancellationToken);
    Task<GoogleProviderEventPage> ListEventsAsync(
        string accessToken, string calendarId, DateTimeOffset rangeStart, DateTimeOffset rangeEnd,
        string? pageToken, int maximumResults, CancellationToken cancellationToken);
    Task<GoogleProviderEvent> CreateEventAsync(
        string accessToken, string calendarId, GoogleProviderCreateEvent request,
        CancellationToken cancellationToken);
    Task<GoogleProviderEvent> GetEventAsync(
        string accessToken, string calendarId, string eventId, CancellationToken cancellationToken);
    Task<GoogleProviderEvent> UpdateEventAsync(
        string accessToken, string calendarId, string eventId, string expectedProviderVersion,
        GoogleProviderUpdateEvent request, CancellationToken cancellationToken);
    Task DeleteEventAsync(
        string accessToken, string calendarId, string eventId, string expectedProviderVersion,
        CancellationToken cancellationToken);
    Task RevokeAsync(string token, CancellationToken cancellationToken);
}

public sealed class GoogleCalendarProviderClient(
    IHttpClientFactory httpClientFactory,
    IOptions<GoogleCalendarConfiguration> options,
    TimeProvider timeProvider) : IGoogleCalendarProviderClient
{
    private readonly GoogleCalendarConfiguration _configuration = options.Value;

    public string CreateAuthorizationUrl(
        string state, string capability = CalendarAuthorizationCapabilities.ReadOnly)
    {
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = _configuration.ClientId,
            ["redirect_uri"] = _configuration.CallbackUrl,
            ["response_type"] = "code",
            ["scope"] = string.Join(' ', GoogleCalendarScopes.ForCapability(capability)),
            ["access_type"] = "offline",
            ["include_granted_scopes"] = "true",
            ["prompt"] = "consent select_account",
            ["state"] = state,
        };
        return QueryHelpers.AddQueryString("https://accounts.google.com/o/oauth2/v2/auth", query);
    }

    public async Task<GoogleCalendarTokenResult> ExchangeCodeAsync(
        string code, CancellationToken cancellationToken)
    {
        var token = await SendTokenRequestAsync(new Dictionary<string, string>
        {
            ["client_id"] = _configuration.ClientId,
            ["client_secret"] = _configuration.ClientSecret,
            ["code"] = code,
            ["redirect_uri"] = _configuration.CallbackUrl,
            ["grant_type"] = "authorization_code",
        }, cancellationToken);
        if (string.IsNullOrWhiteSpace(token.IdToken))
            throw new GoogleCalendarProviderException(
                GoogleCalendarProviderFailure.InvalidResponse, "Google did not return an identity token.");

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(token.IdToken,
                new GoogleJsonWebSignature.ValidationSettings { Audience = [_configuration.ClientId] });
        }
        catch (InvalidJwtException exception)
        {
            throw new GoogleCalendarProviderException(
                GoogleCalendarProviderFailure.InvalidResponse, "Google returned an invalid identity token.", exception);
        }

        if (string.IsNullOrWhiteSpace(payload.Subject)
            || string.IsNullOrWhiteSpace(payload.Email)
            || payload.EmailVerified != true)
            throw new GoogleCalendarProviderException(
                GoogleCalendarProviderFailure.InvalidResponse, "Google did not identify the calendar account.");

        return new GoogleCalendarTokenResult(
            token.AccessToken,
            token.RefreshToken,
            timeProvider.GetUtcNow() + TimeSpan.FromSeconds(token.ExpiresIn),
            (token.Scope ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries),
            payload.Subject,
            payload.Email);
    }

    public async Task<GoogleCalendarRefreshResult> RefreshAsync(
        string refreshToken, CancellationToken cancellationToken)
    {
        var token = await SendTokenRequestAsync(new Dictionary<string, string>
        {
            ["client_id"] = _configuration.ClientId,
            ["client_secret"] = _configuration.ClientSecret,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token",
        }, cancellationToken);
        return new GoogleCalendarRefreshResult(
            token.AccessToken,
            timeProvider.GetUtcNow() + TimeSpan.FromSeconds(token.ExpiresIn));
    }

    public async Task<IReadOnlyList<GoogleProviderCalendar>> ListCalendarsAsync(
        string accessToken, CancellationToken cancellationToken)
    {
        try
        {
            using var service = CreateService(accessToken);
            var request = service.CalendarList.List();
            request.ShowDeleted = false;
            request.ShowHidden = false;
            request.MaxResults = 250;
            var results = new List<GoogleProviderCalendar>();
            do
            {
                var response = await request.ExecuteAsync(cancellationToken);
                results.AddRange((response.Items ?? []).Select(item => new GoogleProviderCalendar(
                    item.Id,
                    item.SummaryOverride ?? item.Summary ?? "Untitled calendar",
                    item.TimeZone,
                    item.BackgroundColor,
                    item.Primary == true,
                    item.AccessRole ?? "reader")));
                request.PageToken = response.NextPageToken;
            } while (!string.IsNullOrEmpty(request.PageToken));
            return results;
        }
        catch (GoogleApiException exception)
        {
            throw Map(exception);
        }
    }

    public async Task<GoogleProviderEvent> CreateEventAsync(
        string accessToken, string calendarId, GoogleProviderCreateEvent request,
        CancellationToken cancellationToken)
    {
        try
        {
            using var service = CreateService(accessToken);
            var eventResource = new Google.Apis.Calendar.v3.Data.Event
            {
                Id = request.Id,
                Summary = request.Title,
                Location = request.Location,
                Description = request.Notes,
                Start = request.IsAllDay
                    ? new Google.Apis.Calendar.v3.Data.EventDateTime { Date = request.Start }
                    : new Google.Apis.Calendar.v3.Data.EventDateTime
                    {
                        DateTimeDateTimeOffset = DateTimeOffset.Parse(
                            request.Start, CultureInfo.InvariantCulture),
                        TimeZone = request.TimeZone,
                    },
                End = request.IsAllDay
                    ? new Google.Apis.Calendar.v3.Data.EventDateTime { Date = request.End }
                    : new Google.Apis.Calendar.v3.Data.EventDateTime
                    {
                        DateTimeDateTimeOffset = DateTimeOffset.Parse(
                            request.End, CultureInfo.InvariantCulture),
                        TimeZone = request.TimeZone,
                    },
                Reminders = new Google.Apis.Calendar.v3.Data.Event.RemindersData { UseDefault = true },
            };
            var created = await service.Events.Insert(eventResource, calendarId)
                .ExecuteAsync(cancellationToken);
            return ToProviderEvent(created);
        }
        catch (GoogleApiException exception) when (exception.HttpStatusCode == HttpStatusCode.Conflict)
        {
            return await GetEventAsync(accessToken, calendarId, request.Id, cancellationToken);
        }
        catch (GoogleApiException exception)
        {
            throw Map(exception);
        }
    }

    public async Task<GoogleProviderEvent> GetEventAsync(
        string accessToken, string calendarId, string eventId, CancellationToken cancellationToken)
    {
        try
        {
            using var service = CreateService(accessToken);
            return ToProviderEvent(await service.Events.Get(calendarId, eventId)
                .ExecuteAsync(cancellationToken));
        }
        catch (GoogleApiException exception)
        {
            throw Map(exception);
        }
    }

    public async Task<GoogleProviderEvent> UpdateEventAsync(
        string accessToken, string calendarId, string eventId, string expectedProviderVersion,
        GoogleProviderUpdateEvent request, CancellationToken cancellationToken)
    {
        try
        {
            using var service = CreateService(accessToken);
            var existing = await service.Events.Get(calendarId, eventId).ExecuteAsync(cancellationToken);
            existing.ETag = expectedProviderVersion;
            existing.Summary = request.Title;
            existing.Location = request.Location;
            existing.Description = request.Notes;
            existing.Start = request.IsAllDay
                ? new Google.Apis.Calendar.v3.Data.EventDateTime { Date = request.Start }
                : new Google.Apis.Calendar.v3.Data.EventDateTime
                {
                    DateTimeDateTimeOffset = DateTimeOffset.Parse(request.Start, CultureInfo.InvariantCulture),
                    TimeZone = request.TimeZone,
                };
            existing.End = request.IsAllDay
                ? new Google.Apis.Calendar.v3.Data.EventDateTime { Date = request.End }
                : new Google.Apis.Calendar.v3.Data.EventDateTime
                {
                    DateTimeDateTimeOffset = DateTimeOffset.Parse(request.End, CultureInfo.InvariantCulture),
                    TimeZone = request.TimeZone,
                };
            var update = service.Events.Update(existing, calendarId, eventId);
            update.ETagAction = Google.Apis.ETagAction.IfMatch;
            update.SendUpdates = EventsResource.UpdateRequest.SendUpdatesEnum.None;
            return ToProviderEvent(await update.ExecuteAsync(cancellationToken));
        }
        catch (GoogleApiException exception) { throw Map(exception); }
    }

    public async Task DeleteEventAsync(
        string accessToken, string calendarId, string eventId, string expectedProviderVersion,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient(nameof(GoogleCalendarProviderClient));
            using var request = new HttpRequestMessage(HttpMethod.Delete,
                $"https://www.googleapis.com/calendar/v3/calendars/{Uri.EscapeDataString(calendarId)}/events/{Uri.EscapeDataString(eventId)}?sendUpdates=none");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.TryAddWithoutValidation("If-Match", expectedProviderVersion);
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw MapStatus(response.StatusCode);
        }
        catch (HttpRequestException exception)
        {
            throw new GoogleCalendarProviderException(
                GoogleCalendarProviderFailure.Unavailable, "Google Calendar was unavailable.", exception);
        }
    }

    public async Task<GoogleProviderEventPage> ListEventsAsync(
        string accessToken, string calendarId, DateTimeOffset rangeStart, DateTimeOffset rangeEnd,
        string? pageToken, int maximumResults, CancellationToken cancellationToken)
    {
        try
        {
            using var service = CreateService(accessToken);
            var request = service.Events.List(calendarId);
            request.TimeMinDateTimeOffset = rangeStart;
            request.TimeMaxDateTimeOffset = rangeEnd;
            request.SingleEvents = true;
            request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;
            request.ShowDeleted = false;
            request.MaxResults = maximumResults;
            request.PageToken = pageToken;
            var response = await request.ExecuteAsync(cancellationToken);
            return new GoogleProviderEventPage(
                (response.Items ?? []).Where(item => item.Start is not null && item.End is not null)
                    .Select(ToProviderEvent)
                    .ToArray(),
                response.NextPageToken);
        }
        catch (GoogleApiException exception)
        {
            throw Map(exception);
        }
    }

    public async Task RevokeAsync(string token, CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClientFactory.CreateClient(nameof(GoogleCalendarProviderClient))
                .PostAsync($"https://oauth2.googleapis.com/revoke?token={Uri.EscapeDataString(token)}", null, cancellationToken);
            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.BadRequest)
                throw new GoogleCalendarProviderException(
                    GoogleCalendarProviderFailure.Unavailable, "Google token revocation was unavailable.");
        }
        catch (HttpRequestException exception)
        {
            throw new GoogleCalendarProviderException(
                GoogleCalendarProviderFailure.Unavailable, "Google token revocation was unavailable.", exception);
        }
    }

    private async Task<TokenEndpointResponse> SendTokenRequestAsync(
        Dictionary<string, string> values, CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClientFactory.CreateClient(nameof(GoogleCalendarProviderClient))
                .PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(values), cancellationToken);
            var token = await response.Content.ReadFromJsonAsync<TokenEndpointResponse>(cancellationToken);
            if (!response.IsSuccessStatusCode || token is null || string.IsNullOrWhiteSpace(token.AccessToken))
            {
                var failure = token?.Error == "invalid_grant"
                    ? GoogleCalendarProviderFailure.ReauthorizationRequired
                    : response.StatusCode == HttpStatusCode.TooManyRequests
                        ? GoogleCalendarProviderFailure.RateLimited
                        : GoogleCalendarProviderFailure.Unavailable;
                throw new GoogleCalendarProviderException(failure, "Google token exchange failed.");
            }
            return token;
        }
        catch (HttpRequestException exception)
        {
            throw new GoogleCalendarProviderException(
                GoogleCalendarProviderFailure.Unavailable, "Google token exchange was unavailable.", exception);
        }
    }

    private static GoogleCalendarProviderException Map(GoogleApiException exception) =>
        new(MapFailure(exception.HttpStatusCode), "Google Calendar was unavailable.", exception);

    private static GoogleCalendarProviderException MapStatus(HttpStatusCode status) =>
        new(MapFailure(status), "Google Calendar rejected the event operation.");

    private static GoogleCalendarProviderFailure MapFailure(HttpStatusCode status) =>
        status switch
        {
            HttpStatusCode.Unauthorized => GoogleCalendarProviderFailure.ReauthorizationRequired,
            HttpStatusCode.TooManyRequests => GoogleCalendarProviderFailure.RateLimited,
            HttpStatusCode.NotFound or HttpStatusCode.Gone => GoogleCalendarProviderFailure.NotFound,
            HttpStatusCode.PreconditionFailed => GoogleCalendarProviderFailure.VersionConflict,
            HttpStatusCode.Forbidden => GoogleCalendarProviderFailure.WriteForbidden,
            _ => GoogleCalendarProviderFailure.Unavailable,
        };

    private static GoogleProviderEvent ToProviderEvent(Google.Apis.Calendar.v3.Data.Event item)
    {
        if (item.Start is null || item.End is null)
            throw new GoogleCalendarProviderException(
                GoogleCalendarProviderFailure.InvalidResponse,
                "Google returned an event without a valid range.");
        return new GoogleProviderEvent(
            item.Id,
            string.IsNullOrWhiteSpace(item.Summary) ? "Untitled event" : item.Summary,
            item.Start.Date is not null,
            item.Start.Date ?? item.Start.DateTimeDateTimeOffset?.ToString("O") ?? string.Empty,
            item.End.Date ?? item.End.DateTimeDateTimeOffset?.ToString("O") ?? string.Empty,
            item.Start.TimeZone,
            item.Location,
            item.Description,
            item.ETag ?? string.Empty,
            !string.IsNullOrWhiteSpace(item.RecurringEventId) || item.Recurrence?.Count > 0,
            item.Attendees?.Count > 0 || item.ConferenceData is not null || item.Attachments?.Count > 0
                || (!string.IsNullOrWhiteSpace(item.EventType) && item.EventType != "default"));
    }

    private static CalendarService CreateService(string accessToken) => new(new BaseClientService.Initializer
    {
        ApplicationName = "Family Dashboard",
        HttpClientInitializer = GoogleCredential.FromAccessToken(accessToken),
    });

    private sealed record TokenEndpointResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("scope")] string? Scope,
        [property: JsonPropertyName("id_token")] string? IdToken,
        [property: JsonPropertyName("error")] string? Error);
}

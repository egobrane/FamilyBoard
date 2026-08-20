using FamilyDashboard.Api.Configuration;
using FamilyDashboard.Api.Domain.Households;
using FamilyDashboard.Api.Domain.Integrations;
using FamilyDashboard.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Npgsql;

namespace FamilyDashboard.Api.Features.Calendar;

public sealed class CalendarOperationException(int status, string code, string message)
    : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}

public sealed record CalendarCallbackResult(Guid HouseholdId, string ReturnPath);

public sealed class GoogleCalendarService(
    FamilyDashboardDbContext dbContext,
    IGoogleCalendarProviderClient provider,
    CalendarTokenProtector tokenProtector,
    CalendarStateProtector stateProtector,
    IMemoryCache cache,
    IOptions<GoogleCalendarConfiguration> options,
    TimeProvider timeProvider)
{
    private readonly GoogleCalendarConfiguration _configuration = options.Value;

    public async Task<CalendarConnectionResponse> GetConnectionAsync(
        Guid householdId, Guid userAccountId, CancellationToken cancellationToken)
    {
        var connection = await dbContext.GoogleCalendarConnections.AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserAccountId == userAccountId, cancellationToken);
        var count = connection is null ? 0 : await dbContext.HouseholdCalendarSources.CountAsync(
            source => source.HouseholdId == householdId
                && source.GoogleCalendarConnectionId == connection.Id
                && source.IsActive,
            cancellationToken);
        return connection is null
            ? new CalendarConnectionResponse(
                _configuration.Enabled, null, "disconnected", null, null, true, 0,
                _configuration.EventCreationEnabled, false)
            : new CalendarConnectionResponse(
                _configuration.Enabled,
                connection.Id,
                ToContract(connection.Status),
                connection.ProviderEmailNormalized,
                connection.ConnectedAt,
                true,
                count,
                _configuration.EventCreationEnabled,
                HasEventCreationScope(connection));
    }

    public (BeginCalendarAuthorizationResponse Response, string State) BeginAuthorization(
        Guid householdId, Guid userAccountId, Guid userSessionId, string returnPath,
        string capability)
    {
        RequireAvailable();
        if (capability == CalendarAuthorizationCapabilities.EventCreation)
            RequireEventCreationAvailable();
        else if (capability != CalendarAuthorizationCapabilities.ReadOnly)
            throw new CalendarOperationException(400,
                Common.ApiProblemCodes.ValidationFailed,
                "The requested calendar capability is invalid.");
        var (state, expiresAt) = stateProtector.CreateAuthorization(
            userAccountId, userSessionId, householdId, returnPath, capability);
        return (new BeginCalendarAuthorizationResponse(
            provider.CreateAuthorizationUrl(state, capability), expiresAt), state);
    }

    public async Task<CalendarCallbackResult> CompleteAuthorizationAsync(
        string code, string state, Guid userAccountId, Guid userSessionId,
        CancellationToken cancellationToken)
    {
        RequireAvailable();
        if (!stateProtector.TryReadAuthorization(state, out var payload)
            || payload!.UserAccountId != userAccountId
            || payload.UserSessionId != userSessionId)
        {
            throw new CalendarOperationException(400,
                Common.ApiProblemCodes.CalendarAuthorizationExpired,
                "The calendar authorization request expired or is invalid.");
        }

        var token = await provider.ExchangeCodeAsync(code, cancellationToken);
        // Google may return the canonical userinfo.email scope for the requested OpenID
        // "email" alias. Identity is validated from the signed ID token above this seam;
        // only the Calendar data-access scopes require exact granted-scope validation here.
        var requiredScopes = payload.Capability == CalendarAuthorizationCapabilities.EventCreation
            ? GoogleCalendarScopes.RequiredEventCreation
            : GoogleCalendarScopes.RequiredCalendarDataAccess;
        var missingScopes = requiredScopes
            .Except(token.Scopes, StringComparer.Ordinal)
            .ToArray();
        if (missingScopes.Length > 0)
            throw new CalendarOperationException(409,
                Common.ApiProblemCodes.CalendarScopeMissing,
                "Required Google Calendar permissions were not granted.");

        var now = timeProvider.GetUtcNow();
        var connection = await dbContext.GoogleCalendarConnections
            .Include(item => item.HouseholdSources)
            .SingleOrDefaultAsync(item => item.UserAccountId == userAccountId, cancellationToken);
        if (connection is null)
        {
            if (string.IsNullOrWhiteSpace(token.RefreshToken))
                throw new CalendarOperationException(409,
                    Common.ApiProblemCodes.CalendarOfflineAccessRequired,
                    "Google did not grant offline calendar access. Reconnect and approve access.");
            connection = new GoogleCalendarConnection
            {
                UserAccountId = userAccountId,
                ProviderSubject = token.ProviderSubject,
                ProviderEmailNormalized = token.ProviderEmail.Trim().ToLowerInvariant(),
                GrantedScopes = string.Join(' ', token.Scopes.Order(StringComparer.Ordinal)),
                ConnectedAt = now,
                UpdatedAt = now,
            };
            dbContext.GoogleCalendarConnections.Add(connection);
        }
        else
        {
            if (connection.ProviderSubject != token.ProviderSubject)
                throw new CalendarOperationException(409,
                    Common.ApiProblemCodes.CalendarAccountMismatch,
                    "Disconnect the current Calendar account before connecting a different account.");
            connection.ProviderSubject = token.ProviderSubject;
            connection.ProviderEmailNormalized = token.ProviderEmail.Trim().ToLowerInvariant();
            connection.GrantedScopes = string.Join(' ', token.Scopes.Order(StringComparer.Ordinal));
            connection.UpdatedAt = now;
            connection.RevokedAt = null;
        }

        var refreshToken = token.RefreshToken;
        if (string.IsNullOrWhiteSpace(refreshToken) && connection.ProtectedRefreshToken is null)
            throw new CalendarOperationException(409,
                Common.ApiProblemCodes.CalendarOfflineAccessRequired,
                "Google did not grant offline calendar access. Reconnect and approve access.");
        connection.ProtectedAccessToken = tokenProtector.Protect(connection.Id, "access-token", token.AccessToken);
        if (!string.IsNullOrWhiteSpace(refreshToken))
            connection.ProtectedRefreshToken = tokenProtector.Protect(connection.Id, "refresh-token", refreshToken);
        connection.AccessTokenExpiresAt = token.ExpiresAt;
        connection.Status = GoogleCalendarConnectionStatus.Active;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new CalendarCallbackResult(payload.HouseholdId, payload.ReturnPath);
    }

    public async Task<IReadOnlyList<ProviderCalendarResponse>> ListProviderCalendarsAsync(
        Guid householdId, Guid userAccountId, CancellationToken cancellationToken)
    {
        RequireAvailable();
        var connection = await FindActiveConnectionAsync(userAccountId, cancellationToken);
        var accessToken = await GetAccessTokenAsync(connection, cancellationToken);
        IReadOnlyList<GoogleProviderCalendar> calendars;
        try { calendars = await provider.ListCalendarsAsync(accessToken, cancellationToken); }
        catch (GoogleCalendarProviderException exception)
            when (exception.Failure == GoogleCalendarProviderFailure.ReauthorizationRequired)
        {
            await MarkReauthorizationRequiredAsync(connection, cancellationToken);
            throw new CalendarOperationException(409,
                Common.ApiProblemCodes.CalendarReauthorizationRequired, "Reconnect Google Calendar.");
        }
        var selected = await dbContext.HouseholdCalendarSources.AsNoTracking()
            .Where(source => source.HouseholdId == householdId
                && source.GoogleCalendarConnectionId == connection.Id
                && source.IsActive)
            .Select(source => source.ExternalCalendarId)
            .ToHashSetAsync(cancellationToken);
        var creationTarget = await dbContext.HouseholdCalendarSources.AsNoTracking()
            .Where(source => source.HouseholdId == householdId
                && source.GoogleCalendarConnectionId == connection.Id
                && source.IsActive
                && source.IsEventCreationTarget)
            .Select(source => source.ExternalCalendarId)
            .SingleOrDefaultAsync(cancellationToken);
        return calendars.Select(calendar => new ProviderCalendarResponse(
            calendar.Id, calendar.Name, calendar.TimeZone, calendar.Color,
            calendar.IsPrimary, selected.Contains(calendar.Id), calendar.AccessRole,
            CanCreateEvents(calendar.AccessRole) && HasEventCreationScope(connection),
            calendar.Id == creationTarget)).ToArray();
    }

    public async Task<IReadOnlyList<CalendarSourceResponse>> ListSourcesAsync(
        Guid householdId, Guid userAccountId, CancellationToken cancellationToken) =>
        await dbContext.HouseholdCalendarSources.AsNoTracking()
            .Where(source => source.HouseholdId == householdId)
            .OrderByDescending(source => source.IsActive)
            .ThenBy(source => source.DisplayNameSnapshot)
            .Select(source => new CalendarSourceResponse(
                source.Id,
                source.GoogleCalendarConnectionId,
                source.ExternalCalendarId,
                source.DisplayNameSnapshot,
                source.TimeZone,
                source.Color,
                source.IsActive,
                source.OwnerUserAccountId == userAccountId,
                source.IsEventCreationTarget))
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyList<CalendarSourceResponse>> UpdateSourcesAsync(
        Guid householdId, Guid userAccountId, UpdateCalendarSourcesRequest request,
        CancellationToken cancellationToken)
    {
        RequireAvailable();
        var ids = (request.ExternalCalendarIds ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (ids.Length > _configuration.MaximumCalendarsPerHousehold)
            throw new CalendarOperationException(400, Common.ApiProblemCodes.ValidationFailed,
                $"Choose no more than {_configuration.MaximumCalendarsPerHousehold} calendars.");
        var connection = await FindActiveConnectionAsync(userAccountId, cancellationToken);
        if (connection.Id != request.ConnectionId)
            throw new CalendarOperationException(404,
                Common.ApiProblemCodes.CalendarSourceNotFound, "The calendar connection was not found.");
        IReadOnlyList<GoogleProviderCalendar> providerCalendars;
        try
        {
            providerCalendars = await provider.ListCalendarsAsync(
                await GetAccessTokenAsync(connection, cancellationToken), cancellationToken);
        }
        catch (GoogleCalendarProviderException exception)
            when (exception.Failure == GoogleCalendarProviderFailure.ReauthorizationRequired)
        {
            await MarkReauthorizationRequiredAsync(connection, cancellationToken);
            throw new CalendarOperationException(409,
                Common.ApiProblemCodes.CalendarReauthorizationRequired, "Reconnect Google Calendar.");
        }
        var available = providerCalendars.ToDictionary(calendar => calendar.Id, StringComparer.Ordinal);
        if (ids.Any(id => !available.ContainsKey(id)))
            throw new CalendarOperationException(400,
                Common.ApiProblemCodes.CalendarSourceNotFound,
                "One or more selected calendars are unavailable.");

        var now = timeProvider.GetUtcNow();
        var existing = await dbContext.HouseholdCalendarSources
            .Where(source => source.HouseholdId == householdId
                && source.GoogleCalendarConnectionId == connection.Id)
            .ToListAsync(cancellationToken);
        foreach (var source in existing)
        {
            source.IsActive = ids.Contains(source.ExternalCalendarId, StringComparer.Ordinal);
            if (!source.IsActive)
            {
                source.IsEventCreationTarget = false;
                source.EventCreationEnabledAt = null;
                source.EventCreationEnabledByUserAccountId = null;
            }
            source.UpdatedAt = now;
            if (available.TryGetValue(source.ExternalCalendarId, out var calendar))
            {
                source.DisplayNameSnapshot = calendar.Name;
                source.TimeZone = calendar.TimeZone;
                source.Color = calendar.Color;
            }
        }
        foreach (var id in ids.Except(existing.Select(source => source.ExternalCalendarId), StringComparer.Ordinal))
        {
            var calendar = available[id];
            dbContext.HouseholdCalendarSources.Add(new HouseholdCalendarSource
            {
                HouseholdId = householdId,
                GoogleCalendarConnectionId = connection.Id,
                OwnerUserAccountId = userAccountId,
                ExternalCalendarId = id,
                DisplayNameSnapshot = calendar.Name,
                TimeZone = calendar.TimeZone,
                Color = calendar.Color,
                AddedByUserAccountId = userAccountId,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return await ListSourcesAsync(householdId, userAccountId, cancellationToken);
    }

    public async Task DisconnectAsync(
        Guid userAccountId, DisconnectCalendarRequest request, CancellationToken cancellationToken)
    {
        if (!request.ConfirmGlobalDisconnect)
            throw new CalendarOperationException(409,
                Common.ApiProblemCodes.CalendarDisconnectConfirmationRequired,
                "Confirm that this connection should be removed from every household.");
        var connection = await dbContext.GoogleCalendarConnections
            .Include(item => item.HouseholdSources)
            .SingleOrDefaultAsync(item => item.Id == request.ConnectionId
                && item.UserAccountId == userAccountId, cancellationToken)
            ?? throw new CalendarOperationException(404,
                Common.ApiProblemCodes.CalendarSourceNotFound, "The calendar connection was not found.");
        string? refreshToken = null;
        if (connection.ProtectedRefreshToken is not null)
        {
            try { refreshToken = tokenProtector.Unprotect(connection.Id, "refresh-token", connection.ProtectedRefreshToken); }
            catch (System.Security.Cryptography.CryptographicException) { }
        }
        if (refreshToken is not null)
        {
            try { await provider.RevokeAsync(refreshToken, cancellationToken); }
            catch (GoogleCalendarProviderException) { /* Local revocation remains authoritative. */ }
        }
        var now = timeProvider.GetUtcNow();
        connection.Status = GoogleCalendarConnectionStatus.Disconnected;
        connection.ProtectedAccessToken = null;
        connection.ProtectedRefreshToken = null;
        connection.AccessTokenExpiresAt = null;
        connection.RevokedAt = now;
        connection.UpdatedAt = now;
        foreach (var source in connection.HouseholdSources)
        {
            source.IsActive = false;
            source.IsEventCreationTarget = false;
            source.EventCreationEnabledAt = null;
            source.EventCreationEnabledByUserAccountId = null;
            source.UpdatedAt = now;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<CalendarEventCreationTargetResponse> GetEventCreationTargetAsync(
        Guid householdId, CancellationToken cancellationToken)
    {
        var target = await dbContext.HouseholdCalendarSources.AsNoTracking()
            .Include(source => source.GoogleCalendarConnection)
            .SingleOrDefaultAsync(source => source.HouseholdId == householdId
                && source.IsActive
                && source.IsEventCreationTarget
                && source.GoogleCalendarConnection.Status == GoogleCalendarConnectionStatus.Active
                && source.GoogleCalendarConnection.UserAccount.IsActive,
                cancellationToken);
        return target is null
            ? new CalendarEventCreationTargetResponse(
                _configuration.EventCreationEnabled, false, null, null, null, null)
            : new CalendarEventCreationTargetResponse(
                _configuration.EventCreationEnabled,
                HasEventCreationScope(target.GoogleCalendarConnection),
                target.Id,
                target.DisplayNameSnapshot,
                target.TimeZone,
                target.Color);
    }

    public async Task<CalendarEventCreationTargetResponse> UpdateEventCreationTargetAsync(
        Guid householdId, Guid userAccountId, UpdateCalendarEventCreationTargetRequest request,
        CancellationToken cancellationToken)
    {
        RequireEventCreationAvailable();
        var currentTargets = await dbContext.HouseholdCalendarSources
            .Where(source => source.HouseholdId == householdId && source.IsEventCreationTarget)
            .ToListAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        foreach (var current in currentTargets)
        {
            current.IsEventCreationTarget = false;
            current.EventCreationEnabledAt = null;
            current.EventCreationEnabledByUserAccountId = null;
            current.UpdatedAt = now;
        }
        if (request.SourceId is not null)
        {
            var source = await dbContext.HouseholdCalendarSources
                .Include(item => item.GoogleCalendarConnection)
                .SingleOrDefaultAsync(item => item.Id == request.SourceId
                    && item.HouseholdId == householdId
                    && item.OwnerUserAccountId == userAccountId
                    && item.IsActive,
                    cancellationToken)
                ?? throw new CalendarOperationException(404,
                    Common.ApiProblemCodes.CalendarSourceNotFound,
                    "The calendar source was not found.");
            if (!HasEventCreationScope(source.GoogleCalendarConnection))
                throw new CalendarOperationException(409,
                    Common.ApiProblemCodes.CalendarWriteAuthorizationRequired,
                    "Authorize Google Calendar event creation first.");
            IReadOnlyList<GoogleProviderCalendar> calendars;
            try
            {
                calendars = await provider.ListCalendarsAsync(
                    await GetAccessTokenAsync(source.GoogleCalendarConnection, cancellationToken),
                    cancellationToken);
            }
            catch (GoogleCalendarProviderException exception)
                when (exception.Failure == GoogleCalendarProviderFailure.ReauthorizationRequired)
            {
                await MarkReauthorizationRequiredAsync(source.GoogleCalendarConnection, cancellationToken);
                throw new CalendarOperationException(409,
                    Common.ApiProblemCodes.CalendarReauthorizationRequired,
                    "Reconnect Google Calendar.");
            }
            var calendar = calendars.SingleOrDefault(item => item.Id == source.ExternalCalendarId);
            if (calendar is null || !CanCreateEvents(calendar.AccessRole))
                throw new CalendarOperationException(409,
                    Common.ApiProblemCodes.CalendarEventCreationTargetInvalid,
                    "Choose a Google calendar with writer or owner access.");
            source.IsEventCreationTarget = true;
            source.EventCreationEnabledAt = now;
            source.EventCreationEnabledByUserAccountId = userAccountId;
            source.UpdatedAt = now;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetEventCreationTargetAsync(householdId, cancellationToken);
    }

    public async Task<CreatedCalendarEventResponse> CreateEventAsync(
        Guid householdId, Guid userAccountId, Guid userSessionId,
        CreateCalendarEventRequest request, string traceId,
        CancellationToken cancellationToken)
    {
        RequireEventCreationAvailable();
        var session = await dbContext.UserSessions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == userSessionId
                && item.UserAccountId == userAccountId
                && item.RevokedAt == null,
                cancellationToken)
            ?? throw new CalendarOperationException(401,
                Common.ApiProblemCodes.AuthenticationRequired,
                "Authentication is required.");
        if (session.SelectedHouseholdId != householdId)
            throw new CalendarOperationException(409,
                Common.ApiProblemCodes.HouseholdSelectionRequired,
                "Select this household before adding an event.");

        var source = await dbContext.HouseholdCalendarSources
            .Include(item => item.GoogleCalendarConnection)
            .ThenInclude(connection => connection.UserAccount)
            .SingleOrDefaultAsync(item => item.Id == request.SourceId
                && item.HouseholdId == householdId
                && item.IsActive
                && item.IsEventCreationTarget
                && item.GoogleCalendarConnection.Status == GoogleCalendarConnectionStatus.Active
                && item.GoogleCalendarConnection.UserAccount.IsActive,
                cancellationToken)
            ?? throw new CalendarOperationException(404,
                Common.ApiProblemCodes.CalendarEventCreationTargetInvalid,
                "The event creation target was not found.");
        if (!HasEventCreationScope(source.GoogleCalendarConnection))
            throw new CalendarOperationException(409,
                Common.ApiProblemCodes.CalendarWriteAuthorizationRequired,
                "Authorize Google Calendar event creation first.");

        var attributedMemberId = request.AttributedMemberId;
        if (attributedMemberId is null)
        {
            if (session.IsSharedDisplay)
                throw new CalendarOperationException(400,
                    Common.ApiProblemCodes.ValidationFailed,
                    "Choose who is adding this event.");
            attributedMemberId = await dbContext.HouseholdMemberships.AsNoTracking()
                .Where(item => item.UserAccountId == userAccountId
                    && item.HouseholdId == householdId
                    && item.HouseholdMember.IsActive)
                .Select(item => (Guid?)item.HouseholdMemberId)
                .SingleOrDefaultAsync(cancellationToken);
        }
        if (attributedMemberId is null || !await dbContext.HouseholdMembers.AsNoTracking()
            .AnyAsync(item => item.Id == attributedMemberId
                && item.HouseholdId == householdId
                && item.IsActive,
                cancellationToken))
            throw new CalendarOperationException(400,
                Common.ApiProblemCodes.ValidationFailed,
                "Choose an active household member.");

        var normalized = ValidateAndNormalize(request);
        var fingerprint = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            request.SourceId,
            AttributedMemberId = attributedMemberId.Value,
            normalized.Title,
            normalized.Location,
            normalized.Notes,
            normalized.IsAllDay,
            normalized.Start,
            normalized.End,
            normalized.TimeZone,
        }));
        var receipt = await dbContext.CalendarEventCreationReceipts
            .SingleOrDefaultAsync(item => item.HouseholdId == householdId
                && item.Id == request.IdempotencyKey,
                cancellationToken);
        var recovered = receipt is not null;
        if (receipt is not null
            && !CryptographicOperations.FixedTimeEquals(receipt.RequestFingerprint, fingerprint))
            throw new CalendarOperationException(409,
                Common.ApiProblemCodes.CalendarIdempotencyConflict,
                "This event request identifier was already used for different details.");
        if (receipt is null)
        {
            receipt = new CalendarEventCreationReceipt
            {
                Id = request.IdempotencyKey,
                HouseholdId = householdId,
                HouseholdCalendarSourceId = source.Id,
                RequestedByUserAccountId = userAccountId,
                AttributedHouseholdMemberId = attributedMemberId.Value,
                RequestedFromSharedDisplay = session.IsSharedDisplay,
                RequestFingerprint = fingerprint,
                CreatedAt = timeProvider.GetUtcNow(),
                TraceId = traceId.Length <= 128 ? traceId : traceId[..128],
            };
            dbContext.CalendarEventCreationReceipts.Add(receipt);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException
                { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                dbContext.Entry(receipt).State = EntityState.Detached;
                receipt = await dbContext.CalendarEventCreationReceipts.SingleAsync(
                    item => item.HouseholdId == householdId
                        && item.Id == request.IdempotencyKey,
                    cancellationToken);
                recovered = true;
                if (!CryptographicOperations.FixedTimeEquals(receipt.RequestFingerprint, fingerprint))
                    throw new CalendarOperationException(409,
                        Common.ApiProblemCodes.CalendarIdempotencyConflict,
                        "This event request identifier was already used for different details.");
            }
        }

        var accessToken = await GetAccessTokenAsync(source.GoogleCalendarConnection, cancellationToken);
        IReadOnlyList<GoogleProviderCalendar> calendars;
        try { calendars = await provider.ListCalendarsAsync(accessToken, cancellationToken); }
        catch (GoogleCalendarProviderException exception)
            when (exception.Failure == GoogleCalendarProviderFailure.ReauthorizationRequired)
        {
            await MarkReauthorizationRequiredAsync(source.GoogleCalendarConnection, cancellationToken);
            throw new CalendarOperationException(409,
                Common.ApiProblemCodes.CalendarReauthorizationRequired,
                "Reconnect Google Calendar.");
        }
        var calendar = calendars.SingleOrDefault(item => item.Id == source.ExternalCalendarId);
        if (calendar is null || !CanCreateEvents(calendar.AccessRole))
            throw new CalendarOperationException(409,
                Common.ApiProblemCodes.CalendarEventCreationTargetInvalid,
                "The selected Google calendar is no longer writable.");
        var providerEventId = receipt.ProviderEventId ?? $"fd{request.IdempotencyKey:N}";
        GoogleProviderEvent providerEvent;
        try
        {
            providerEvent = receipt.Status == CalendarEventCreationReceiptStatus.Succeeded
                ? await provider.GetEventAsync(
                    accessToken, source.ExternalCalendarId, providerEventId, cancellationToken)
                : await provider.CreateEventAsync(
                    accessToken,
                    source.ExternalCalendarId,
                    new GoogleProviderCreateEvent(
                        providerEventId,
                        normalized.Title,
                        normalized.Location,
                        normalized.Notes,
                        normalized.IsAllDay,
                        normalized.Start,
                        normalized.End,
                        normalized.TimeZone),
                    cancellationToken);
        }
        catch (GoogleCalendarProviderException exception)
            when (exception.Failure == GoogleCalendarProviderFailure.ReauthorizationRequired)
        {
            await MarkReauthorizationRequiredAsync(source.GoogleCalendarConnection, cancellationToken);
            throw new CalendarOperationException(409,
                Common.ApiProblemCodes.CalendarReauthorizationRequired,
                "Reconnect Google Calendar.");
        }
        if (receipt.Status != CalendarEventCreationReceiptStatus.Succeeded)
        {
            receipt.ProviderEventId = providerEvent.Id;
            receipt.Status = CalendarEventCreationReceiptStatus.Succeeded;
            receipt.CompletedAt = timeProvider.GetUtcNow();
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        cache.Set($"calendar-version:{source.Id:D}", Guid.NewGuid().ToString("N"),
            _configuration.StaleCacheLifetime);
        return new CreatedCalendarEventResponse(
            providerEvent.Id,
            source.Id,
            source.DisplayNameSnapshot,
            providerEvent.Title,
            providerEvent.IsAllDay,
            providerEvent.Start,
            providerEvent.End,
            providerEvent.TimeZone,
            providerEvent.Location,
            source.Color,
            attributedMemberId.Value,
            recovered);
    }

    public async Task<CalendarEventsResponse> ListEventsAsync(
        Guid householdId, DateTimeOffset from, DateTimeOffset to, string? cursor,
        CancellationToken cancellationToken)
    {
        if (from >= to || to - from > TimeSpan.FromDays(31))
            throw new CalendarOperationException(400,
                Common.ApiProblemCodes.CalendarRangeInvalid,
                "Choose a calendar range of no more than 31 days.");
        CalendarPageCursor? page = null;
        if (cursor is not null && (!stateProtector.TryReadCursor(cursor, out page)
            || page!.HouseholdId != householdId || page.From != from || page.To != to))
            throw new CalendarOperationException(400,
                Common.ApiProblemCodes.CalendarCursorInvalid, "The calendar cursor is invalid or expired.");

        var sources = await dbContext.HouseholdCalendarSources.AsNoTracking()
            .Include(source => source.GoogleCalendarConnection)
            .Where(source => source.HouseholdId == householdId
                && source.IsActive
                && source.GoogleCalendarConnection.Status == GoogleCalendarConnectionStatus.Active
                && source.GoogleCalendarConnection.UserAccount.IsActive
                && source.GoogleCalendarConnection.UserAccount.HouseholdMemberships.Any(membership =>
                    membership.HouseholdId == householdId
                    && membership.HouseholdMember.IsActive
                    && membership.HouseholdMember.Role == HouseholdMemberRole.Adult))
            .OrderBy(source => source.Id)
            .ToListAsync(cancellationToken);
        if (page is not null)
            sources = sources.Where(source => page.RemainingSources.ContainsKey(source.Id)).ToList();

        var events = new List<CalendarEventResponse>();
        var warnings = new List<CalendarWarningResponse>();
        var remaining = new Dictionary<Guid, string?>();
        var connectionsRequiringAuthorization = new HashSet<Guid>();
        var stale = false;
        var remainingCapacity = _configuration.MaximumEventsPerRequest;
        foreach (var connectionGroup in sources.GroupBy(source => source.GoogleCalendarConnectionId))
        {
            var connection = connectionGroup.First().GoogleCalendarConnection;
            string accessToken;
            try { accessToken = await GetAccessTokenAsync(connection, cancellationToken); }
            catch (CalendarOperationException exception)
            {
                warnings.AddRange(connectionGroup.Select(source => new CalendarWarningResponse(
                    source.Id, exception.Code, "Reconnect this Google Calendar account.")));
                continue;
            }
            foreach (var source in connectionGroup)
            {
                var pageToken = page?.RemainingSources[source.Id];
                if (remainingCapacity == 0)
                {
                    remaining[source.Id] = pageToken;
                    continue;
                }
                var result = await GetEventPageAsync(
                    source, accessToken, from, to, pageToken,
                    Math.Min(250, remainingCapacity), cancellationToken);
                stale |= result.IsStale;
                if (result.Warning is not null) warnings.Add(result.Warning);
                if (result.Warning?.Code == Common.ApiProblemCodes.CalendarReauthorizationRequired
                    && connectionsRequiringAuthorization.Add(connection.Id))
                    await MarkReauthorizationRequiredAsync(connection, cancellationToken);
                events.AddRange(result.Page.Events.Select(item => new CalendarEventResponse(
                    item.Id, source.Id, source.DisplayNameSnapshot, item.Title,
                    item.IsAllDay, item.Start, item.End, item.TimeZone,
                    item.Location, source.Color)));
                remainingCapacity -= result.Page.Events.Count;
                if (result.Page.NextPageToken is not null)
                    remaining[source.Id] = result.Page.NextPageToken;
            }
        }
        events = events.OrderBy(item => item.Start, StringComparer.Ordinal).ToList();
        var nextCursor = remaining.Count == 0 ? null : stateProtector.CreateCursor(new CalendarPageCursor(
            householdId, from, to, remaining,
            timeProvider.GetUtcNow() + _configuration.StaleCacheLifetime));
        return new CalendarEventsResponse(events, nextCursor, stale, warnings);
    }

    private async Task<(GoogleProviderEventPage Page, bool IsStale, CalendarWarningResponse? Warning)> GetEventPageAsync(
        HouseholdCalendarSource source, string accessToken, DateTimeOffset from, DateTimeOffset to,
        string? pageToken, int maximumResults, CancellationToken cancellationToken)
    {
        if (!cache.TryGetValue<string>($"calendar-version:{source.Id:D}", out var version))
        {
            version = "initial";
            cache.Set($"calendar-version:{source.Id:D}", version, _configuration.StaleCacheLifetime);
        }
        var key = $"calendar:{source.Id:D}:{version}:{from:O}:{to:O}:{pageToken}:{maximumResults}";
        if (cache.TryGetValue<EventCache>(key, out var cached)
            && cached!.FetchedAt + _configuration.FreshCacheLifetime > timeProvider.GetUtcNow())
            return (cached.Page, false, null);
        try
        {
            var page = await provider.ListEventsAsync(accessToken, source.ExternalCalendarId,
                from, to, pageToken, maximumResults, cancellationToken);
            cache.Set(key, new EventCache(page, timeProvider.GetUtcNow()), _configuration.StaleCacheLifetime);
            return (page, false, null);
        }
        catch (GoogleCalendarProviderException exception)
        {
            if (cached is not null)
                return (cached.Page, true, new CalendarWarningResponse(source.Id,
                    exception.Failure == GoogleCalendarProviderFailure.ReauthorizationRequired
                        ? Common.ApiProblemCodes.CalendarReauthorizationRequired
                        : Common.ApiProblemCodes.CalendarProviderUnavailable,
                    exception.Failure == GoogleCalendarProviderFailure.ReauthorizationRequired
                        ? "Showing cached information. Reconnect this Google Calendar account."
                        : "Showing recently cached calendar information."));
            return (new GoogleProviderEventPage([], null), false, new CalendarWarningResponse(
                source.Id,
                exception.Failure switch
                {
                    GoogleCalendarProviderFailure.RateLimited => Common.ApiProblemCodes.CalendarProviderRateLimited,
                    GoogleCalendarProviderFailure.ReauthorizationRequired => Common.ApiProblemCodes.CalendarReauthorizationRequired,
                    _ => Common.ApiProblemCodes.CalendarProviderUnavailable,
                },
                exception.Failure == GoogleCalendarProviderFailure.ReauthorizationRequired
                    ? "Reconnect this Google Calendar account."
                    : "This calendar is temporarily unavailable."));
        }
    }

    private async Task<GoogleCalendarConnection> FindActiveConnectionAsync(
        Guid userAccountId, CancellationToken cancellationToken)
    {
        var connection = await dbContext.GoogleCalendarConnections.SingleOrDefaultAsync(
            item => item.UserAccountId == userAccountId, cancellationToken);
        if (connection is null)
            throw new CalendarOperationException(409,
                Common.ApiProblemCodes.CalendarConnectionRequired, "Connect Google Calendar first.");
        if (connection.Status != GoogleCalendarConnectionStatus.Active)
            throw new CalendarOperationException(409,
                Common.ApiProblemCodes.CalendarReauthorizationRequired, "Reconnect Google Calendar.");
        return connection;
    }

    private async Task<string> GetAccessTokenAsync(
        GoogleCalendarConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            if (connection.AccessTokenExpiresAt > timeProvider.GetUtcNow() + TimeSpan.FromMinutes(1)
                && connection.ProtectedAccessToken is not null)
                return tokenProtector.Unprotect(connection.Id, "access-token", connection.ProtectedAccessToken);
            if (connection.ProtectedRefreshToken is null)
                throw new System.Security.Cryptography.CryptographicException();
            var refreshToken = tokenProtector.Unprotect(
                connection.Id, "refresh-token", connection.ProtectedRefreshToken);
            var refreshed = await provider.RefreshAsync(refreshToken, cancellationToken);
            connection.ProtectedAccessToken = tokenProtector.Protect(
                connection.Id, "access-token", refreshed.AccessToken);
            connection.AccessTokenExpiresAt = refreshed.ExpiresAt;
            connection.LastSuccessfulRefreshAt = timeProvider.GetUtcNow();
            connection.UpdatedAt = timeProvider.GetUtcNow();
            dbContext.Update(connection);
            await dbContext.SaveChangesAsync(cancellationToken);
            return refreshed.AccessToken;
        }
        catch (Exception exception) when (exception is System.Security.Cryptography.CryptographicException
            || exception is GoogleCalendarProviderException
            { Failure: GoogleCalendarProviderFailure.ReauthorizationRequired })
        {
            connection.Status = GoogleCalendarConnectionStatus.ReauthorizationRequired;
            connection.ProtectedAccessToken = null;
            connection.AccessTokenExpiresAt = null;
            connection.UpdatedAt = timeProvider.GetUtcNow();
            dbContext.Update(connection);
            await dbContext.SaveChangesAsync(cancellationToken);
            throw new CalendarOperationException(409,
                Common.ApiProblemCodes.CalendarReauthorizationRequired, "Reconnect Google Calendar.");
        }
    }

    private async Task MarkReauthorizationRequiredAsync(
        GoogleCalendarConnection connection, CancellationToken cancellationToken)
    {
        connection.Status = GoogleCalendarConnectionStatus.ReauthorizationRequired;
        connection.ProtectedAccessToken = null;
        connection.AccessTokenExpiresAt = null;
        connection.UpdatedAt = timeProvider.GetUtcNow();
        dbContext.Update(connection);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private NormalizedCreateEvent ValidateAndNormalize(CreateCalendarEventRequest request)
    {
        if (request.SourceId == Guid.Empty || request.IdempotencyKey == Guid.Empty)
            throw Validation("A calendar source and request identifier are required.");
        var title = request.Title?.Trim() ?? string.Empty;
        var location = NullIfWhiteSpace(request.Location);
        var notes = NullIfWhiteSpace(request.Notes);
        if (title.Length is < 1 or > 200)
            throw Validation("Event titles must contain 1 to 200 characters.");
        if (location?.Length > 500)
            throw Validation("Event locations may contain no more than 500 characters.");
        if (notes?.Length > 2000)
            throw Validation("Event notes may contain no more than 2,000 characters.");
        if (request.IsAllDay)
        {
            if (!DateOnly.TryParseExact(request.Start, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var startDate)
                || !DateOnly.TryParseExact(request.End, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var endDate)
                || endDate <= startDate
                || endDate.DayNumber - startDate.DayNumber > 31
                || !string.IsNullOrWhiteSpace(request.TimeZone))
                throw Validation("All-day events require valid start and exclusive end dates of no more than 31 days.");
            var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
            if (startDate < today.AddDays(-1) || startDate > today.AddYears(2))
                throw Validation("Choose an all-day event between yesterday and two years from now.");
            return new NormalizedCreateEvent(
                title, location, notes, true,
                startDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                endDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                null);
        }
        if (!DateTimeOffset.TryParse(request.Start, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var start)
            || !DateTimeOffset.TryParse(request.End, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var end)
            || end <= start
            || end - start > TimeSpan.FromDays(31)
            || string.IsNullOrWhiteSpace(request.TimeZone))
            throw Validation("Timed events require a valid start, end, and IANA time zone.");
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(request.TimeZone); }
        catch (TimeZoneNotFoundException) { throw Validation("The event time zone is invalid."); }
        catch (InvalidTimeZoneException) { throw Validation("The event time zone is invalid."); }
        if (zone.GetUtcOffset(start) != start.Offset || zone.GetUtcOffset(end) != end.Offset)
            throw Validation("The event offsets do not match the selected time zone.");
        var now = timeProvider.GetUtcNow();
        if (start < now.AddDays(-1) || start > now.AddYears(2))
            throw Validation("Choose an event between yesterday and two years from now.");
        return new NormalizedCreateEvent(
            title, location, notes, false,
            start.ToString("O", CultureInfo.InvariantCulture),
            end.ToString("O", CultureInfo.InvariantCulture),
            request.TimeZone);
    }

    private static CalendarOperationException Validation(string message) => new(
        400, Common.ApiProblemCodes.ValidationFailed, message);

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool CanCreateEvents(string accessRole) =>
        accessRole is "writer" or "owner";

    private static bool HasEventCreationScope(GoogleCalendarConnection connection) =>
        connection.GrantedScopes.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Contains(GoogleCalendarScopes.EventsWrite, StringComparer.Ordinal);

    private void RequireAvailable()
    {
        if (!_configuration.Enabled)
            throw new CalendarOperationException(503,
                Common.ApiProblemCodes.GoogleCalendarUnavailable,
                "Google Calendar is not configured.");
    }

    private void RequireEventCreationAvailable()
    {
        RequireAvailable();
        if (!_configuration.EventCreationEnabled)
            throw new CalendarOperationException(503,
                Common.ApiProblemCodes.CalendarEventCreationUnavailable,
                "Google Calendar event creation is not enabled.");
    }

    private static string ToContract(GoogleCalendarConnectionStatus status) => status switch
    {
        GoogleCalendarConnectionStatus.Active => "connected",
        GoogleCalendarConnectionStatus.ReauthorizationRequired => "reauthorizationRequired",
        _ => "disconnected",
    };

    private sealed record EventCache(GoogleProviderEventPage Page, DateTimeOffset FetchedAt);
    private sealed record NormalizedCreateEvent(
        string Title,
        string? Location,
        string? Notes,
        bool IsAllDay,
        string Start,
        string End,
        string? TimeZone);
}

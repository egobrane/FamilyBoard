using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using FamilyDashboard.Api.Configuration;
using FamilyDashboard.Api.Domain.Integrations;
using FamilyDashboard.Api.Features.Common;
using FamilyDashboard.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Npgsql;

namespace FamilyDashboard.Api.Features.Calendar;

public sealed class CalendarEventManagementService(
    FamilyDashboardDbContext dbContext,
    GoogleCalendarService calendarService,
    IGoogleCalendarProviderClient provider,
    IMemoryCache cache,
    IOptions<GoogleCalendarConfiguration> options,
    TimeProvider timeProvider)
{
    private readonly GoogleCalendarConfiguration _configuration = options.Value;

    public async Task<ManagedCalendarEventResponse> GetAsync(
        Guid householdId, Guid managementId, CancellationToken cancellationToken)
    {
        RequireAvailable();
        var context = await LoadAsync(householdId, managementId, cancellationToken);
        var providerEvent = await GetProviderEventAsync(context, cancellationToken);
        return ToResponse(context, providerEvent);
    }

    public async Task<CalendarEventMutationResponse> UpdateAsync(
        Guid householdId, Guid managementId, Guid userAccountId, Guid sessionId,
        UpdateCalendarEventRequest request, string traceId, CancellationToken cancellationToken)
    {
        RequireAvailable();
        if (request.IdempotencyKey == Guid.Empty || string.IsNullOrWhiteSpace(request.ExpectedProviderVersion))
            throw Validation("A request identifier and Google event version are required.");
        var context = await LoadAsync(householdId, managementId, cancellationToken);
        var actor = await RequireActorAsync(householdId, userAccountId, sessionId, cancellationToken);
        var normalized = Normalize(request);
        var fingerprint = Fingerprint(new
        {
            managementId,
            operation = "update",
            request.ExpectedProviderVersion,
            normalized.Title,
            normalized.Location,
            normalized.Notes,
            normalized.IsAllDay,
            normalized.Start,
            normalized.End,
            normalized.TimeZone,
        });
        var (receipt, recovered) = await GetOrCreateReceiptAsync(
            context, actor, request.IdempotencyKey, CalendarEventMutationOperation.Update,
            request.ExpectedProviderVersion!, fingerprint, traceId, cancellationToken);
        if (receipt.Status == CalendarEventMutationReceiptStatus.Succeeded)
        {
            var existing = await GetProviderEventAsync(context, cancellationToken);
            return new("update", receipt.CompletedAt!.Value, true, ToResponse(context, existing));
        }

        var current = await GetProviderEventAsync(context, cancellationToken);
        EnsureSupported(current);
        if (!string.Equals(current.ProviderVersion, request.ExpectedProviderVersion, StringComparison.Ordinal))
        {
            if (Matches(current, normalized))
            {
                Complete(receipt, current.ProviderVersion);
                await dbContext.SaveChangesAsync(cancellationToken);
                Invalidate(context.Source.Id);
                return new("update", receipt.CompletedAt!.Value, true, ToResponse(context, current));
            }
            throw VersionConflict();
        }

        var accessToken = await calendarService.GetAccessTokenAsync(
            context.Source.GoogleCalendarConnection, cancellationToken);
        var updated = await RunProviderAsync(context, () => provider.UpdateEventAsync(
            accessToken,
            context.Source.ExternalCalendarId,
            context.Creation.ProviderEventId!,
            request.ExpectedProviderVersion!,
            new GoogleProviderUpdateEvent(normalized.Title, normalized.Location, normalized.Notes,
                normalized.IsAllDay, normalized.Start, normalized.End, normalized.TimeZone),
            cancellationToken), cancellationToken);
        Complete(receipt, updated.ProviderVersion);
        await dbContext.SaveChangesAsync(cancellationToken);
        Invalidate(context.Source.Id);
        return new("update", receipt.CompletedAt!.Value, recovered, ToResponse(context, updated));
    }

    public async Task<CalendarEventMutationResponse> DeleteAsync(
        Guid householdId, Guid managementId, Guid userAccountId, Guid sessionId,
        DeleteCalendarEventRequest request, string traceId, CancellationToken cancellationToken)
    {
        RequireAvailable();
        if (!request.ConfirmDelete)
            throw new CalendarOperationException(409, ApiProblemCodes.CalendarEventDeleteConfirmationRequired,
                "Confirm that this event should be deleted from Google Calendar.");
        if (request.IdempotencyKey == Guid.Empty || string.IsNullOrWhiteSpace(request.ExpectedProviderVersion))
            throw Validation("A request identifier and Google event version are required.");
        var context = await LoadAsync(householdId, managementId, cancellationToken);
        var actor = await RequireActorAsync(householdId, userAccountId, sessionId, cancellationToken);
        var fingerprint = Fingerprint(new
        {
            managementId,
            operation = "delete",
            request.ExpectedProviderVersion,
            request.ConfirmDelete,
        });
        var (receipt, recovered) = await GetOrCreateReceiptAsync(
            context, actor, request.IdempotencyKey, CalendarEventMutationOperation.Delete,
            request.ExpectedProviderVersion!, fingerprint, traceId, cancellationToken);
        if (receipt.Status == CalendarEventMutationReceiptStatus.Succeeded)
            return new("delete", receipt.CompletedAt!.Value, true, null);

        GoogleProviderEvent current;
        try { current = await GetProviderEventAsync(context, cancellationToken); }
        catch (CalendarOperationException exception) when (exception.Code == ApiProblemCodes.CalendarEventNotFound)
        {
            Complete(receipt, null);
            await dbContext.SaveChangesAsync(cancellationToken);
            Invalidate(context.Source.Id);
            return new("delete", receipt.CompletedAt!.Value, true, null);
        }
        EnsureSupported(current);
        if (!string.Equals(current.ProviderVersion, request.ExpectedProviderVersion, StringComparison.Ordinal))
            throw VersionConflict();
        try
        {
            var token = await calendarService.GetAccessTokenAsync(
                context.Source.GoogleCalendarConnection, cancellationToken);
            await provider.DeleteEventAsync(token, context.Source.ExternalCalendarId,
                context.Creation.ProviderEventId!, request.ExpectedProviderVersion!, cancellationToken);
        }
        catch (GoogleCalendarProviderException exception)
            when (exception.Failure == GoogleCalendarProviderFailure.NotFound)
        {
            recovered = true;
        }
        catch (GoogleCalendarProviderException exception)
        {
            await ThrowMappedAsync(context, exception, cancellationToken);
        }
        Complete(receipt, null);
        await dbContext.SaveChangesAsync(cancellationToken);
        Invalidate(context.Source.Id);
        return new("delete", receipt.CompletedAt!.Value, recovered, null);
    }

    private async Task<ManagementContext> LoadAsync(
        Guid householdId, Guid managementId, CancellationToken cancellationToken)
    {
        var creation = await dbContext.CalendarEventCreationReceipts.AsNoTracking()
            .SingleOrDefaultAsync(item => item.HouseholdId == householdId
                && item.Id == managementId
                && item.Status == CalendarEventCreationReceiptStatus.Succeeded
                && item.ProviderEventId != null, cancellationToken)
            ?? throw new CalendarOperationException(404, ApiProblemCodes.CalendarEventNotManaged,
                "This household-created calendar event was not found.");
        var source = await dbContext.HouseholdCalendarSources
            .Include(item => item.GoogleCalendarConnection).ThenInclude(item => item.UserAccount)
            .SingleOrDefaultAsync(item => item.HouseholdId == householdId
                && item.Id == creation.HouseholdCalendarSourceId
                && item.IsActive
                && item.GoogleCalendarConnection.Status == GoogleCalendarConnectionStatus.Active
                && item.GoogleCalendarConnection.UserAccount.IsActive, cancellationToken)
            ?? throw new CalendarOperationException(404, ApiProblemCodes.CalendarEventNotManaged,
                "The calendar source for this event is no longer active.");
        return new(creation, source);
    }

    private async Task<GoogleProviderEvent> GetProviderEventAsync(
        ManagementContext context, CancellationToken cancellationToken)
    {
        try
        {
            var token = await calendarService.GetAccessTokenAsync(
                context.Source.GoogleCalendarConnection, cancellationToken);
            return await provider.GetEventAsync(token, context.Source.ExternalCalendarId,
                context.Creation.ProviderEventId!, cancellationToken);
        }
        catch (GoogleCalendarProviderException exception)
        {
            await ThrowMappedAsync(context, exception, cancellationToken);
            throw;
        }
    }

    private async Task<T> RunProviderAsync<T>(
        ManagementContext context, Func<Task<T>> action, CancellationToken cancellationToken)
    {
        try { return await action(); }
        catch (GoogleCalendarProviderException exception)
        {
            await ThrowMappedAsync(context, exception, cancellationToken);
            throw;
        }
    }

    private async Task ThrowMappedAsync(
        ManagementContext context, GoogleCalendarProviderException exception,
        CancellationToken cancellationToken)
    {
        if (exception.Failure == GoogleCalendarProviderFailure.ReauthorizationRequired)
        {
            await calendarService.MarkReauthorizationRequiredAsync(
                context.Source.GoogleCalendarConnection, cancellationToken);
            throw new CalendarOperationException(409, ApiProblemCodes.CalendarReauthorizationRequired,
                "Reconnect Google Calendar.");
        }
        throw exception.Failure switch
        {
            GoogleCalendarProviderFailure.NotFound => new CalendarOperationException(404,
                ApiProblemCodes.CalendarEventNotFound, "The Google Calendar event no longer exists."),
            GoogleCalendarProviderFailure.VersionConflict => VersionConflict(),
            GoogleCalendarProviderFailure.WriteForbidden => new CalendarOperationException(409,
                ApiProblemCodes.CalendarEventWriteForbidden, "Google Calendar no longer allows this event to be changed."),
            _ => exception,
        };
    }

    private async Task<(Guid UserId, Guid MemberId, bool Shared)> RequireActorAsync(
        Guid householdId, Guid userAccountId, Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await dbContext.UserSessions.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == sessionId && item.UserAccountId == userAccountId
            && item.RevokedAt == null && item.SelectedHouseholdId == householdId, cancellationToken)
            ?? throw new CalendarOperationException(409, ApiProblemCodes.HouseholdSelectionRequired,
                "Select this household before changing an event.");
        var memberId = await dbContext.HouseholdMemberships.AsNoTracking()
            .Where(item => item.HouseholdId == householdId && item.UserAccountId == userAccountId
                && item.HouseholdMember.IsActive)
            .Select(item => (Guid?)item.HouseholdMemberId).SingleOrDefaultAsync(cancellationToken)
            ?? throw new CalendarOperationException(403, ApiProblemCodes.AdultAccessRequired,
                "An active adult household member is required.");
        return (userAccountId, memberId, session.IsSharedDisplay);
    }

    private async Task<(CalendarEventMutationReceipt Receipt, bool Recovered)> GetOrCreateReceiptAsync(
        ManagementContext context, (Guid UserId, Guid MemberId, bool Shared) actor, Guid id,
        CalendarEventMutationOperation operation, string expectedVersion, byte[] fingerprint,
        string traceId, CancellationToken cancellationToken)
    {
        var existing = await dbContext.CalendarEventMutationReceipts.SingleOrDefaultAsync(
            item => item.HouseholdId == context.Creation.HouseholdId && item.Id == id,
            cancellationToken);
        if (existing is not null)
        {
            if (!CryptographicOperations.FixedTimeEquals(existing.RequestFingerprint, fingerprint))
                throw new CalendarOperationException(409,
                    ApiProblemCodes.CalendarEventMutationIdempotencyConflict,
                    "This request identifier was already used for a different event change.");
            return (existing, true);
        }
        var receipt = new CalendarEventMutationReceipt
        {
            Id = id,
            HouseholdId = context.Creation.HouseholdId,
            CalendarEventCreationReceiptId = context.Creation.Id,
            HouseholdCalendarSourceId = context.Source.Id,
            Operation = operation,
            RequestedByUserAccountId = actor.UserId,
            ActingHouseholdMemberId = actor.MemberId,
            RequestedFromSharedDisplay = actor.Shared,
            RequestFingerprint = fingerprint,
            ExpectedProviderVersion = expectedVersion,
            TraceId = traceId.Length <= 128 ? traceId : traceId[..128],
            CreatedAt = timeProvider.GetUtcNow(),
        };
        dbContext.CalendarEventMutationReceipts.Add(receipt);
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            dbContext.Entry(receipt).State = EntityState.Detached;
            existing = await dbContext.CalendarEventMutationReceipts.SingleAsync(item =>
                item.HouseholdId == context.Creation.HouseholdId && item.Id == id, cancellationToken);
            if (!CryptographicOperations.FixedTimeEquals(existing.RequestFingerprint, fingerprint))
                throw new CalendarOperationException(409,
                    ApiProblemCodes.CalendarEventMutationIdempotencyConflict,
                    "This request identifier was already used for a different event change.");
            return (existing, true);
        }
        return (receipt, false);
    }

    private static ManagedCalendarEventResponse ToResponse(ManagementContext context, GoogleProviderEvent item)
    {
        var canManage = !item.IsRecurring && !item.HasUnsupportedStructure
            && !string.IsNullOrWhiteSpace(item.ProviderVersion);
        var reason = item.IsRecurring ? "Recurring events remain read-only."
            : item.HasUnsupportedStructure ? "This event has Google features Family Dashboard does not modify."
            : string.IsNullOrWhiteSpace(item.ProviderVersion) ? "Google did not provide a version for this event."
            : null;
        return new(context.Creation.Id, context.Source.Id, context.Source.DisplayNameSnapshot,
            item.Title, item.Location, item.Notes, item.IsAllDay, item.Start, item.End,
            item.TimeZone, item.ProviderVersion, canManage, canManage, reason);
    }

    private static void EnsureSupported(GoogleProviderEvent item)
    {
        if (item.IsRecurring || item.HasUnsupportedStructure || string.IsNullOrWhiteSpace(item.ProviderVersion))
            throw new CalendarOperationException(409, ApiProblemCodes.CalendarEventUnsupported,
                "This Google Calendar event is read-only in Family Dashboard.");
    }

    private static NormalizedEvent Normalize(UpdateCalendarEventRequest request)
    {
        var create = new CreateCalendarEventRequest(Guid.NewGuid(), Guid.NewGuid(), null,
            request.Title, request.Location, request.Notes, request.IsAllDay,
            request.Start, request.End, request.TimeZone);
        // Keep update validation identical to creation without accepting source or attribution from the client.
        var title = create.Title?.Trim() ?? string.Empty;
        var location = NullIfWhiteSpace(create.Location);
        var notes = NullIfWhiteSpace(create.Notes);
        if (title.Length is < 1 or > 200 || location?.Length > 500 || notes?.Length > 2000)
            throw Validation("Check the event title, location, and notes lengths.");
        if (create.IsAllDay)
        {
            if (!DateOnly.TryParseExact(create.Start, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start)
                || !DateOnly.TryParseExact(create.End, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end)
                || end <= start || end.DayNumber - start.DayNumber > 31 || !string.IsNullOrWhiteSpace(create.TimeZone))
                throw Validation("All-day events require valid start and exclusive end dates of no more than 31 days.");
            return new(title, location, notes, true,
                start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), null);
        }
        if (!DateTimeOffset.TryParse(create.Start, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var startTime)
            || !DateTimeOffset.TryParse(create.End, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var endTime)
            || endTime <= startTime || endTime - startTime > TimeSpan.FromDays(31)
            || string.IsNullOrWhiteSpace(create.TimeZone))
            throw Validation("Timed events require a valid start, end, and IANA time zone.");
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(create.TimeZone);
            if (zone.GetUtcOffset(startTime) != startTime.Offset || zone.GetUtcOffset(endTime) != endTime.Offset)
                throw Validation("The event offsets do not match the selected time zone.");
        }
        catch (TimeZoneNotFoundException) { throw Validation("The event time zone is invalid."); }
        catch (InvalidTimeZoneException) { throw Validation("The event time zone is invalid."); }
        return new(title, location, notes, false, startTime.ToString("O"), endTime.ToString("O"), create.TimeZone);
    }

    private static bool Matches(GoogleProviderEvent item, NormalizedEvent expected) =>
        item.Title == expected.Title && item.Location == expected.Location && item.Notes == expected.Notes
        && item.IsAllDay == expected.IsAllDay && item.Start == expected.Start && item.End == expected.End
        && item.TimeZone == expected.TimeZone;

    private static byte[] Fingerprint(object value) => SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value));
    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static CalendarOperationException Validation(string message) => new(400, ApiProblemCodes.ValidationFailed, message);
    private static CalendarOperationException VersionConflict() => new(409,
        ApiProblemCodes.CalendarEventVersionConflict, "The Google Calendar event changed. Reload it before trying again.");
    private void Complete(CalendarEventMutationReceipt receipt, string? version)
    {
        receipt.Status = CalendarEventMutationReceiptStatus.Succeeded;
        receipt.ResultProviderVersion = version;
        receipt.CompletedAt = timeProvider.GetUtcNow();
    }
    private void Invalidate(Guid sourceId) => cache.Set($"calendar-version:{sourceId:D}",
        Guid.NewGuid().ToString("N"), _configuration.StaleCacheLifetime);
    private void RequireAvailable()
    {
        if (!_configuration.Enabled || !_configuration.EventCreationEnabled || !_configuration.EventManagementEnabled)
            throw new CalendarOperationException(503, ApiProblemCodes.CalendarEventManagementUnavailable,
                "Google Calendar event management is not enabled.");
    }

    private sealed record ManagementContext(
        CalendarEventCreationReceipt Creation, HouseholdCalendarSource Source);
    private sealed record NormalizedEvent(
        string Title, string? Location, string? Notes, bool IsAllDay,
        string Start, string End, string? TimeZone);
}

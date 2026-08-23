namespace FamilyDashboard.Api.Features.Chores;

using FamilyDashboard.Api.Domain.Chores;

public sealed class ChoreDueTimeService(TimeProvider timeProvider)
{
    public bool TryResolve(DateOnly localDate, TimeOnly? localTime, string timeZoneId,
        out DateTimeOffset dueAt, out string? error)
    {
        dueAt = default;
        error = null;
        TimeZoneInfo timeZone;
        try { timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId); }
        catch (TimeZoneNotFoundException) { error = "The household time zone is unavailable."; return false; }
        catch (InvalidTimeZoneException) { error = "The household time zone is invalid."; return false; }

        var now = timeProvider.GetUtcNow();
        if (localDate > DateOnly.FromDateTime(now.UtcDateTime.AddYears(2)))
        {
            error = "The due date cannot be more than two years in the future.";
            return false;
        }

        var local = localDate.ToDateTime(localTime ?? TimeOnly.MaxValue, DateTimeKind.Unspecified);
        if (timeZone.IsInvalidTime(local))
        {
            error = "That local time does not exist because of daylight-saving time.";
            return false;
        }
        if (timeZone.IsAmbiguousTime(local))
        {
            error = "That local time is ambiguous because of daylight-saving time. Choose another time.";
            return false;
        }
        dueAt = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, timeZone), TimeSpan.Zero);
        return true;
    }

    public bool TryResolveRecurring(DateOnly localDate, TimeOnly? localTime, string timeZoneId,
        out DateTimeOffset dueAt, out ChoreDueTimeResolution resolution, out string? error)
    {
        dueAt = default;
        resolution = ChoreDueTimeResolution.Exact;
        error = null;
        TimeZoneInfo timeZone;
        try { timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId); }
        catch (TimeZoneNotFoundException) { error = "The household time zone is unavailable."; return false; }
        catch (InvalidTimeZoneException) { error = "The household time zone is invalid."; return false; }

        var local = localDate.ToDateTime(localTime ?? TimeOnly.MaxValue, DateTimeKind.Unspecified);
        if (timeZone.IsInvalidTime(local))
        {
            var shifted = local;
            for (var minute = 0; minute < 180 && timeZone.IsInvalidTime(shifted); minute++)
                shifted = shifted.AddMinutes(1);
            if (timeZone.IsInvalidTime(shifted)) { error = "The recurring due time cannot be resolved."; return false; }
            local = shifted;
            resolution = ChoreDueTimeResolution.ShiftedForward;
        }

        if (timeZone.IsAmbiguousTime(local))
        {
            var earlierOffset = timeZone.GetAmbiguousTimeOffsets(local).Max();
            dueAt = new DateTimeOffset(local, earlierOffset).ToUniversalTime();
            resolution = ChoreDueTimeResolution.AmbiguousEarlier;
            return true;
        }

        dueAt = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, timeZone), TimeSpan.Zero);
        return true;
    }
}

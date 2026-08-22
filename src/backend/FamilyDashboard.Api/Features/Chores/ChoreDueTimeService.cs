namespace FamilyDashboard.Api.Features.Chores;

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
}

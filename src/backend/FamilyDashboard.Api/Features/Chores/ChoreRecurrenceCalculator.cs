using FamilyDashboard.Api.Domain.Chores;

namespace FamilyDashboard.Api.Features.Chores;

public sealed class ChoreRecurrenceCalculator
{
    public DateOnly? FindNext(ChoreRecurrenceKind kind, int interval, int? daysOfWeekMask,
        DateOnly start, DateOnly? end, DateOnly onOrAfter)
    {
        var candidate = onOrAfter < start ? start : onOrAfter;
        var limit = end ?? start.AddYears(10);
        while (candidate <= limit)
        {
            if (IsOccurrence(kind, interval, daysOfWeekMask, start, candidate)) return candidate;
            candidate = candidate.AddDays(1);
        }
        return null;
    }

    public bool IsOccurrence(ChoreRecurrenceKind kind, int interval, int? daysOfWeekMask,
        DateOnly start, DateOnly candidate)
    {
        if (candidate < start || interval < 1) return false;
        var elapsedDays = candidate.DayNumber - start.DayNumber;
        if (kind == ChoreRecurrenceKind.Daily) return elapsedDays % interval == 0;
        if (daysOfWeekMask is null) return false;
        var startWeekMonday = start.AddDays(-DayOffset(start.DayOfWeek));
        var candidateWeekMonday = candidate.AddDays(-DayOffset(candidate.DayOfWeek));
        var elapsedWeeks = (candidateWeekMonday.DayNumber - startWeekMonday.DayNumber) / 7;
        var dayBit = 1 << DayOffset(candidate.DayOfWeek);
        return elapsedWeeks % interval == 0 && (daysOfWeekMask.Value & dayBit) != 0;
    }

    private static int DayOffset(DayOfWeek day) => ((int)day + 6) % 7;
}

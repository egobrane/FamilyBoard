namespace FamilyDashboard.Api.Features.Chores;

using FamilyDashboard.Api.Domain.Chores;

public static class ChoreValidation
{
    public static bool TryDefinition(Guid clientRequestId, string? title, string? description,
        out (Guid ClientRequestId, string Title, string? Description) values,
        out Dictionary<string, string[]> errors)
    {
        errors = [];
        var cleanTitle = title?.Trim();
        var cleanDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (clientRequestId == Guid.Empty) errors["clientRequestId"] = ["A client request ID is required."];
        if (string.IsNullOrWhiteSpace(cleanTitle) || cleanTitle.Length > 120)
            errors["title"] = ["Title must contain between 1 and 120 characters."];
        if (cleanDescription?.Length > 500)
            errors["description"] = ["Description cannot exceed 500 characters."];
        values = (clientRequestId, cleanTitle ?? string.Empty, cleanDescription);
        return errors.Count == 0;
    }

    public static bool TryNote(string? note, string field, out string? clean,
        out Dictionary<string, string[]> errors)
    {
        errors = [];
        clean = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (clean?.Length > 240) errors[field] = ["The note cannot exceed 240 characters."];
        return errors.Count == 0;
    }

    public static bool TrySchedule(ChoreRecurrenceRequest? recurrence, DateOnly start,
        DateOnly? end, out (ChoreRecurrenceKind Kind, int Interval, int? DaysMask) values,
        out Dictionary<string, string[]> errors)
    {
        errors = [];
        values = default;
        if (recurrence is null)
        {
            errors["recurrence"] = ["A recurrence is required."];
            return false;
        }

        if (!Enum.TryParse<ChoreRecurrenceKind>(recurrence.Kind, true, out var kind))
            errors["recurrence.kind"] = ["Choose daily or weekly."];
        var maximum = kind == ChoreRecurrenceKind.Weekly ? 12 : 30;
        if (recurrence.Interval is < 1 || recurrence.Interval > maximum)
            errors["recurrence.interval"] = [$"Interval must be between 1 and {maximum}."];
        if (end < start) errors["endLocalDate"] = ["End date cannot precede start date."];

        int? mask = null;
        var days = recurrence.DaysOfWeek ?? [];
        if (kind == ChoreRecurrenceKind.Daily && days.Count > 0)
            errors["recurrence.daysOfWeek"] = ["Daily schedules cannot select weekdays."];
        if (kind == ChoreRecurrenceKind.Weekly)
        {
            mask = 0;
            foreach (var day in days.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!Enum.TryParse<DayOfWeek>(day, true, out var parsed))
                {
                    errors["recurrence.daysOfWeek"] = ["Choose valid weekday names."];
                    break;
                }
                mask |= 1 << (((int)parsed + 6) % 7);
            }
            if (mask == 0) errors["recurrence.daysOfWeek"] = ["Choose at least one weekday."];
        }

        values = (kind, recurrence.Interval, mask);
        return errors.Count == 0;
    }
}

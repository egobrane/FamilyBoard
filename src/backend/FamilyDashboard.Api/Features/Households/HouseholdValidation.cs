using System.Globalization;

namespace FamilyDashboard.Api.Features.Households;

internal sealed record ValidatedHouseholdValues(
    string Name,
    string TimeZone,
    string Locale,
    DayOfWeek WeekStartsOn);

internal sealed record ValidatedHouseholdPatch(
    string? Name,
    string? TimeZone,
    string? Locale,
    DayOfWeek? WeekStartsOn);

internal static class HouseholdValidation
{
    private static readonly HashSet<string> KnownLocales = CultureInfo
        .GetCultures(CultureTypes.AllCultures)
        .Select(culture => culture.Name)
        .Where(name => !string.IsNullOrEmpty(name))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static bool TryValidate(
        CreateHouseholdRequest? request,
        out ValidatedHouseholdValues? values,
        out IDictionary<string, string[]> errors)
    {
        var validationErrors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (request is null)
        {
            validationErrors["request"] = ["A request body is required."];
            values = null;
            errors = validationErrors;
            return false;
        }

        var name = ValidateRequiredText(request.Name, 120, "name", validationErrors);
        var timeZone = ValidateTimeZone(request.TimeZone, validationErrors);
        var locale = ValidateLocale(request.Locale, validationErrors);
        var weekStartsOn = ValidateWeekStartsOn(request.WeekStartsOn, validationErrors);

        if (validationErrors.Count > 0)
        {
            values = null;
            errors = validationErrors;
            return false;
        }

        values = new ValidatedHouseholdValues(name!, timeZone!, locale!, weekStartsOn!.Value);
        errors = validationErrors;
        return true;
    }

    public static bool TryValidate(
        UpdateHouseholdRequest? request,
        out ValidatedHouseholdPatch? values,
        out IDictionary<string, string[]> errors)
    {
        var validationErrors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (request is null)
        {
            validationErrors["request"] = ["A request body is required."];
            values = null;
            errors = validationErrors;
            return false;
        }

        if (request.Name is null
            && request.TimeZone is null
            && request.Locale is null
            && request.WeekStartsOn is null)
        {
            validationErrors["request"] = ["At least one household setting must be supplied."];
        }

        var name = request.Name is null
            ? null
            : ValidateRequiredText(request.Name, 120, "name", validationErrors);
        var timeZone = request.TimeZone is null
            ? null
            : ValidateTimeZone(request.TimeZone, validationErrors);
        var locale = request.Locale is null
            ? null
            : ValidateLocale(request.Locale, validationErrors);
        var weekStartsOn = request.WeekStartsOn is null
            ? null
            : ValidateWeekStartsOn(request.WeekStartsOn, validationErrors);

        if (validationErrors.Count > 0)
        {
            values = null;
            errors = validationErrors;
            return false;
        }

        values = new ValidatedHouseholdPatch(name, timeZone, locale, weekStartsOn);
        errors = validationErrors;
        return true;
    }

    private static string? ValidateRequiredText(
        string? value,
        int maximumLength,
        string field,
        Dictionary<string, string[]> errors)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            errors[field] = ["A value is required."];
            return null;
        }

        if (normalized.Length > maximumLength)
        {
            errors[field] = [$"The value must not exceed {maximumLength} characters."];
            return null;
        }

        return normalized;
    }

    private static string? ValidateTimeZone(
        string? value,
        Dictionary<string, string[]> errors)
    {
        var normalized = ValidateRequiredText(value, 100, "timeZone", errors);
        if (normalized is null)
        {
            return null;
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(normalized);
            return normalized;
        }
        catch (TimeZoneNotFoundException)
        {
            errors["timeZone"] = ["The time zone is not recognized."];
        }
        catch (InvalidTimeZoneException)
        {
            errors["timeZone"] = ["The time zone is invalid."];
        }

        return null;
    }

    private static string? ValidateLocale(
        string? value,
        Dictionary<string, string[]> errors)
    {
        var normalized = ValidateRequiredText(value, 20, "locale", errors);
        if (normalized is null)
        {
            return null;
        }

        if (!KnownLocales.Contains(normalized))
        {
            errors["locale"] = ["The locale is not recognized."];
            return null;
        }

        return CultureInfo.GetCultureInfo(normalized).Name;
    }

    private static DayOfWeek? ValidateWeekStartsOn(
        string? value,
        Dictionary<string, string[]> errors)
    {
        var normalized = ValidateRequiredText(value, 16, "weekStartsOn", errors);
        if (normalized is not null
            && Enum.TryParse<DayOfWeek>(normalized, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        if (normalized is not null)
        {
            errors["weekStartsOn"] = ["Use a day name from Sunday through Saturday."];
        }

        return null;
    }
}

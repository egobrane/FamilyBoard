namespace FamilyDashboard.Api.Features.HouseholdMembers;

internal sealed record ValidatedHouseholdMemberPatch(
    string? DisplayName,
    string? AvatarColor,
    bool? IsActive);

internal static class HouseholdMemberValidation
{
    public static bool TryValidate(
        CreateChildMemberRequest? request,
        out string? displayName,
        out string? avatarColor,
        out IDictionary<string, string[]> errors)
    {
        var validationErrors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (request is null)
        {
            validationErrors["request"] = ["A request body is required."];
            displayName = null;
            avatarColor = null;
            errors = validationErrors;
            return false;
        }

        displayName = ValidateDisplayName(request.DisplayName, validationErrors);
        avatarColor = ValidateAvatarColor(request.AvatarColor, validationErrors);
        errors = validationErrors;
        return validationErrors.Count == 0;
    }

    public static bool TryValidate(
        UpdateHouseholdMemberRequest? request,
        out ValidatedHouseholdMemberPatch? patch,
        out IDictionary<string, string[]> errors)
    {
        var validationErrors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (request is null)
        {
            validationErrors["request"] = ["A request body is required."];
            patch = null;
            errors = validationErrors;
            return false;
        }

        if (request.DisplayName is null && request.AvatarColor is null && request.IsActive is null)
        {
            validationErrors["request"] = ["At least one member value must be supplied."];
        }

        var displayName = request.DisplayName is null
            ? null
            : ValidateDisplayName(request.DisplayName, validationErrors);
        var avatarColor = request.AvatarColor is null
            ? null
            : ValidateAvatarColor(request.AvatarColor, validationErrors);

        if (validationErrors.Count > 0)
        {
            patch = null;
            errors = validationErrors;
            return false;
        }

        patch = new ValidatedHouseholdMemberPatch(displayName, avatarColor, request.IsActive);
        errors = validationErrors;
        return true;
    }

    private static string? ValidateDisplayName(
        string? value,
        Dictionary<string, string[]> errors)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            errors["displayName"] = ["A display name is required."];
            return null;
        }

        if (normalized.Length > 80)
        {
            errors["displayName"] = ["The display name must not exceed 80 characters."];
            return null;
        }

        return normalized;
    }

    private static string? ValidateAvatarColor(
        string? value,
        Dictionary<string, string[]> errors)
    {
        if (value is null)
        {
            return null;
        }

        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length == 0
            || normalized.Length > 20
            || normalized.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character != '-'))
        {
            errors["avatarColor"] = ["Use a design-token name containing letters, numbers, or hyphens."];
            return null;
        }

        return normalized;
    }
}

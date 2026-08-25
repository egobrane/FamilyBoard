namespace FamilyDashboard.Api.Features.Rewards;

public static class RewardValidation
{
    public static bool TryDefinition(Guid requestId, string? title, string? description, int pointCost,
        out CreateRewardRequest? clean, out Dictionary<string, string[]> errors)
    {
        errors = [];
        var normalizedTitle = title?.Trim() ?? "";
        var normalizedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (requestId == Guid.Empty) errors["clientRequestId"] = ["A request ID is required."];
        if (normalizedTitle.Length is < 1 or > 120) errors["title"] = ["Title must be between 1 and 120 characters."];
        if (normalizedDescription?.Length > 500) errors["description"] = ["Description cannot exceed 500 characters."];
        if (pointCost is < 1 or > 10000) errors["pointCost"] = ["Point cost must be between 1 and 10,000."];
        clean = errors.Count == 0 ? new(requestId, normalizedTitle, normalizedDescription, pointCost) : null;
        return clean is not null;
    }

    public static bool TryNote(string? value, string field, bool required, out string? clean,
        out Dictionary<string, string[]> errors)
    {
        errors = [];
        clean = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (required && clean is null) errors[field] = ["A reason is required."];
        else if (clean?.Length > 240) errors[field] = ["The value cannot exceed 240 characters."];
        return errors.Count == 0;
    }
}

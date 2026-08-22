namespace FamilyDashboard.Api.Features.Chores;

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
}

namespace FamilyDashboard.Api.Features.Dashboard;

internal static class DashboardValidation
{
    public static Dictionary<string, string[]> Validate(UpdateDashboardAppearanceRequest? request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request is null) return new() { ["request"] = ["A request body is required."] };
        if (request.GreetingTitle?.Trim().Length > 80) errors["greetingTitle"] = ["The greeting title cannot exceed 80 characters."];
        if (request.GreetingMessage?.Trim().Length > 240) errors["greetingMessage"] = ["The greeting message cannot exceed 240 characters."];
        if (request.PhotoFocalX is < 0 or > 1) errors["photoFocalX"] = ["The horizontal focal position must be between 0 and 1."];
        if (request.PhotoFocalY is < 0 or > 1) errors["photoFocalY"] = ["The vertical focal position must be between 0 and 1."];
        if (request.ExpectedVersion < 1) errors["expectedVersion"] = ["A valid version is required."];
        return errors;
    }

    public static Dictionary<string, string[]> Validate(UpdateWeatherSettingsRequest? request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request is null) return new() { ["request"] = ["A request body is required."] };
        if (request.Latitude is < -90 or > 90) errors["latitude"] = ["Latitude must be between -90 and 90."];
        if (request.Longitude is < -180 or > 180) errors["longitude"] = ["Longitude must be between -180 and 180."];
        if (string.IsNullOrWhiteSpace(request.LocationLabel) || request.LocationLabel.Trim().Length > 100)
            errors["locationLabel"] = ["Provide a location label of 100 characters or fewer."];
        if (request.TemperatureUnit is not ("auto" or "fahrenheit" or "celsius"))
            errors["temperatureUnit"] = ["Temperature unit must be auto, fahrenheit, or celsius."];
        if (request.ExpectedVersion is < 1) errors["expectedVersion"] = ["A valid version is required."];
        return errors;
    }
}

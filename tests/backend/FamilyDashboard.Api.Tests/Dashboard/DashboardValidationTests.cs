using FamilyDashboard.Api.Features.Dashboard;

namespace FamilyDashboard.Api.Tests.Dashboard;

public sealed class DashboardValidationTests
{
    [Fact]
    public void AppearanceRejectsUnsafeLengthsAndFocalPositions()
    {
        var errors = DashboardValidation.Validate(new UpdateDashboardAppearanceRequest(
            new string('x', 81), new string('y', 241), -0.1m, 1.1m, 0));

        Assert.Contains("greetingTitle", errors);
        Assert.Contains("greetingMessage", errors);
        Assert.Contains("photoFocalX", errors);
        Assert.Contains("photoFocalY", errors);
        Assert.Contains("expectedVersion", errors);
    }

    [Fact]
    public void WeatherRejectsOutOfRangeCoordinatesAndUnknownUnit()
    {
        var errors = DashboardValidation.Validate(new UpdateWeatherSettingsRequest(
            91, -181, " ", "kelvin", null));

        Assert.Contains("latitude", errors);
        Assert.Contains("longitude", errors);
        Assert.Contains("locationLabel", errors);
        Assert.Contains("temperatureUnit", errors);
    }
}

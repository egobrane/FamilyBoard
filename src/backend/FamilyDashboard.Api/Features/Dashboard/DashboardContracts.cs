namespace FamilyDashboard.Api.Features.Dashboard;

public sealed record DashboardAppearanceResponse(
    Guid HouseholdId,
    string TimeZone,
    string? GreetingTitle,
    string? GreetingMessage,
    decimal PhotoFocalX,
    decimal PhotoFocalY,
    int Version,
    DashboardPhotoResponse? Photo);

public sealed record DashboardPhotoResponse(
    Guid AssetId,
    string SmallUrl,
    string MediumUrl,
    string LargeUrl,
    int PixelWidth,
    int PixelHeight);

public sealed record UpdateDashboardAppearanceRequest(
    string? GreetingTitle,
    string? GreetingMessage,
    decimal PhotoFocalX,
    decimal PhotoFocalY,
    int ExpectedVersion);

public sealed record WeatherSettingsResponse(
    Guid HouseholdId,
    decimal Latitude,
    decimal Longitude,
    string LocationLabel,
    string TemperatureUnit,
    int Version);

public sealed record UpdateWeatherSettingsRequest(
    decimal Latitude,
    decimal Longitude,
    string? LocationLabel,
    string? TemperatureUnit,
    int? ExpectedVersion);

public sealed record HouseholdWeatherResponse(
    string Status,
    string LocationLabel,
    string TemperatureUnit,
    WeatherCurrentResponse? Current,
    IReadOnlyList<WeatherPeriodResponse> Forecast,
    DateTimeOffset? ObservedAt,
    DateTimeOffset RetrievedAt,
    bool IsStale,
    string Attribution);

public sealed record WeatherCurrentResponse(decimal? Temperature, string Summary, string Icon);

public sealed record WeatherPeriodResponse(
    string Name,
    DateTimeOffset Start,
    DateTimeOffset End,
    decimal? Temperature,
    string TemperatureUnit,
    string Summary,
    string Icon,
    bool IsDaytime);

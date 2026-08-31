namespace FamilyDashboard.Api.Configuration;

public sealed class WeatherConfiguration
{
    public const string SectionName = "Weather";
    public bool Enabled { get; set; }
    public string Provider { get; set; } = "Nws";
    public string BaseUrl { get; set; } = "https://api.weather.gov";
    public string UserAgent { get; set; } = "FamilyDashboard/1.0 (https://family.egobrane.net)";
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(8);
    public TimeSpan FreshLifetime { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan StaleLifetime { get; set; } = TimeSpan.FromHours(6);
}

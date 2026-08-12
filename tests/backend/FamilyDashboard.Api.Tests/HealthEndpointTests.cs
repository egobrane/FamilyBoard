using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Text.Json;
using FamilyDashboard.Api.Features.Common;

namespace FamilyDashboard.Api.Tests;

public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task LivenessEndpointReportsHealthyWithoutDatabaseAccess()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ProductionAuthenticationSeamFailsClosedWithProblemDetails()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            ApiProblemCodes.AuthenticationRequired,
            document.RootElement.GetProperty("code").GetString());
    }
}

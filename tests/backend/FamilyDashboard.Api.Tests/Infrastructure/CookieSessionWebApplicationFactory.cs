using FamilyDashboard.Api.Features.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FamilyDashboard.Api.Tests.Infrastructure;

internal sealed class CookieSessionWebApplicationFactory(
    string connectionString,
    bool enableGoogle = false) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:FamilyDashboard", connectionString);
        builder.UseSetting("Cors:AllowedOrigins:0", "https://family.egobrane.net");
        builder.UseSetting("Authentication:FrontendOrigin", "https://family.egobrane.net");
        builder.UseSetting("Authentication:Google:Enabled", enableGoogle.ToString());
        if (enableGoogle)
        {
            builder.UseSetting("Authentication:Google:ClientId", "test-client-id");
            builder.UseSetting("Authentication:Google:ClientSecret", "test-client-secret");
        }
    }
}

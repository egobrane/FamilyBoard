using FamilyDashboard.Api.Persistence;
using FamilyDashboard.Api.Tests.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyDashboard.Api.Tests.Infrastructure;

internal sealed class IdentityHouseholdWebApplicationFactory(
    string connectionString,
    bool enableCalendar = false,
    bool enableCalendarEventCreation = false,
    Action<IServiceCollection>? configureServices = null)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:FamilyDashboard", connectionString);
        builder.UseSetting("ParentAccess:Enabled", "true");
        builder.UseSetting("GoogleCalendar:Enabled", enableCalendar.ToString());
        builder.UseSetting(
            "GoogleCalendar:EventCreationEnabled",
            enableCalendarEventCreation.ToString());
        builder.UseSetting("GoogleCalendar:ClientId", enableCalendar ? "calendar-client-id" : "");
        builder.UseSetting("GoogleCalendar:ClientSecret", enableCalendar ? "calendar-client-secret" : "");
        builder.UseSetting("GoogleCalendar:CallbackUrl", enableCalendar
            ? "https://api.example.test/api/integrations/google-calendar/callback"
            : "");
        builder.UseSetting(
            "ParentAccess:Pepper",
            "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=");
        builder.UseSetting("ParentAccess:WorkFactor", "1000");
        builder.ConfigureTestServices(services =>
        {
            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultForbidScheme = TestAuthenticationHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName,
                    _ => { });
            configureServices?.Invoke(services);
        });
    }
}

internal sealed class PostgreSqlTestDatabase : IAsyncDisposable
{
    private PostgreSqlTestDatabase(
        FamilyDashboardDbContext dbContext,
        IdentityHouseholdWebApplicationFactory factory)
    {
        DbContext = dbContext;
        Factory = factory;
    }

    public FamilyDashboardDbContext DbContext { get; }
    public IdentityHouseholdWebApplicationFactory Factory { get; }

    public static async Task<PostgreSqlTestDatabase> CreateAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION_STRING")!;
        var options = new DbContextOptionsBuilder<FamilyDashboardDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        var dbContext = new FamilyDashboardDbContext(options);
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.MigrateAsync();
        return new PostgreSqlTestDatabase(
            dbContext,
            new IdentityHouseholdWebApplicationFactory(connectionString));
    }

    public async ValueTask DisposeAsync()
    {
        await DbContext.DisposeAsync();
        await Factory.DisposeAsync();
    }
}

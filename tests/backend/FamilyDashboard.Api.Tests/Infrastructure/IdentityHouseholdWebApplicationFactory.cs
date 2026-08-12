using FamilyDashboard.Api.Persistence;
using FamilyDashboard.Api.Tests.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyDashboard.Api.Tests.Infrastructure;

internal sealed class IdentityHouseholdWebApplicationFactory(string connectionString)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:FamilyDashboard", connectionString);
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

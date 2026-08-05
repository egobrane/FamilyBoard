using FamilyDashboard.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FamilyDashboard.Api.Tests;

[Collection("PostgreSQL integration")]
public sealed class DatabaseMigrationTests
{
    [PostgreSqlFact]
    public async Task InitialMigrationAppliesToAnEmptyPostgreSqlDatabase()
    {
        var connectionString = Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION_STRING")!;

        var connectionDetails = new NpgsqlConnectionStringBuilder(connectionString);
        var databaseName = connectionDetails.Database;
        if (string.IsNullOrWhiteSpace(databaseName)
            || !databaseName.EndsWith("_test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Migration tests only run against databases whose names end in '_test'.");
        }

        var options = new DbContextOptionsBuilder<FamilyDashboardDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var context = new FamilyDashboardDbContext(options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();

        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.True(await context.Database.CanConnectAsync());
    }
}

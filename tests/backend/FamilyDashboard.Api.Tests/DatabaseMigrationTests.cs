using FamilyDashboard.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace FamilyDashboard.Api.Tests;

[Collection("PostgreSQL integration")]
public sealed class DatabaseMigrationTests
{
    [PostgreSqlFact]
    public async Task AllMigrationsApplyToAnEmptyPostgreSqlDatabase()
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
        Assert.True(await context.UserAccounts.AnyAsync() is false);
        Assert.True(await context.HouseholdMemberships.AnyAsync() is false);
    }

    [PostgreSqlFact]
    public async Task IdentityMigrationPreservesExistingHouseholdData()
    {
        var connectionString = Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION_STRING")!;
        var options = new DbContextOptionsBuilder<FamilyDashboardDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var context = new FamilyDashboardDbContext(options);
        await context.Database.EnsureDeletedAsync();
        var migrator = context.Database.GetService<IMigrator>();
        await migrator.MigrateAsync("20260805193026_InitialCoreSchema");

        var householdId = Guid.NewGuid();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Households" ("Id", "Name", "IsActive", "CreatedAt", "UpdatedAt")
            VALUES ({householdId}, {"Existing Household"}, {true}, {DateTimeOffset.UtcNow}, {DateTimeOffset.UtcNow})
            """);

        await migrator.MigrateAsync();

        Assert.Equal(
            "Existing Household",
            await context.Households
                .Where(household => household.Id == householdId)
                .Select(household => household.Name)
                .SingleAsync());
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }
}

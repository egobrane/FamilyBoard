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

    [PostgreSqlFact]
    public async Task ChoreWorkflowMigrationBackfillsAndPreservesExistingChoreHistory()
    {
        var connectionString = Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION_STRING")!;
        var options = new DbContextOptionsBuilder<FamilyDashboardDbContext>().UseNpgsql(connectionString).Options;
        await using var context = new FamilyDashboardDbContext(options);
        await context.Database.EnsureDeletedAsync();
        var migrator = context.Database.GetService<IMigrator>();
        await migrator.MigrateAsync("20260819210734_AddGoogleCalendarEventCreation");
        var householdId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var completionId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Households" ("Id", "Name", "IsActive", "CreatedAt", "UpdatedAt")
            VALUES ({householdId}, {"Existing Chore Family"}, {true}, {now}, {now});
            INSERT INTO "HouseholdConfigurations" ("HouseholdId", "TimeZone", "Locale", "WeekStartsOn", "Theme", "UpdatedAt")
            VALUES ({householdId}, {"America/New_York"}, {"en-US"}, {"Sunday"}, {"system"}, {now});
            INSERT INTO "HouseholdMembers" ("Id", "HouseholdId", "DisplayName", "Role", "IsActive", "CreatedAt", "UpdatedAt")
            VALUES ({memberId}, {householdId}, {"Existing Child"}, {"Child"}, {true}, {now}, {now});
            INSERT INTO "ChoreDefinitions" ("Id", "HouseholdId", "Title", "DefaultPointValue", "IsActive", "CreatedAt", "UpdatedAt")
            VALUES ({definitionId}, {householdId}, {"Existing Chore"}, {0}, {true}, {now}, {now});
            INSERT INTO "ChoreAssignments" ("Id", "ChoreDefinitionId", "HouseholdMemberId", "DueAt", "Status", "CreatedAt", "UpdatedAt")
            VALUES ({assignmentId}, {definitionId}, {memberId}, {now.AddHours(2)}, {"Completed"}, {now}, {now});
            INSERT INTO "ChoreCompletions" ("Id", "ChoreAssignmentId", "CompletedByMemberId", "Status", "CompletedAt")
            VALUES ({completionId}, {assignmentId}, {memberId}, {"Approved"}, {now});
            """);

        await migrator.MigrateAsync();
        context.ChangeTracker.Clear();
        var assignment = await context.ChoreAssignments.SingleAsync(item => item.Id == assignmentId);
        var completion = await context.ChoreCompletions.SingleAsync(item => item.Id == completionId);
        Assert.Equal(householdId, assignment.HouseholdId);
        Assert.Equal("Existing Chore", assignment.TitleSnapshot);
        Assert.Equal("America/New_York", assignment.DueTimeZone);
        Assert.Equal(householdId, completion.HouseholdId);
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }
}

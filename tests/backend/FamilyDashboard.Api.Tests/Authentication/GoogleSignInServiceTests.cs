using System.Security.Claims;
using FamilyDashboard.Api.Configuration;
using FamilyDashboard.Api.Features.Authentication;
using FamilyDashboard.Api.Tests.Infrastructure;
using FamilyDashboard.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FamilyDashboard.Api.Tests.Authentication;

[Collection("PostgreSQL integration")]
public sealed class GoogleSignInServiceTests
{
    [PostgreSqlFact]
    public async Task FirstLoginCreatesIdentityAccountAndRevocableSession()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var sessions = new UserSessionService(
            database.DbContext,
            TimeProvider.System,
            Options.Create(new AuthenticationConfiguration()));
        var service = new GoogleSignInService(
            database.DbContext,
            sessions,
            TimeProvider.System);

        var result = await service.SignInAsync(GooglePrincipal(), default);

        Assert.Equal(GoogleSignInStatus.Success, result.Status);
        Assert.NotNull(result.Account);
        Assert.NotNull(result.Session);
        var identity = Assert.Single(database.DbContext.ExternalIdentities);
        Assert.Equal("google-subject-123", identity.ProviderSubject);
        Assert.True(identity.EmailVerified);
        Assert.Equal(result.Account!.Id, identity.UserAccountId);
        Assert.Equal(result.Account.Id, result.Session!.UserAccountId);
        Assert.False(result.Session.IsSharedDisplay);
    }

    [PostgreSqlFact]
    public async Task RepeatLoginUsesProviderSubjectAndDisabledAccountIsRejected()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var sessions = new UserSessionService(
            database.DbContext,
            TimeProvider.System,
            Options.Create(new AuthenticationConfiguration()));
        var service = new GoogleSignInService(
            database.DbContext,
            sessions,
            TimeProvider.System);
        var first = await service.SignInAsync(GooglePrincipal(), default);

        var second = await service.SignInAsync(
            GooglePrincipal("updated@example.test", "Updated Adult"),
            default);

        Assert.Equal(GoogleSignInStatus.Success, second.Status);
        Assert.Equal(first.Account!.Id, second.Account!.Id);
        Assert.Equal(1, database.DbContext.UserAccounts.Count());
        Assert.Equal(2, database.DbContext.UserSessions.Count());
        Assert.Equal("updated@example.test", second.Account.PrimaryEmail);

        second.Account.IsActive = false;
        await database.DbContext.SaveChangesAsync();
        var disabled = await service.SignInAsync(GooglePrincipal(), default);
        Assert.Equal(GoogleSignInStatus.AccountDisabled, disabled.Status);
        Assert.Equal(2, database.DbContext.UserSessions.Count());
    }

    [PostgreSqlFact]
    public async Task MissingOrUnverifiedIdentityClaimsAreRejected()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var sessions = new UserSessionService(
            database.DbContext,
            TimeProvider.System,
            Options.Create(new AuthenticationConfiguration()));
        var service = new GoogleSignInService(
            database.DbContext,
            sessions,
            TimeProvider.System);
        var principal = GooglePrincipal();
        ((ClaimsIdentity)principal.Identity!).RemoveClaim(
            principal.FindFirst(GoogleSignInService.EmailVerifiedClaim)!);

        var result = await service.SignInAsync(principal, default);

        Assert.Equal(GoogleSignInStatus.InvalidIdentity, result.Status);
        Assert.Empty(database.DbContext.UserAccounts);
        Assert.Empty(database.DbContext.UserSessions);
    }

    [PostgreSqlFact]
    public async Task ConcurrentFirstLoginCreatesOneExternalIdentityAndTwoSessions()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var connectionString = Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION_STRING")!;
        var contextOptions = new DbContextOptionsBuilder<FamilyDashboardDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var firstContext = new FamilyDashboardDbContext(contextOptions);
        await using var secondContext = new FamilyDashboardDbContext(contextOptions);
        var firstService = CreateService(firstContext);
        var secondService = CreateService(secondContext);

        var results = await Task.WhenAll(
            firstService.SignInAsync(GooglePrincipal(), default),
            secondService.SignInAsync(GooglePrincipal(), default));

        Assert.All(results, result => Assert.Equal(GoogleSignInStatus.Success, result.Status));
        await using var verification = new FamilyDashboardDbContext(contextOptions);
        Assert.Equal(1, await verification.UserAccounts.CountAsync());
        Assert.Equal(1, await verification.ExternalIdentities.CountAsync());
        Assert.Equal(2, await verification.UserSessions.CountAsync());
    }

    private static ClaimsPrincipal GooglePrincipal(
        string email = "alex@example.test",
        string name = "Alex Adult") =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "google-subject-123"),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Name, name),
            new Claim(GoogleSignInService.EmailVerifiedClaim, "true"),
        ], AuthenticationSchemes.ExternalCookie));

    private static GoogleSignInService CreateService(FamilyDashboardDbContext context)
    {
        var sessions = new UserSessionService(
            context,
            TimeProvider.System,
            Options.Create(new AuthenticationConfiguration()));
        return new GoogleSignInService(context, sessions, TimeProvider.System);
    }
}

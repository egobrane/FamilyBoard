using FamilyDashboard.Api.Configuration;
using FamilyDashboard.Api.Domain.Households;
using FamilyDashboard.Api.Domain.Identity;
using FamilyDashboard.Api.Domain.Integrations;
using FamilyDashboard.Api.Features.Calendar;
using FamilyDashboard.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FamilyDashboard.Api.Tests.Calendar;

[Collection("PostgreSQL integration")]
public sealed class GoogleCalendarServiceTests
{
    [PostgreSqlFact]
    public async Task ExpiredAccessTokenRefreshesAndEventsRemainProviderOwned()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        using var dependencies = Dependencies.Create();
        var seeded = await SeedAsync(database, dependencies.TokenProtector);
        var provider = new FakeProvider
        {
            Events = new GoogleProviderEventPage([
                new GoogleProviderEvent(
                    "event-1", "School concert", false,
                    "2026-08-20T22:00:00.0000000+00:00",
                    "2026-08-21T00:00:00.0000000+00:00",
                    "America/New_York", "Auditorium"),
            ], null),
        };
        var service = dependencies.Service(database, provider);

        var response = await service.ListEventsAsync(
            seeded.Household.Id,
            new DateTimeOffset(2026, 8, 20, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 22, 0, 0, 0, TimeSpan.Zero),
            null,
            CancellationToken.None);

        Assert.Equal(1, provider.RefreshCount);
        Assert.Single(response.Events);
        Assert.Equal("School concert", response.Events[0].Title);
        Assert.Equal("Family", response.Events[0].CalendarName);
        Assert.Equal("Auditorium", response.Events[0].Location);
        Assert.DoesNotContain(database.DbContext.ChangeTracker.Entries(),
            entry => entry.Metadata.ClrType.Name.Contains("Event", StringComparison.Ordinal));
        database.DbContext.ChangeTracker.Clear();
        var connection = await database.DbContext.GoogleCalendarConnections.SingleAsync();
        Assert.DoesNotContain("refreshed-access-token", connection.ProtectedAccessToken);
        Assert.Equal("refreshed-access-token", dependencies.TokenProtector.Unprotect(
            connection.Id, "access-token", connection.ProtectedAccessToken!));
        Assert.NotNull(connection.LastSuccessfulRefreshAt);
    }

    [PostgreSqlFact]
    public async Task DisconnectFailsLocallyClosedWhenProviderRevocationIsUnavailable()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        using var dependencies = Dependencies.Create();
        var seeded = await SeedAsync(database, dependencies.TokenProtector);
        var provider = new FakeProvider { FailRevocation = true };
        var service = dependencies.Service(database, provider);

        await service.DisconnectAsync(
            seeded.Account.Id,
            new DisconnectCalendarRequest(seeded.Connection.Id, true),
            CancellationToken.None);

        database.DbContext.ChangeTracker.Clear();
        var connection = await database.DbContext.GoogleCalendarConnections.SingleAsync();
        Assert.Equal(GoogleCalendarConnectionStatus.Disconnected, connection.Status);
        Assert.Null(connection.ProtectedAccessToken);
        Assert.Null(connection.ProtectedRefreshToken);
        Assert.NotNull(connection.RevokedAt);
        Assert.False(await database.DbContext.HouseholdCalendarSources.Select(source => source.IsActive).SingleAsync());
    }

    private static async Task<Seeded> SeedAsync(
        PostgreSqlTestDatabase database, CalendarTokenProtector tokenProtector)
    {
        var account = new UserAccount { DisplayName = "Calendar Owner", PrimaryEmail = "owner@example.test" };
        var household = new Household { Name = "Calendar Household" };
        household.Configuration = new HouseholdConfiguration { HouseholdId = household.Id };
        var member = new HouseholdMember
        {
            HouseholdId = household.Id,
            DisplayName = account.DisplayName,
            Role = HouseholdMemberRole.Adult,
        };
        var membership = new HouseholdMembership
        {
            UserAccountId = account.Id,
            HouseholdId = household.Id,
            HouseholdMemberId = member.Id,
        };
        household.Members.Add(member);
        household.Memberships.Add(membership);
        account.HouseholdMemberships.Add(membership);
        member.Membership = membership;
        var connection = new GoogleCalendarConnection
        {
            UserAccountId = account.Id,
            ProviderSubject = "calendar-subject",
            ProviderEmailNormalized = "calendar@example.test",
            ProtectedAccessToken = tokenProtector.Protect(Guid.Empty, "unused", "placeholder"),
            ProtectedRefreshToken = "temporary",
            AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            GrantedScopes = string.Join(' ', GoogleCalendarScopes.Required),
        };
        connection.ProtectedAccessToken = tokenProtector.Protect(
            connection.Id, "access-token", "expired-access-token");
        connection.ProtectedRefreshToken = tokenProtector.Protect(
            connection.Id, "refresh-token", "refresh-token");
        var source = new HouseholdCalendarSource
        {
            HouseholdId = household.Id,
            GoogleCalendarConnectionId = connection.Id,
            OwnerUserAccountId = account.Id,
            ExternalCalendarId = "family@example.test",
            DisplayNameSnapshot = "Family",
            Color = "#73b49a",
            AddedByUserAccountId = account.Id,
        };
        database.DbContext.AddRange(account, household, connection, source);
        await database.DbContext.SaveChangesAsync();
        database.DbContext.ChangeTracker.Clear();
        return new Seeded(account, household, connection);
    }

    private sealed record Seeded(
        UserAccount Account, Household Household, GoogleCalendarConnection Connection);

    private sealed class Dependencies : IDisposable
    {
        private readonly ServiceProvider _services;
        private readonly MemoryCache _cache = new(new MemoryCacheOptions());
        private readonly IOptions<GoogleCalendarConfiguration> _options = Options.Create(
            new GoogleCalendarConfiguration { Enabled = true });

        private Dependencies(ServiceProvider services)
        {
            _services = services;
            TokenProtector = new CalendarTokenProtector(
                services.GetRequiredService<IDataProtectionProvider>());
        }

        public CalendarTokenProtector TokenProtector { get; }

        public static Dependencies Create()
        {
            var services = new ServiceCollection().AddDataProtection().Services.BuildServiceProvider();
            return new Dependencies(services);
        }

        public GoogleCalendarService Service(
            PostgreSqlTestDatabase database, IGoogleCalendarProviderClient provider) => new(
                database.DbContext,
                provider,
                TokenProtector,
                new CalendarStateProtector(
                    _services.GetRequiredService<IDataProtectionProvider>(),
                    TimeProvider.System,
                    _options),
                _cache,
                _options,
                TimeProvider.System);

        public void Dispose()
        {
            _cache.Dispose();
            _services.Dispose();
        }
    }

    private sealed class FakeProvider : IGoogleCalendarProviderClient
    {
        public int RefreshCount { get; private set; }
        public bool FailRevocation { get; init; }
        public GoogleProviderEventPage Events { get; init; } = new([], null);

        public string CreateAuthorizationUrl(string state) => throw new NotSupportedException();
        public Task<GoogleCalendarTokenResult> ExchangeCodeAsync(string code, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<GoogleProviderCalendar>> ListCalendarsAsync(
            string accessToken, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<GoogleCalendarRefreshResult> RefreshAsync(
            string refreshToken, CancellationToken cancellationToken)
        {
            RefreshCount++;
            return Task.FromResult(new GoogleCalendarRefreshResult(
                "refreshed-access-token", DateTimeOffset.UtcNow.AddHours(1)));
        }

        public Task<GoogleProviderEventPage> ListEventsAsync(
            string accessToken, string calendarId, DateTimeOffset rangeStart, DateTimeOffset rangeEnd,
            string? pageToken, int maximumResults, CancellationToken cancellationToken)
        {
            Assert.Equal("refreshed-access-token", accessToken);
            return Task.FromResult(Events);
        }

        public Task RevokeAsync(string token, CancellationToken cancellationToken) =>
            FailRevocation
                ? Task.FromException(new GoogleCalendarProviderException(
                    GoogleCalendarProviderFailure.Unavailable, "Unavailable"))
                : Task.CompletedTask;
    }
}

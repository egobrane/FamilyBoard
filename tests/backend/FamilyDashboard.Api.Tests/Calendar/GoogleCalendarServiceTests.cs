using FamilyDashboard.Api.Configuration;
using FamilyDashboard.Api.Domain.Households;
using FamilyDashboard.Api.Domain.Identity;
using FamilyDashboard.Api.Domain.Integrations;
using FamilyDashboard.Api.Features.Calendar;
using FamilyDashboard.Api.Features.Common;
using FamilyDashboard.Api.Persistence;
using FamilyDashboard.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace FamilyDashboard.Api.Tests.Calendar;

[Collection("PostgreSQL integration")]
public sealed class GoogleCalendarServiceTests
{
    [PostgreSqlFact]
    public async Task CanonicalGoogleEmailScopeAllowsEncryptedCalendarConnection()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        using var dependencies = Dependencies.Create();
        var account = new UserAccount
        {
            DisplayName = "Calendar Owner",
            PrimaryEmail = "owner@example.test",
        };
        database.DbContext.UserAccounts.Add(account);
        await database.DbContext.SaveChangesAsync();

        var sessionId = Guid.NewGuid();
        var householdId = Guid.NewGuid();
        var (state, _) = dependencies.StateProtector.CreateAuthorization(
            account.Id, sessionId, householdId, $"/households/{householdId:D}/calendars");
        var provider = new FakeProvider
        {
            ExchangeResult = new GoogleCalendarTokenResult(
                "calendar-access-token",
                "calendar-refresh-token",
                DateTimeOffset.UtcNow.AddHours(1),
                [
                    GoogleCalendarScopes.OpenId,
                    "https://www.googleapis.com/auth/userinfo.email",
                    GoogleCalendarScopes.CalendarListReadOnly,
                    GoogleCalendarScopes.EventsReadOnly,
                ],
                "calendar-subject",
                "Calendar.Owner@Example.Test"),
        };
        var service = dependencies.Service(database, provider);

        var result = await service.CompleteAuthorizationAsync(
            "authorization-code", state, account.Id, sessionId, CancellationToken.None);

        Assert.Equal(householdId, result.HouseholdId);
        database.DbContext.ChangeTracker.Clear();
        var connection = await database.DbContext.GoogleCalendarConnections.SingleAsync();
        Assert.Equal(GoogleCalendarConnectionStatus.Active, connection.Status);
        Assert.Equal("calendar.owner@example.test", connection.ProviderEmailNormalized);
        Assert.DoesNotContain("calendar-access-token", connection.ProtectedAccessToken);
        Assert.DoesNotContain("calendar-refresh-token", connection.ProtectedRefreshToken);
        Assert.Equal("calendar-access-token", dependencies.TokenProtector.Unprotect(
            connection.Id, "access-token", connection.ProtectedAccessToken!));
        Assert.Equal("calendar-refresh-token", dependencies.TokenProtector.Unprotect(
            connection.Id, "refresh-token", connection.ProtectedRefreshToken!));
    }

    [PostgreSqlFact]
    public async Task MissingCalendarDataScopeStillFailsClosed()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        using var dependencies = Dependencies.Create();
        var userAccountId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var householdId = Guid.NewGuid();
        var (state, _) = dependencies.StateProtector.CreateAuthorization(
            userAccountId, sessionId, householdId, $"/households/{householdId:D}/calendars");
        var provider = new FakeProvider
        {
            ExchangeResult = new GoogleCalendarTokenResult(
                "calendar-access-token",
                "calendar-refresh-token",
                DateTimeOffset.UtcNow.AddHours(1),
                [
                    GoogleCalendarScopes.OpenId,
                    "https://www.googleapis.com/auth/userinfo.email",
                    GoogleCalendarScopes.CalendarListReadOnly,
                ],
                "calendar-subject",
                "owner@example.test"),
        };
        var service = dependencies.Service(database, provider);

        var exception = await Assert.ThrowsAsync<CalendarOperationException>(() =>
            service.CompleteAuthorizationAsync(
                "authorization-code", state, userAccountId, sessionId, CancellationToken.None));

        Assert.Equal(ApiProblemCodes.CalendarScopeMissing, exception.Code);
        Assert.Empty(await database.DbContext.GoogleCalendarConnections.ToArrayAsync());
    }

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

    [PostgreSqlFact]
    public async Task CreateEventUsesProviderAsSourceOfTruthAndReplaysIdempotently()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        using var dependencies = Dependencies.Create(enableEventCreation: true);
        var seeded = await SeedAsync(database, dependencies.TokenProtector, enableEventCreation: true);
        var provider = new FakeProvider
        {
            Calendars =
            [
                new GoogleProviderCalendar(
                    "family@example.test", "Family", "America/New_York", "#73b49a", false, "owner"),
            ],
        };
        var service = dependencies.Service(database, provider);
        var eventRange = TomorrowEveningInNewYork();
        var request = new CreateCalendarEventRequest(
            seeded.Source.Id,
            Guid.NewGuid(),
            null,
            "School concert",
            "Auditorium",
            "Bring tickets",
            false,
            eventRange.Start,
            eventRange.End,
            "America/New_York");

        var first = await service.CreateEventAsync(
            seeded.Household.Id, seeded.Account.Id, seeded.Session.Id,
            request, "trace-one", CancellationToken.None);
        database.DbContext.ChangeTracker.Clear();
        var replay = await service.CreateEventAsync(
            seeded.Household.Id, seeded.Account.Id, seeded.Session.Id,
            request, "trace-two", CancellationToken.None);

        Assert.False(first.RecoveredExistingEvent);
        Assert.True(replay.RecoveredExistingEvent);
        Assert.Equal(first.Id, replay.Id);
        Assert.Equal(1, provider.CreateCount);
        Assert.Equal(1, provider.GetCount);
        var receipt = await database.DbContext.CalendarEventCreationReceipts.SingleAsync();
        Assert.Equal(CalendarEventCreationReceiptStatus.Succeeded, receipt.Status);
        Assert.Equal(first.Id, receipt.ProviderEventId);
        Assert.Equal(request.AttributedMemberId ?? seeded.Member.Id, receipt.AttributedHouseholdMemberId);
        Assert.DoesNotContain(database.DbContext.Model.GetEntityTypes(), entity =>
            entity.ClrType.Name is "CalendarEvent" or "GoogleCalendarEvent");
    }

    [PostgreSqlFact]
    public async Task ConcurrentDuplicateEventRequestsConvergeOnOneReceiptAndProviderId()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        using var dependencies = Dependencies.Create(enableEventCreation: true);
        var seeded = await SeedAsync(database, dependencies.TokenProtector, enableEventCreation: true);
        database.DbContext.ChangeTracker.Clear();
        var provider = new FakeProvider
        {
            Calendars =
            [
                new GoogleProviderCalendar(
                    "family@example.test", "Family", "America/New_York", "#73b49a", false, "owner"),
            ],
        };
        var options = new DbContextOptionsBuilder<FamilyDashboardDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION_STRING")!)
            .Options;
        await using var firstContext = new FamilyDashboardDbContext(options);
        await using var secondContext = new FamilyDashboardDbContext(options);
        var eventRange = TomorrowEveningInNewYork();
        var request = new CreateCalendarEventRequest(
            seeded.Source.Id, Guid.NewGuid(), null, "Family dinner", null, null, false,
            eventRange.Start, eventRange.End, "America/New_York");

        var results = await Task.WhenAll(
            dependencies.Service(firstContext, provider).CreateEventAsync(
                seeded.Household.Id, seeded.Account.Id, seeded.Session.Id,
                request, "trace-one", CancellationToken.None),
            dependencies.Service(secondContext, provider).CreateEventAsync(
                seeded.Household.Id, seeded.Account.Id, seeded.Session.Id,
                request, "trace-two", CancellationToken.None));

        Assert.Equal(results[0].Id, results[1].Id);
        database.DbContext.ChangeTracker.Clear();
        Assert.Equal(1, await database.DbContext.CalendarEventCreationReceipts.CountAsync());
    }

    [PostgreSqlFact]
    public async Task SharedDisplayRequiresExplicitActiveHouseholdMemberAttribution()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        using var dependencies = Dependencies.Create(enableEventCreation: true);
        var seeded = await SeedAsync(database, dependencies.TokenProtector, enableEventCreation: true);
        seeded.Session.IsSharedDisplay = true;
        database.DbContext.UserSessions.Update(seeded.Session);
        await database.DbContext.SaveChangesAsync();
        database.DbContext.ChangeTracker.Clear();
        var service = dependencies.Service(database, new FakeProvider
        {
            Calendars =
            [
                new GoogleProviderCalendar(
                    "family@example.test", "Family", "America/New_York", "#73b49a", false, "owner"),
            ],
        });
        var eventRange = TomorrowEveningInNewYork();
        var request = new CreateCalendarEventRequest(
            seeded.Source.Id, Guid.NewGuid(), null, "Shared event", null, null, false,
            eventRange.Start, eventRange.End, "America/New_York");

        var exception = await Assert.ThrowsAsync<CalendarOperationException>(() =>
            service.CreateEventAsync(
                seeded.Household.Id, seeded.Account.Id, seeded.Session.Id,
                request, "shared-trace", CancellationToken.None));

        Assert.Equal(ApiProblemCodes.ValidationFailed, exception.Code);
        Assert.Empty(await database.DbContext.CalendarEventCreationReceipts.ToArrayAsync());
    }

    private static async Task<Seeded> SeedAsync(
        PostgreSqlTestDatabase database, CalendarTokenProtector tokenProtector,
        bool enableEventCreation = false)
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
            GrantedScopes = string.Join(' ', enableEventCreation
                ? GoogleCalendarScopes.ForCapability(CalendarAuthorizationCapabilities.EventCreation)
                : GoogleCalendarScopes.Required),
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
            IsEventCreationTarget = enableEventCreation,
            EventCreationEnabledAt = enableEventCreation ? DateTimeOffset.UtcNow : null,
            EventCreationEnabledByUserAccountId = enableEventCreation ? account.Id : null,
        };
        var session = new UserSession
        {
            UserAccountId = account.Id,
            SelectedHouseholdId = household.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
            AbsoluteExpiresAt = DateTimeOffset.UtcNow.AddDays(2),
        };
        database.DbContext.AddRange(account, household, connection, source, session);
        await database.DbContext.SaveChangesAsync();
        database.DbContext.ChangeTracker.Clear();
        return new Seeded(account, household, member, connection, source, session);
    }

    private static (string Start, string End) TomorrowEveningInNewYork()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var localStart = DateTime.SpecifyKind(
            tomorrow.ToDateTime(new TimeOnly(18, 0)), DateTimeKind.Unspecified);
        var start = new DateTimeOffset(localStart, zone.GetUtcOffset(localStart));
        return (start.ToString("O"), start.AddHours(1).ToString("O"));
    }

    private sealed record Seeded(
        UserAccount Account,
        Household Household,
        HouseholdMember Member,
        GoogleCalendarConnection Connection,
        HouseholdCalendarSource Source,
        UserSession Session);

    private sealed class Dependencies : IDisposable
    {
        private readonly ServiceProvider _services;
        private readonly MemoryCache _cache = new(new MemoryCacheOptions());
        private readonly IOptions<GoogleCalendarConfiguration> _options;

        private Dependencies(ServiceProvider services, bool enableEventCreation)
        {
            _services = services;
            _options = Options.Create(new GoogleCalendarConfiguration
            {
                Enabled = true,
                EventCreationEnabled = enableEventCreation,
            });
            TokenProtector = new CalendarTokenProtector(
                services.GetRequiredService<IDataProtectionProvider>());
            StateProtector = new CalendarStateProtector(
                services.GetRequiredService<IDataProtectionProvider>(),
                TimeProvider.System,
                _options);
        }

        public CalendarTokenProtector TokenProtector { get; }
        public CalendarStateProtector StateProtector { get; }

        public static Dependencies Create(bool enableEventCreation = false)
        {
            var services = new ServiceCollection().AddDataProtection().Services.BuildServiceProvider();
            return new Dependencies(services, enableEventCreation);
        }

        public GoogleCalendarService Service(
            PostgreSqlTestDatabase database, IGoogleCalendarProviderClient provider) => new(
                database.DbContext,
                provider,
                TokenProtector,
                StateProtector,
                _cache,
                _options,
                TimeProvider.System);

        public GoogleCalendarService Service(
            FamilyDashboardDbContext dbContext, IGoogleCalendarProviderClient provider) => new(
                dbContext,
                provider,
                TokenProtector,
                StateProtector,
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
        public GoogleCalendarTokenResult? ExchangeResult { get; init; }

        public int CreateCount { get; private set; }
        public int GetCount { get; private set; }
        public IReadOnlyList<GoogleProviderCalendar> Calendars { get; init; } = [];
        private readonly ConcurrentDictionary<string, GoogleProviderEvent> _createdEvents = [];

        public string CreateAuthorizationUrl(
            string state, string capability = CalendarAuthorizationCapabilities.ReadOnly) =>
            throw new NotSupportedException();
        public Task<GoogleCalendarTokenResult> ExchangeCodeAsync(string code, CancellationToken cancellationToken) =>
            Task.FromResult(ExchangeResult ?? throw new NotSupportedException());
        public Task<IReadOnlyList<GoogleProviderCalendar>> ListCalendarsAsync(
            string accessToken, CancellationToken cancellationToken) => Task.FromResult(Calendars);

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

        public Task<GoogleProviderEvent> CreateEventAsync(
            string accessToken, string calendarId, GoogleProviderCreateEvent request,
            CancellationToken cancellationToken)
        {
            CreateCount++;
            var created = new GoogleProviderEvent(
                request.Id, request.Title, request.IsAllDay, request.Start, request.End,
                request.TimeZone, request.Location);
            return Task.FromResult(_createdEvents.GetOrAdd(request.Id, created));
        }

        public Task<GoogleProviderEvent> GetEventAsync(
            string accessToken, string calendarId, string eventId,
            CancellationToken cancellationToken)
        {
            GetCount++;
            return Task.FromResult(_createdEvents[eventId]);
        }

        public Task RevokeAsync(string token, CancellationToken cancellationToken) =>
            FailRevocation
                ? Task.FromException(new GoogleCalendarProviderException(
                    GoogleCalendarProviderFailure.Unavailable, "Unavailable"))
                : Task.CompletedTask;
    }
}

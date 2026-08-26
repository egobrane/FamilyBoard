using FamilyDashboard.Api.Configuration;
using FamilyDashboard.Api.Domain.Households;
using FamilyDashboard.Api.Domain.Identity;
using FamilyDashboard.Api.Domain.Integrations;
using FamilyDashboard.Api.Features.Tasks;
using FamilyDashboard.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FamilyDashboard.Api.Tests.Tasks;

[Collection("PostgreSQL integration")]
public sealed class GoogleTasksServiceTests
{
    [PostgreSqlFact]
    public async Task AuthorizationEncryptsTokensAndStoresNoTaskCopy()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        using var dependencies = new Dependencies();
        var account = new UserAccount { DisplayName = "Task owner", PrimaryEmail = "owner@example.test" };
        database.DbContext.UserAccounts.Add(account); await database.DbContext.SaveChangesAsync();
        var sessionId = Guid.NewGuid(); var householdId = Guid.NewGuid();
        var (state, _) = dependencies.State.CreateAuthorization(account.Id, sessionId, householdId,
            $"/households/{householdId:D}/tasks");
        var provider = new FakeProvider { ExchangeResult = new GoogleTasksTokenResult(
            "tasks-access", "tasks-refresh", DateTimeOffset.UtcNow.AddHours(1),
            [GoogleTasksScopes.OpenId, "https://www.googleapis.com/auth/userinfo.email", GoogleTasksScopes.TasksReadOnly],
            "task-subject", "Owner@Example.Test") };
        var result = await dependencies.Service(database, provider).CompleteAuthorizationAsync(
            "code", state, account.Id, sessionId, CancellationToken.None);
        Assert.Equal(householdId, result.HouseholdId);
        database.DbContext.ChangeTracker.Clear();
        var connection = await database.DbContext.GoogleTasksConnections.SingleAsync();
        Assert.Equal(GoogleTasksConnectionStatus.Active, connection.Status);
        Assert.DoesNotContain("tasks-access", connection.ProtectedAccessToken);
        Assert.Equal("tasks-refresh", dependencies.Tokens.Unprotect(connection.Id, "refresh-token", connection.ProtectedRefreshToken!));
        Assert.DoesNotContain(database.DbContext.Model.GetEntityTypes(), entity => entity.ClrType.Name is "GoogleTask" or "TaskItem");
    }

    [PostgreSqlFact]
    public async Task HouseholdReadRequiresActiveAdultOwnerAndReturnsProviderData()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        using var dependencies = new Dependencies();
        var account = new UserAccount { DisplayName = "Task owner", PrimaryEmail = "owner@example.test" };
        var household = new Household { Name = "Task family" };
        var member = new HouseholdMember { Household = household, DisplayName = "Owner", Role = HouseholdMemberRole.Adult };
        var membership = new HouseholdMembership { UserAccount = account, Household = household, HouseholdMember = member };
        var connection = new GoogleTasksConnection { UserAccount = account, UserAccountId = account.Id,
            ProviderSubject = "subject", ProviderEmailNormalized = "owner@example.test",
            GrantedScopes = GoogleTasksScopes.TasksReadOnly, ProtectedAccessToken = "pending",
            ProtectedRefreshToken = "pending", AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddHours(1) };
        connection.ProtectedAccessToken = dependencies.Tokens.Protect(connection.Id, "access-token", "access");
        connection.ProtectedRefreshToken = dependencies.Tokens.Protect(connection.Id, "refresh-token", "refresh");
        var source = new HouseholdTaskListSource { Household = household, GoogleTasksConnection = connection,
            OwnerUserAccountId = account.Id, AddedByUserAccount = account, ExternalTaskListId = "list-1",
            DisplayNameSnapshot = "Family tasks" };
        database.DbContext.AddRange(account, household, member, membership, connection, source);
        await database.DbContext.SaveChangesAsync(); database.DbContext.ChangeTracker.Clear();
        var provider = new FakeProvider { TaskPage = new GoogleProviderTaskPage([
            new GoogleProviderTask("task-1", "Pack lunch", null, "needsAction", "2026-08-27", null, null, "1", false)
        ], null) };
        var result = await dependencies.Service(database, provider).ListTasksAsync(
            household.Id, false, null, CancellationToken.None);
        Assert.Single(result.Tasks); Assert.Equal("Pack lunch", result.Tasks[0].Title);
        Assert.Equal("Family tasks", result.Tasks[0].TaskListName);
    }

    private sealed class Dependencies : IDisposable
    {
        private readonly ServiceProvider _services = new ServiceCollection().AddDataProtection().Services.BuildServiceProvider();
        private readonly MemoryCache _cache = new(new MemoryCacheOptions());
        private readonly IOptions<GoogleTasksConfiguration> _options = Options.Create(new GoogleTasksConfiguration { Enabled = true });
        public Dependencies()
        {
            Tokens = new TasksTokenProtector(_services.GetRequiredService<IDataProtectionProvider>());
            State = new TasksStateProtector(_services.GetRequiredService<IDataProtectionProvider>(), TimeProvider.System, _options);
        }
        public TasksTokenProtector Tokens { get; }
        public TasksStateProtector State { get; }
        public GoogleTasksService Service(PostgreSqlTestDatabase database, IGoogleTasksProviderClient provider) =>
            new(database.DbContext, provider, Tokens, State, _cache, _options, TimeProvider.System);
        public void Dispose() { _cache.Dispose(); _services.Dispose(); }
    }

    private sealed class FakeProvider : IGoogleTasksProviderClient
    {
        public GoogleTasksTokenResult? ExchangeResult { get; init; }
        public GoogleProviderTaskPage TaskPage { get; init; } = new([], null);
        public string CreateAuthorizationUrl(string state) => $"https://example.test?state={state}";
        public Task<GoogleTasksTokenResult> ExchangeCodeAsync(string code, CancellationToken cancellationToken) => Task.FromResult(ExchangeResult!);
        public Task<GoogleTasksRefreshResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken) => Task.FromResult(new GoogleTasksRefreshResult("access", DateTimeOffset.UtcNow.AddHours(1)));
        public Task<IReadOnlyList<GoogleProviderTaskList>> ListTaskListsAsync(string accessToken, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<GoogleProviderTaskList>>([]);
        public Task<GoogleProviderTaskPage> ListTasksAsync(string accessToken, string taskListId, bool includeCompleted, string? pageToken, int maximumResults, CancellationToken cancellationToken) => Task.FromResult(TaskPage);
        public Task RevokeAsync(string token, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

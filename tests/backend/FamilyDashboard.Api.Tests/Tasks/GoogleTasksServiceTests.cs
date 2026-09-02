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
            new GoogleProviderTask("task-1", "Pack lunch", null, "needsAction", "2026-08-27", null, null, "1", false, "etag-1")
        ], null) };
        var result = await dependencies.Service(database, provider).ListTasksAsync(
            household.Id, false, null, CancellationToken.None);
        Assert.Single(result.Tasks); Assert.Equal("Pack lunch", result.Tasks[0].Title);
        Assert.Equal("Family tasks", result.Tasks[0].TaskListName);
    }

    [PostgreSqlFact]
    public async Task CreateAndCompleteAreAttributedAndIdempotentWithoutStoringTaskContent()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        using var dependencies = new Dependencies();
        var now = DateTimeOffset.UtcNow;
        var account = new UserAccount { DisplayName = "Task owner", PrimaryEmail = "owner@example.test" };
        var household = new Household { Name = "Task family" };
        var member = new HouseholdMember { Household = household, DisplayName = "Owner", Role = HouseholdMemberRole.Adult };
        var membership = new HouseholdMembership { UserAccount = account, Household = household, HouseholdMember = member };
        var session = new UserSession { UserAccount = account, SelectedHouseholdId = household.Id,
            CreatedAt = now, LastSeenAt = now, ExpiresAt = now.AddHours(1), AbsoluteExpiresAt = now.AddDays(1) };
        var connection = new GoogleTasksConnection { UserAccount = account, UserAccountId = account.Id,
            ProviderSubject = "subject", ProviderEmailNormalized = "owner@example.test",
            GrantedScopes = GoogleTasksScopes.Tasks, ProtectedAccessToken = "pending",
            ProtectedRefreshToken = "pending", AccessTokenExpiresAt = now.AddHours(1) };
        connection.ProtectedAccessToken = dependencies.Tokens.Protect(connection.Id, "access-token", "access");
        connection.ProtectedRefreshToken = dependencies.Tokens.Protect(connection.Id, "refresh-token", "refresh");
        var source = new HouseholdTaskListSource { Household = household, GoogleTasksConnection = connection,
            OwnerUserAccountId = account.Id, AddedByUserAccount = account, ExternalTaskListId = "list-1",
            DisplayNameSnapshot = "Family tasks", IsWriteTarget = true,
            WriteTargetConfiguredAt = now, WriteTargetConfiguredByUserAccountId = account.Id };
        database.DbContext.AddRange(account, household, member, membership, session, connection, source);
        await database.DbContext.SaveChangesAsync();
        var provider = new FakeProvider { TaskPage = new GoogleProviderTaskPage([
            new GoogleProviderTask("created", "Pack lunch", "Use blue bag", "needsAction",
                "2026-08-29", null, null, "1", false, "etag-created")], null) };
        var service = dependencies.Service(database, provider);
        var id = Guid.NewGuid();
        var created = await service.CreateTaskAsync(household.Id, account.Id, session.Id,
            new CreateGoogleTaskRequest(id, null, "Pack lunch", "Use blue bag", "2026-08-29"),
            "trace", CancellationToken.None);
        var replay = await service.CreateTaskAsync(household.Id, account.Id, session.Id,
            new CreateGoogleTaskRequest(id, null, "Pack lunch", "Use blue bag", "2026-08-29"),
            "trace", CancellationToken.None);
        Assert.True(replay.RecoveredExistingMutation);
        var completed = await service.UpdateTaskStatusAsync(household.Id, account.Id, session.Id,
            new UpdateGoogleTaskStatusRequest(source.Id, created.TaskId, Guid.NewGuid(),
                "completed", created.MutationVersion), "trace", CancellationToken.None);
        var receipts = await database.DbContext.GoogleTaskMutationReceipts.OrderBy(item => item.CreatedAt).ToArrayAsync();
        Assert.Equal(2, receipts.Length);
        Assert.All(receipts, receipt => Assert.Equal(member.Id, receipt.AttributedHouseholdMemberId));
        Assert.All(receipts, receipt => Assert.Equal(GoogleTaskMutationReceiptStatus.Succeeded, receipt.Status));
        Assert.Equal("completed", completed.Status);
        Assert.DoesNotContain(database.DbContext.Model.GetEntityTypes(), entity =>
            entity.GetProperties().Any(property => property.Name is "Title" or "Notes")
                && entity.ClrType == typeof(GoogleTaskMutationReceipt));
        Assert.Equal(created.TaskId, replay.TaskId);
    }

    [PostgreSqlFact]
    public async Task SharedDisplayStatusChangeRecordsSharedSessionWithoutMemberAttribution()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        using var dependencies = new Dependencies();
        var now = DateTimeOffset.UtcNow;
        var account = new UserAccount { DisplayName = "Wall display owner", PrimaryEmail = "owner@example.test" };
        var household = new Household { Name = "Task family" };
        var member = new HouseholdMember { Household = household, DisplayName = "Owner", Role = HouseholdMemberRole.Adult };
        var membership = new HouseholdMembership { UserAccount = account, Household = household, HouseholdMember = member };
        var session = new UserSession { UserAccount = account, SelectedHouseholdId = household.Id,
            IsSharedDisplay = true, CreatedAt = now, LastSeenAt = now, ExpiresAt = now.AddHours(1),
            AbsoluteExpiresAt = now.AddDays(1) };
        var connection = new GoogleTasksConnection { UserAccount = account, UserAccountId = account.Id,
            ProviderSubject = "subject", ProviderEmailNormalized = "owner@example.test",
            GrantedScopes = GoogleTasksScopes.Tasks, ProtectedAccessToken = "pending",
            ProtectedRefreshToken = "pending", AccessTokenExpiresAt = now.AddHours(1) };
        connection.ProtectedAccessToken = dependencies.Tokens.Protect(connection.Id, "access-token", "access");
        connection.ProtectedRefreshToken = dependencies.Tokens.Protect(connection.Id, "refresh-token", "refresh");
        var source = new HouseholdTaskListSource { Household = household, GoogleTasksConnection = connection,
            OwnerUserAccountId = account.Id, AddedByUserAccount = account, ExternalTaskListId = "list-1",
            DisplayNameSnapshot = "Family tasks", IsWriteTarget = true,
            WriteTargetConfiguredAt = now, WriteTargetConfiguredByUserAccountId = account.Id };
        database.DbContext.AddRange(account, household, member, membership, session, connection, source);
        await database.DbContext.SaveChangesAsync();
        var provider = new FakeProvider { TaskPage = new GoogleProviderTaskPage([
            new GoogleProviderTask("task-1", "Pack lunch", null, "needsAction", null, null, null,
                "1", false, "etag-1")], null) };
        var mutationVersion = new TasksMutationProtector(
            dependencies.DataProtectionProvider, TimeProvider.System)
            .Protect(household.Id, source.Id, "task-1", "etag-1");

        var result = await dependencies.Service(database, provider).UpdateTaskStatusAsync(
            household.Id, account.Id, session.Id,
            new UpdateGoogleTaskStatusRequest(source.Id, "task-1", Guid.NewGuid(), "completed", mutationVersion),
            "trace", CancellationToken.None);

        var receipt = await database.DbContext.GoogleTaskMutationReceipts.SingleAsync();
        Assert.True(receipt.RequestedFromSharedDisplay);
        Assert.Equal(account.Id, receipt.RequestedByUserAccountId);
        Assert.Null(receipt.AttributedHouseholdMemberId);
        Assert.Null(result.AttributedMemberId);
    }

    private sealed class Dependencies : IDisposable
    {
        private readonly ServiceProvider _services = new ServiceCollection().AddDataProtection().Services.BuildServiceProvider();
        private readonly MemoryCache _cache = new(new MemoryCacheOptions());
        private readonly IOptions<GoogleTasksConfiguration> _options = Options.Create(new GoogleTasksConfiguration { Enabled = true, MutationsEnabled = true });
        public Dependencies()
        {
            Tokens = new TasksTokenProtector(_services.GetRequiredService<IDataProtectionProvider>());
            State = new TasksStateProtector(_services.GetRequiredService<IDataProtectionProvider>(), TimeProvider.System, _options);
        }
        public TasksTokenProtector Tokens { get; }
        public TasksStateProtector State { get; }
        public IDataProtectionProvider DataProtectionProvider => _services.GetRequiredService<IDataProtectionProvider>();
        public GoogleTasksService Service(PostgreSqlTestDatabase database, IGoogleTasksProviderClient provider) =>
            new(database.DbContext, provider, Tokens, State,
                new TasksMutationProtector(_services.GetRequiredService<IDataProtectionProvider>(), TimeProvider.System),
                _cache, _options, TimeProvider.System);
        public void Dispose() { _cache.Dispose(); _services.Dispose(); }
    }

    private sealed class FakeProvider : IGoogleTasksProviderClient
    {
        public GoogleTasksTokenResult? ExchangeResult { get; init; }
        public GoogleProviderTaskPage TaskPage { get; init; } = new([], null);
        public string CreateAuthorizationUrl(string state, bool requestWriteAccess = false) => $"https://example.test?state={state}";
        public Task<GoogleTasksTokenResult> ExchangeCodeAsync(string code, CancellationToken cancellationToken) => Task.FromResult(ExchangeResult!);
        public Task<GoogleTasksRefreshResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken) => Task.FromResult(new GoogleTasksRefreshResult("access", DateTimeOffset.UtcNow.AddHours(1)));
        public Task<IReadOnlyList<GoogleProviderTaskList>> ListTaskListsAsync(string accessToken, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<GoogleProviderTaskList>>([]);
        public Task<GoogleProviderTaskPage> ListTasksAsync(string accessToken, string taskListId, bool includeCompleted, string? pageToken, int maximumResults, CancellationToken cancellationToken) => Task.FromResult(TaskPage);
        public Task<GoogleProviderTask> GetTaskAsync(string accessToken, string taskListId, string taskId, CancellationToken cancellationToken) => Task.FromResult(TaskPage.Tasks.Single(item => item.Id == taskId));
        public Task<GoogleProviderTask> CreateTaskAsync(string accessToken, string taskListId, string title, string? notes, string? dueDate, CancellationToken cancellationToken) => Task.FromResult(new GoogleProviderTask("created", title, notes, "needsAction", dueDate, null, null, "1", false, "etag-created"));
        public Task<GoogleProviderTask> UpdateTaskStatusAsync(string accessToken, string taskListId, string taskId, string expectedETag, string targetStatus, CancellationToken cancellationToken) => Task.FromResult(new GoogleProviderTask(taskId, "Task", null, targetStatus, null, targetStatus == "completed" ? DateTimeOffset.UtcNow : null, null, "1", false, "etag-updated"));
        public Task RevokeAsync(string token, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

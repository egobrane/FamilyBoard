using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FamilyDashboard.Api.Domain.Identity;
using FamilyDashboard.Api.Features.Households;
using FamilyDashboard.Api.Features.Tasks;
using FamilyDashboard.Api.Tests.Authentication;
using FamilyDashboard.Api.Tests.Infrastructure;

namespace FamilyDashboard.Api.Tests.Endpoints;

[Collection("PostgreSQL integration")]
public sealed class TasksEndpointTests
{
    [PostgreSqlFact]
    public async Task DisabledStatusAndRoutineEmptyReadFailSafely()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var account = await AddAccountAsync(database, "Owner", "tasks-owner@example.test");
        using var client = Client(database.Factory, account.Id);
        var household = await BootstrapAsync(client, "Tasks household");
        var status = await client.GetFromJsonAsync<TasksConnectionResponse>($"/api/households/{household.Id}/tasks/connection");
        Assert.NotNull(status); Assert.False(status.IsAvailable); Assert.Equal("disconnected", status.Status);
        using var read = await client.GetAsync($"/api/households/{household.Id}/tasks");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, read.StatusCode);
        Assert.Equal("google_tasks_unavailable", await ProblemCodeAsync(read));
    }

    [PostgreSqlFact]
    public async Task AuthorizationUsesDedicatedReadOnlyScopeAndSecureCorrelationCookie()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var account = await AddAccountAsync(database, "Owner", "tasks-scope@example.test");
        await using var factory = new IdentityHouseholdWebApplicationFactory(
            Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION_STRING")!, enableTasks: true);
        using var client = Client(factory, account.Id);
        var household = await BootstrapAsync(client, "Scoped Tasks household");
        using var response = await client.PostAsJsonAsync($"/api/households/{household.Id}/tasks/authorization",
            new BeginTasksAuthorizationRequest($"/households/{household.Id}/tasks"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BeginTasksAuthorizationResponse>();
        Assert.NotNull(body);
        Assert.Contains(GoogleTasksScopes.TasksReadOnly, Uri.UnescapeDataString(body.AuthorizationUrl));
        Assert.DoesNotContain("auth/calendar", body.AuthorizationUrl);
        var cookie = response.Headers.GetValues("Set-Cookie").Single(value =>
            value.StartsWith(TasksCorrelationCookieService.CookieName, StringComparison.Ordinal));
        Assert.Contains("path=/api/integrations/google-tasks/callback", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [PostgreSqlFact]
    public async Task WriteAuthorizationUsesIncrementalTasksScope()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var account = await AddAccountAsync(database, "Owner", "tasks-write@example.test");
        await using var factory = new IdentityHouseholdWebApplicationFactory(
            Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION_STRING")!,
            enableTasks: true, enableTaskMutations: true);
        using var client = Client(factory, account.Id);
        var household = await BootstrapAsync(client, "Writable Tasks household");
        using var response = await client.PostAsJsonAsync($"/api/households/{household.Id}/tasks/authorization",
            new BeginTasksAuthorizationRequest($"/households/{household.Id}/tasks", "write"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BeginTasksAuthorizationResponse>();
        Assert.NotNull(body);
        var decoded = Uri.UnescapeDataString(body.AuthorizationUrl);
        Assert.Contains(GoogleTasksScopes.Tasks, decoded);
        Assert.DoesNotContain(GoogleTasksScopes.TasksReadOnly, decoded);
    }

    private static async Task<UserAccount> AddAccountAsync(PostgreSqlTestDatabase database, string name, string email)
    {
        var account = new UserAccount { DisplayName = name, PrimaryEmail = email };
        database.DbContext.UserAccounts.Add(account); await database.DbContext.SaveChangesAsync(); return account;
    }
    private static HttpClient Client(IdentityHouseholdWebApplicationFactory factory, Guid accountId)
    {
        var client = factory.CreateClient(); client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserIdHeaderName, accountId.ToString()); return client;
    }
    private static async Task<HouseholdResponse> BootstrapAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/households", new CreateHouseholdRequest(name, "America/New_York", "en-US", "Sunday"));
        response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<HouseholdResponse>())!;
    }
    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); return json.RootElement.GetProperty("code").GetString();
    }
}

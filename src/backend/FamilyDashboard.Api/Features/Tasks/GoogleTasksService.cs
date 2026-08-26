using FamilyDashboard.Api.Configuration;
using FamilyDashboard.Api.Domain.Households;
using FamilyDashboard.Api.Domain.Integrations;
using FamilyDashboard.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace FamilyDashboard.Api.Features.Tasks;

public sealed class TasksOperationException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}

public sealed record TasksCallbackResult(Guid HouseholdId, string ReturnPath);

public sealed class GoogleTasksService(
    FamilyDashboardDbContext dbContext,
    IGoogleTasksProviderClient provider,
    TasksTokenProtector tokenProtector,
    TasksStateProtector stateProtector,
    IMemoryCache cache,
    IOptions<GoogleTasksConfiguration> options,
    TimeProvider timeProvider)
{
    private readonly GoogleTasksConfiguration _configuration = options.Value;

    public async Task<TasksConnectionResponse> GetConnectionAsync(
        Guid householdId, Guid userAccountId, CancellationToken cancellationToken)
    {
        var connection = await dbContext.GoogleTasksConnections.AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserAccountId == userAccountId, cancellationToken);
        if (connection is null)
            return new TasksConnectionResponse(_configuration.Enabled, null, "disconnected", null, null, 0, 0);
        var activeSourceCount = await dbContext.HouseholdTaskListSources.CountAsync(
            item => item.HouseholdId == householdId
                && item.GoogleTasksConnectionId == connection.Id && item.IsActive,
            cancellationToken);
        var activeHouseholdCount = await dbContext.HouseholdTaskListSources
            .Where(item => item.GoogleTasksConnectionId == connection.Id && item.IsActive)
            .Select(item => item.HouseholdId).Distinct().CountAsync(cancellationToken);
        return new TasksConnectionResponse(
            _configuration.Enabled, connection.Id, ToContract(connection.Status),
            connection.ProviderEmailNormalized, connection.ConnectedAt,
            activeSourceCount, activeHouseholdCount);
    }

    public (BeginTasksAuthorizationResponse Response, string State) BeginAuthorization(
        Guid householdId, Guid userAccountId, Guid userSessionId, string returnPath)
    {
        RequireAvailable();
        var (state, expiresAt) = stateProtector.CreateAuthorization(
            userAccountId, userSessionId, householdId, returnPath);
        return (new BeginTasksAuthorizationResponse(provider.CreateAuthorizationUrl(state), expiresAt), state);
    }

    public async Task<TasksCallbackResult> CompleteAuthorizationAsync(
        string code, string state, Guid userAccountId, Guid userSessionId,
        CancellationToken cancellationToken)
    {
        RequireAvailable();
        if (!stateProtector.TryReadAuthorization(state, out var payload)
            || payload!.UserAccountId != userAccountId || payload.UserSessionId != userSessionId)
            throw Error(400, Common.ApiProblemCodes.TasksAuthorizationExpired,
                "The Google Tasks authorization request expired or is invalid.");

        var token = await provider.ExchangeCodeAsync(code, cancellationToken);
        if (!token.Scopes.Contains(GoogleTasksScopes.TasksReadOnly, StringComparer.Ordinal))
            throw Error(409, Common.ApiProblemCodes.TasksScopeMissing,
                "Required Google Tasks permission was not granted.");

        var now = timeProvider.GetUtcNow();
        var connection = await dbContext.GoogleTasksConnections
            .SingleOrDefaultAsync(item => item.UserAccountId == userAccountId, cancellationToken);
        if (connection is null)
        {
            if (string.IsNullOrWhiteSpace(token.RefreshToken))
                throw Error(409, Common.ApiProblemCodes.TasksOfflineAccessRequired,
                    "Google did not grant offline Tasks access. Reconnect and approve access.");
            connection = new GoogleTasksConnection
            {
                UserAccountId = userAccountId,
                ProviderSubject = token.ProviderSubject,
                ProviderEmailNormalized = token.ProviderEmail.Trim().ToLowerInvariant(),
                GrantedScopes = string.Join(' ', token.Scopes.Order(StringComparer.Ordinal)),
                ConnectedAt = now,
                UpdatedAt = now,
            };
            dbContext.GoogleTasksConnections.Add(connection);
        }
        else
        {
            if (connection.ProviderSubject != token.ProviderSubject)
                throw Error(409, Common.ApiProblemCodes.TasksAccountMismatch,
                    "Disconnect the current Google Tasks account before connecting a different account.");
            connection.ProviderEmailNormalized = token.ProviderEmail.Trim().ToLowerInvariant();
            connection.GrantedScopes = string.Join(' ', token.Scopes.Order(StringComparer.Ordinal));
            connection.UpdatedAt = now;
            connection.RevokedAt = null;
        }
        if (string.IsNullOrWhiteSpace(token.RefreshToken) && connection.ProtectedRefreshToken is null)
            throw Error(409, Common.ApiProblemCodes.TasksOfflineAccessRequired,
                "Google did not grant offline Tasks access. Reconnect and approve access.");
        connection.ProtectedAccessToken = tokenProtector.Protect(connection.Id, "access-token", token.AccessToken);
        if (!string.IsNullOrWhiteSpace(token.RefreshToken))
            connection.ProtectedRefreshToken = tokenProtector.Protect(connection.Id, "refresh-token", token.RefreshToken);
        connection.AccessTokenExpiresAt = token.ExpiresAt;
        connection.Status = GoogleTasksConnectionStatus.Active;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new TasksCallbackResult(payload.HouseholdId, payload.ReturnPath);
    }

    public async Task<IReadOnlyList<ProviderTaskListResponse>> ListProviderTaskListsAsync(
        Guid householdId, Guid userAccountId, CancellationToken cancellationToken)
    {
        RequireAvailable();
        var connection = await FindActiveConnectionAsync(userAccountId, cancellationToken);
        var lists = await CallProviderAsync(connection,
            token => provider.ListTaskListsAsync(token, cancellationToken), cancellationToken);
        var selected = await dbContext.HouseholdTaskListSources.AsNoTracking()
            .Where(item => item.HouseholdId == householdId
                && item.GoogleTasksConnectionId == connection.Id && item.IsActive)
            .Select(item => item.ExternalTaskListId).ToHashSetAsync(cancellationToken);
        return lists.Select(item => new ProviderTaskListResponse(item.Id, item.Name, selected.Contains(item.Id))).ToArray();
    }

    public async Task<IReadOnlyList<TaskListSourceResponse>> ListSourcesAsync(
        Guid householdId, Guid userAccountId, CancellationToken cancellationToken) =>
        await dbContext.HouseholdTaskListSources.AsNoTracking()
            .Where(item => item.HouseholdId == householdId)
            .OrderByDescending(item => item.IsActive).ThenBy(item => item.DisplayNameSnapshot)
            .Select(item => new TaskListSourceResponse(item.Id, item.GoogleTasksConnectionId,
                item.ExternalTaskListId, item.DisplayNameSnapshot, item.IsActive,
                item.OwnerUserAccountId == userAccountId))
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyList<TaskListSourceResponse>> UpdateSourcesAsync(
        Guid householdId, Guid userAccountId, UpdateTaskListSourcesRequest request,
        CancellationToken cancellationToken)
    {
        RequireAvailable();
        var ids = (request.ExternalTaskListIds ?? []).Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim()).Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Length > _configuration.MaximumTaskListsPerHousehold)
            throw Error(400, Common.ApiProblemCodes.ValidationFailed,
                $"Choose no more than {_configuration.MaximumTaskListsPerHousehold} task lists.");
        var connection = await FindActiveConnectionAsync(userAccountId, cancellationToken);
        if (connection.Id != request.ConnectionId)
            throw Error(404, Common.ApiProblemCodes.TasksSourceNotFound, "The Google Tasks connection was not found.");
        var providerLists = await CallProviderAsync(connection,
            token => provider.ListTaskListsAsync(token, cancellationToken), cancellationToken);
        var available = providerLists.ToDictionary(item => item.Id, StringComparer.Ordinal);
        if (ids.Any(id => !available.ContainsKey(id)))
            throw Error(400, Common.ApiProblemCodes.TasksSourceConflict,
                "One or more selected task lists are no longer available.");
        var sources = await dbContext.HouseholdTaskListSources
            .Where(item => item.HouseholdId == householdId
                && item.GoogleTasksConnectionId == connection.Id).ToListAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        foreach (var source in sources)
        {
            source.IsActive = ids.Contains(source.ExternalTaskListId, StringComparer.Ordinal);
            source.UpdatedAt = now;
            if (available.TryGetValue(source.ExternalTaskListId, out var providerList))
                source.DisplayNameSnapshot = providerList.Name;
            Invalidate(source.Id);
        }
        foreach (var id in ids.Where(id => sources.All(source => source.ExternalTaskListId != id)))
            dbContext.HouseholdTaskListSources.Add(new HouseholdTaskListSource
            {
                HouseholdId = householdId,
                GoogleTasksConnectionId = connection.Id,
                OwnerUserAccountId = userAccountId,
                ExternalTaskListId = id,
                DisplayNameSnapshot = available[id].Name,
                AddedByUserAccountId = userAccountId,
                CreatedAt = now,
                UpdatedAt = now,
            });
        await dbContext.SaveChangesAsync(cancellationToken);
        return await ListSourcesAsync(householdId, userAccountId, cancellationToken);
    }

    public async Task DisconnectAsync(Guid userAccountId, DisconnectTasksRequest request,
        CancellationToken cancellationToken)
    {
        RequireAvailable();
        if (!request.ConfirmGlobalDisconnect)
            throw Error(400, Common.ApiProblemCodes.TasksDisconnectConfirmationRequired,
                "Confirm that Google Tasks should be disconnected from every household.");
        var connection = await dbContext.GoogleTasksConnections.Include(item => item.HouseholdSources)
            .SingleOrDefaultAsync(item => item.UserAccountId == userAccountId && item.Id == request.ConnectionId,
                cancellationToken)
            ?? throw Error(404, Common.ApiProblemCodes.TasksConnectionRequired, "The Google Tasks connection was not found.");
        var token = connection.ProtectedRefreshToken is null ? connection.ProtectedAccessToken
            : tokenProtector.Unprotect(connection.Id, "refresh-token", connection.ProtectedRefreshToken);
        if (connection.ProtectedRefreshToken is null && token is not null)
            token = tokenProtector.Unprotect(connection.Id, "access-token", token);
        if (!string.IsNullOrWhiteSpace(token))
        {
            try { await provider.RevokeAsync(token, cancellationToken); }
            catch (GoogleTasksProviderException) { }
        }
        var now = timeProvider.GetUtcNow();
        connection.ProtectedAccessToken = null;
        connection.ProtectedRefreshToken = null;
        connection.AccessTokenExpiresAt = null;
        connection.Status = GoogleTasksConnectionStatus.Disconnected;
        connection.RevokedAt = now;
        connection.UpdatedAt = now;
        foreach (var source in connection.HouseholdSources)
        {
            source.IsActive = false;
            source.UpdatedAt = now;
            Invalidate(source.Id);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<GoogleTasksResponse> ListTasksAsync(
        Guid householdId, bool includeCompleted, string? cursor, CancellationToken cancellationToken)
    {
        RequireAvailable();
        TasksPageCursor? page = null;
        if (cursor is not null && (!stateProtector.TryReadCursor(cursor, out page)
            || page!.HouseholdId != householdId || page.IncludeCompleted != includeCompleted))
            throw Error(400, Common.ApiProblemCodes.TasksCursorInvalid,
                "The Google Tasks cursor is invalid or expired.");
        var sources = await dbContext.HouseholdTaskListSources.AsNoTracking()
            .Include(item => item.GoogleTasksConnection)
            .Where(item => item.HouseholdId == householdId && item.IsActive
                && item.GoogleTasksConnection.Status == GoogleTasksConnectionStatus.Active
                && item.GoogleTasksConnection.UserAccount.IsActive
                && item.GoogleTasksConnection.UserAccount.HouseholdMemberships.Any(membership =>
                    membership.HouseholdId == householdId && membership.HouseholdMember.IsActive
                    && membership.HouseholdMember.Role == HouseholdMemberRole.Adult))
            .OrderBy(item => item.Id).ToListAsync(cancellationToken);
        if (page is not null)
        {
            if (page.RemainingSources.Keys.Any(id => sources.All(source => source.Id != id)))
                throw Error(400, Common.ApiProblemCodes.TasksCursorInvalid,
                    "The Google Tasks cursor is invalid or expired.");
            sources = sources.Where(item => page.RemainingSources.ContainsKey(item.Id)).ToList();
        }
        var tasks = new List<GoogleTaskResponse>();
        var warnings = new List<GoogleTasksWarningResponse>();
        var remaining = new Dictionary<Guid, string>();
        var stale = false;
        var perSource = Math.Clamp(_configuration.MaximumTasksPerRequest / Math.Max(1, sources.Count), 1, 100);
        foreach (var connectionGroup in sources.GroupBy(item => item.GoogleTasksConnectionId))
        {
            string accessToken;
            try { accessToken = await GetAccessTokenAsync(connectionGroup.First().GoogleTasksConnection, cancellationToken); }
            catch (TasksOperationException exception)
            {
                warnings.AddRange(connectionGroup.Select(item => new GoogleTasksWarningResponse(
                    item.Id, exception.Code, "Reconnect this Google Tasks account.")));
                continue;
            }
            foreach (var source in connectionGroup)
            {
                var result = await GetTaskPageAsync(source, accessToken, includeCompleted,
                    page?.RemainingSources[source.Id], perSource, cancellationToken);
                stale |= result.IsStale;
                if (result.Warning is not null) warnings.Add(result.Warning);
                tasks.AddRange(result.Page.Tasks.Select(item => new GoogleTaskResponse(
                    item.Id, source.Id, source.DisplayNameSnapshot, item.Title, item.Notes,
                    item.Status, item.DueDate, item.CompletedAt, item.ParentTaskId,
                    item.Position, item.ParentTaskId is not null, item.IsAssigned)));
                if (result.Page.NextPageToken is not null) remaining[source.Id] = result.Page.NextPageToken;
            }
        }
        var next = remaining.Count == 0 ? null : stateProtector.CreateCursor(new TasksPageCursor(
            householdId, includeCompleted, remaining,
            timeProvider.GetUtcNow() + _configuration.StaleCacheLifetime));
        return new GoogleTasksResponse(tasks.OrderBy(item => item.TaskListName)
            .ThenBy(item => item.Position, StringComparer.Ordinal).ToArray(), next, stale, warnings);
    }

    private async Task<(GoogleProviderTaskPage Page, bool IsStale, GoogleTasksWarningResponse? Warning)> GetTaskPageAsync(
        HouseholdTaskListSource source, string accessToken, bool includeCompleted,
        string? pageToken, int maximumResults, CancellationToken cancellationToken)
    {
        var versionKey = $"tasks-version:{source.Id:D}";
        if (!cache.TryGetValue<string>(versionKey, out var version))
        {
            version = "initial";
            cache.Set(versionKey, version, _configuration.StaleCacheLifetime);
        }
        var key = $"tasks:{source.Id:D}:{version}:{includeCompleted}:{pageToken}:{maximumResults}";
        cache.TryGetValue<TaskCache>(key, out var cached);
        if (cached is not null && cached.FetchedAt + _configuration.FreshCacheLifetime > timeProvider.GetUtcNow())
            return (cached.Page, false, null);
        try
        {
            var providerPage = await provider.ListTasksAsync(accessToken, source.ExternalTaskListId,
                includeCompleted, pageToken, maximumResults, cancellationToken);
            cache.Set(key, new TaskCache(providerPage, timeProvider.GetUtcNow()), _configuration.StaleCacheLifetime);
            return (providerPage, false, null);
        }
        catch (GoogleTasksProviderException exception)
        {
            var code = exception.Failure switch
            {
                GoogleTasksProviderFailure.ReauthorizationRequired => Common.ApiProblemCodes.TasksReauthorizationRequired,
                GoogleTasksProviderFailure.RateLimited => Common.ApiProblemCodes.TasksProviderRateLimited,
                _ => Common.ApiProblemCodes.TasksProviderUnavailable,
            };
            if (exception.Failure == GoogleTasksProviderFailure.ReauthorizationRequired)
                await MarkReauthorizationRequiredAsync(source.GoogleTasksConnection, cancellationToken);
            if (cached is not null)
                return (cached.Page, true, new GoogleTasksWarningResponse(source.Id, code,
                    "Showing recently cached Google Tasks information."));
            return (new GoogleProviderTaskPage([], null), false,
                new GoogleTasksWarningResponse(source.Id, code,
                    exception.Failure == GoogleTasksProviderFailure.ReauthorizationRequired
                        ? "Reconnect this Google Tasks account."
                        : "This task list is temporarily unavailable."));
        }
    }

    private async Task<T> CallProviderAsync<T>(GoogleTasksConnection connection,
        Func<string, Task<T>> action, CancellationToken cancellationToken)
    {
        try { return await action(await GetAccessTokenAsync(connection, cancellationToken)); }
        catch (GoogleTasksProviderException exception)
            when (exception.Failure == GoogleTasksProviderFailure.ReauthorizationRequired)
        {
            await MarkReauthorizationRequiredAsync(connection, cancellationToken);
            throw Error(409, Common.ApiProblemCodes.TasksReauthorizationRequired,
                "Reconnect Google Tasks.");
        }
    }

    private async Task<GoogleTasksConnection> FindActiveConnectionAsync(
        Guid userAccountId, CancellationToken cancellationToken)
    {
        var connection = await dbContext.GoogleTasksConnections.SingleOrDefaultAsync(
            item => item.UserAccountId == userAccountId, cancellationToken);
        if (connection is null)
            throw Error(409, Common.ApiProblemCodes.TasksConnectionRequired, "Connect Google Tasks first.");
        if (connection.Status != GoogleTasksConnectionStatus.Active)
            throw Error(409, Common.ApiProblemCodes.TasksReauthorizationRequired, "Reconnect Google Tasks.");
        return connection;
    }

    private async Task<string> GetAccessTokenAsync(
        GoogleTasksConnection connection, CancellationToken cancellationToken)
    {
        if (connection.ProtectedAccessToken is not null
            && connection.AccessTokenExpiresAt > timeProvider.GetUtcNow() + TimeSpan.FromMinutes(1))
            return tokenProtector.Unprotect(connection.Id, "access-token", connection.ProtectedAccessToken);
        if (connection.ProtectedRefreshToken is null)
            throw Error(409, Common.ApiProblemCodes.TasksReauthorizationRequired, "Reconnect Google Tasks.");
        try
        {
            var refresh = await provider.RefreshAsync(
                tokenProtector.Unprotect(connection.Id, "refresh-token", connection.ProtectedRefreshToken),
                cancellationToken);
            connection.ProtectedAccessToken = tokenProtector.Protect(connection.Id, "access-token", refresh.AccessToken);
            connection.AccessTokenExpiresAt = refresh.ExpiresAt;
            connection.LastSuccessfulRefreshAt = timeProvider.GetUtcNow();
            connection.UpdatedAt = timeProvider.GetUtcNow();
            await dbContext.SaveChangesAsync(cancellationToken);
            return refresh.AccessToken;
        }
        catch (GoogleTasksProviderException exception)
            when (exception.Failure == GoogleTasksProviderFailure.ReauthorizationRequired)
        {
            await MarkReauthorizationRequiredAsync(connection, cancellationToken);
            throw Error(409, Common.ApiProblemCodes.TasksReauthorizationRequired, "Reconnect Google Tasks.");
        }
    }

    private async Task MarkReauthorizationRequiredAsync(
        GoogleTasksConnection connection, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        connection.Status = GoogleTasksConnectionStatus.ReauthorizationRequired;
        connection.UpdatedAt = now;
        await dbContext.GoogleTasksConnections
            .Where(item => item.Id == connection.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, GoogleTasksConnectionStatus.ReauthorizationRequired)
                .SetProperty(item => item.UpdatedAt, now), cancellationToken);
    }

    private void Invalidate(Guid sourceId) => cache.Set(
        $"tasks-version:{sourceId:D}", Guid.NewGuid().ToString("N"), _configuration.StaleCacheLifetime);

    private void RequireAvailable()
    {
        if (!_configuration.Enabled)
            throw Error(503, Common.ApiProblemCodes.GoogleTasksUnavailable, "Google Tasks is not available.");
    }

    private static TasksOperationException Error(int status, string code, string message) =>
        new(status, code, message);
    private static string ToContract(GoogleTasksConnectionStatus status) => status switch
    {
        GoogleTasksConnectionStatus.Active => "active",
        GoogleTasksConnectionStatus.ReauthorizationRequired => "reauthorizationRequired",
        _ => "disconnected",
    };
    private sealed record TaskCache(GoogleProviderTaskPage Page, DateTimeOffset FetchedAt);
}

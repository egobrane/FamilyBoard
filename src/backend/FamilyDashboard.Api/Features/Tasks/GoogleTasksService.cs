using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using FamilyDashboard.Api.Configuration;
using FamilyDashboard.Api.Domain.Households;
using FamilyDashboard.Api.Domain.Integrations;
using FamilyDashboard.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Npgsql;

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
    TasksMutationProtector mutationProtector,
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
            return new TasksConnectionResponse(_configuration.Enabled, null, "disconnected", null, null, 0, 0,
                false, false, _configuration.MutationsEnabled, _configuration.MutationsEnabled);
        var activeSourceCount = await dbContext.HouseholdTaskListSources.CountAsync(
            item => item.HouseholdId == householdId
                && item.GoogleTasksConnectionId == connection.Id && item.IsActive,
            cancellationToken);
        var activeHouseholdCount = await dbContext.HouseholdTaskListSources
            .Where(item => item.GoogleTasksConnectionId == connection.Id && item.IsActive)
            .Select(item => item.HouseholdId).Distinct().CountAsync(cancellationToken);
        var isActive = connection.Status == GoogleTasksConnectionStatus.Active;
        var canRead = isActive && HasReadScope(connection);
        var canWrite = isActive && HasWriteScope(connection);
        return new TasksConnectionResponse(
            _configuration.Enabled, connection.Id, ToContract(connection.Status),
            connection.ProviderEmailNormalized, connection.ConnectedAt,
            activeSourceCount, activeHouseholdCount, canRead, canWrite,
            _configuration.MutationsEnabled && !canWrite, _configuration.MutationsEnabled);
    }

    public (BeginTasksAuthorizationResponse Response, string State) BeginAuthorization(
        Guid householdId, Guid userAccountId, Guid userSessionId, string returnPath,
        string capability = "read")
    {
        RequireAvailable();
        var write = string.Equals(capability, "write", StringComparison.OrdinalIgnoreCase);
        if (write) RequireMutationsAvailable();
        var (state, expiresAt) = stateProtector.CreateAuthorization(
            userAccountId, userSessionId, householdId, returnPath, write ? "write" : "read");
        return (new BeginTasksAuthorizationResponse(provider.CreateAuthorizationUrl(state, write), expiresAt), state);
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
        var requiredScope = payload.Capability == "write" ? GoogleTasksScopes.Tasks : GoogleTasksScopes.TasksReadOnly;
        if (!token.Scopes.Contains(requiredScope, StringComparer.Ordinal)
            && !(requiredScope == GoogleTasksScopes.TasksReadOnly
                && token.Scopes.Contains(GoogleTasksScopes.Tasks, StringComparer.Ordinal)))
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
        var writeTarget = await dbContext.HouseholdTaskListSources.AsNoTracking()
            .Where(item => item.HouseholdId == householdId && item.IsWriteTarget)
            .Select(item => item.ExternalTaskListId).SingleOrDefaultAsync(cancellationToken);
        var canWrite = HasWriteScope(connection);
        return lists.Select(item => new ProviderTaskListResponse(item.Id, item.Name,
            selected.Contains(item.Id), canWrite, item.Id == writeTarget)).ToArray();
    }

    public async Task<IReadOnlyList<TaskListSourceResponse>> ListSourcesAsync(
        Guid householdId, Guid userAccountId, CancellationToken cancellationToken)
    {
        var sources = await dbContext.HouseholdTaskListSources.AsNoTracking()
            .Include(item => item.GoogleTasksConnection)
            .Where(item => item.HouseholdId == householdId)
            .OrderByDescending(item => item.IsActive).ThenBy(item => item.DisplayNameSnapshot)
            .ToArrayAsync(cancellationToken);
        return sources.Select(item => new TaskListSourceResponse(item.Id, item.GoogleTasksConnectionId,
                item.ExternalTaskListId, item.DisplayNameSnapshot, item.IsActive,
                item.OwnerUserAccountId == userAccountId,
                item.OwnerUserAccountId == userAccountId && HasWriteScope(item.GoogleTasksConnection),
                item.IsWriteTarget))
            .ToArray();
    }

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
            if (!source.IsActive)
            {
                source.IsWriteTarget = false;
                source.WriteTargetConfiguredAt = null;
                source.WriteTargetConfiguredByUserAccountId = null;
            }
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

    public async Task<TaskWriteTargetResponse> UpdateWriteTargetAsync(
        Guid householdId, Guid userAccountId, UpdateTaskWriteTargetRequest request,
        CancellationToken cancellationToken)
    {
        RequireMutationsAvailable();
        var sources = await dbContext.HouseholdTaskListSources
            .Include(item => item.GoogleTasksConnection)
            .Where(item => item.HouseholdId == householdId).ToListAsync(cancellationToken);
        HouseholdTaskListSource? selected = null;
        if (request.SourceId is not null)
        {
            selected = sources.SingleOrDefault(item => item.Id == request.SourceId && item.IsActive)
                ?? throw Error(404, Common.ApiProblemCodes.TasksSourceNotFound,
                    "The Google Tasks list was not found.");
            if (selected.OwnerUserAccountId != userAccountId || !HasWriteScope(selected.GoogleTasksConnection))
                throw Error(409, Common.ApiProblemCodes.TasksWriteAuthorizationRequired,
                    "Authorize write access for the adult who owns this task list.");
            var usedElsewhere = await dbContext.HouseholdTaskListSources.AsNoTracking().AnyAsync(item =>
                item.HouseholdId != householdId && item.IsWriteTarget
                && item.GoogleTasksConnectionId == selected.GoogleTasksConnectionId
                && item.ExternalTaskListId == selected.ExternalTaskListId, cancellationToken);
            if (usedElsewhere)
                throw Error(409, Common.ApiProblemCodes.TasksWriteTargetConflict,
                    "This Google task list is already writable for another household.");
        }
        var now = timeProvider.GetUtcNow();
        foreach (var source in sources)
        {
            source.IsWriteTarget = source == selected;
            source.WriteTargetConfiguredAt = source == selected ? now : null;
            source.WriteTargetConfiguredByUserAccountId = source == selected ? userAccountId : null;
        }
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw Error(409, Common.ApiProblemCodes.TasksWriteTargetConflict,
                "The writable Google task list changed. Reload settings and try again.");
        }
        return new(_configuration.MutationsEnabled, selected is not null,
            selected?.Id, selected?.DisplayNameSnapshot);
    }

    public async Task<GoogleTaskMutationResponse> CreateTaskAsync(
        Guid householdId, Guid userAccountId, Guid sessionId, CreateGoogleTaskRequest request,
        string traceId, CancellationToken cancellationToken)
    {
        RequireMutationsAvailable();
        var normalized = NormalizeCreate(request);
        var source = await FindWriteTargetAsync(householdId, cancellationToken);
        var actor = await RequireActorAsync(householdId, userAccountId, sessionId,
            request.AttributedMemberId, cancellationToken);
        var fingerprint = Fingerprint(new { operation = "create", normalized.Title, normalized.Notes,
            normalized.DueDate, actor.MemberId, source.Id });
        var (receipt, recovered) = await GetOrCreateReceiptAsync(source, actor, request.IdempotencyKey,
            GoogleTaskMutationOperation.Create, fingerprint, traceId, cancellationToken);
        if (receipt.Status == GoogleTaskMutationReceiptStatus.Succeeded)
            return MutationResponse(receipt, "needsAction", recovered);
        if (receipt.Status == GoogleTaskMutationReceiptStatus.OutcomeUnknown)
            throw Error(409, Common.ApiProblemCodes.TasksMutationOutcomeUnknown,
                "Google may have created this task. Check the task list before trying again.");
        try
        {
            var token = await GetAccessTokenAsync(source.GoogleTasksConnection, cancellationToken);
            var created = await provider.CreateTaskAsync(token, source.ExternalTaskListId,
                normalized.Title, normalized.Notes, normalized.DueDate, cancellationToken);
            Complete(receipt, created);
            await dbContext.SaveChangesAsync(cancellationToken);
            Invalidate(source.Id);
            return MutationResponse(receipt, created.Status, recovered, created.DueDate);
        }
        catch (GoogleTasksProviderException exception)
        {
            if (exception.Failure == GoogleTasksProviderFailure.ReauthorizationRequired)
                await MarkReauthorizationRequiredAsync(source.GoogleTasksConnection, cancellationToken);
            receipt.Status = exception.Failure is GoogleTasksProviderFailure.Unavailable
                or GoogleTasksProviderFailure.RateLimited
                ? GoogleTaskMutationReceiptStatus.OutcomeUnknown : GoogleTaskMutationReceiptStatus.Failed;
            receipt.FailureCode = exception.Failure.ToString();
            receipt.CompletedAt = timeProvider.GetUtcNow();
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    public async Task<GoogleTaskMutationResponse> UpdateTaskStatusAsync(
        Guid householdId, Guid userAccountId, Guid sessionId, UpdateGoogleTaskStatusRequest request,
        string traceId, CancellationToken cancellationToken)
    {
        RequireMutationsAvailable();
        if (request.IdempotencyKey == Guid.Empty || string.IsNullOrWhiteSpace(request.TaskId)
            || request.TargetStatus is not ("completed" or "needsAction")
            || !mutationProtector.TryUnprotect(request.MutationVersion, out var version)
            || version!.HouseholdId != householdId || version.SourceId != request.SourceId
            || version.TaskId != request.TaskId)
            throw Error(400, Common.ApiProblemCodes.ValidationFailed,
                "A valid request identifier, task, status, and task version are required.");
        var source = await FindWriteTargetAsync(householdId, cancellationToken);
        if (source.Id != request.SourceId)
            throw Error(404, Common.ApiProblemCodes.TasksSourceNotFound, "The writable task list was not found.");
        var actor = await RequireActorAsync(householdId, userAccountId, sessionId,
            request.AttributedMemberId, cancellationToken);
        var operation = request.TargetStatus == "completed"
            ? GoogleTaskMutationOperation.Complete : GoogleTaskMutationOperation.Reopen;
        var fingerprint = Fingerprint(new { operation, request.TaskId, request.TargetStatus,
            version.ProviderETag, actor.MemberId, source.Id });
        var (receipt, recovered) = await GetOrCreateReceiptAsync(source, actor, request.IdempotencyKey,
            operation, fingerprint, traceId, cancellationToken);
        if (receipt.Status == GoogleTaskMutationReceiptStatus.Succeeded)
            return MutationResponse(receipt, request.TargetStatus, true);
        try
        {
            var token = await GetAccessTokenAsync(source.GoogleTasksConnection, cancellationToken);
            var current = await provider.GetTaskAsync(token, source.ExternalTaskListId,
                request.TaskId!, cancellationToken);
            if (current.IsAssigned)
                throw Error(409, Common.ApiProblemCodes.TasksTaskReadOnly,
                    "Assigned Google tasks cannot be changed here.");
            GoogleProviderTask updated;
            if (current.Status == request.TargetStatus)
            {
                updated = current;
                recovered = true;
            }
            else
            {
                if (!string.Equals(current.ETag, version.ProviderETag, StringComparison.Ordinal))
                    throw Error(409, Common.ApiProblemCodes.TasksTaskConflict,
                        "The Google task changed. Reload it before trying again.");
                updated = await provider.UpdateTaskStatusAsync(token, source.ExternalTaskListId,
                    request.TaskId!, version.ProviderETag, request.TargetStatus!, cancellationToken);
            }
            Complete(receipt, updated);
            await dbContext.SaveChangesAsync(cancellationToken);
            Invalidate(source.Id);
            return MutationResponse(receipt, updated.Status, recovered, updated.DueDate);
        }
        catch (GoogleTasksProviderException exception) when (exception.Failure == GoogleTasksProviderFailure.NotFound)
        {
            throw Error(404, Common.ApiProblemCodes.TasksTaskNotFound, "The Google task no longer exists.");
        }
        catch (GoogleTasksProviderException exception) when (exception.Failure == GoogleTasksProviderFailure.VersionConflict)
        {
            throw Error(409, Common.ApiProblemCodes.TasksTaskConflict,
                "The Google task changed. Reload it before trying again.");
        }
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
            source.IsWriteTarget = false;
            source.WriteTargetConfiguredAt = null;
            source.WriteTargetConfiguredByUserAccountId = null;
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
                    item.Position, item.ParentTaskId is not null, item.IsAssigned,
                    source.IsWriteTarget && HasWriteScope(source.GoogleTasksConnection) && !item.IsAssigned,
                    source.IsWriteTarget && HasWriteScope(source.GoogleTasksConnection) && !item.IsAssigned
                        ? mutationProtector.Protect(householdId, source.Id, item.Id, item.ETag) : null)));
                if (result.Page.NextPageToken is not null) remaining[source.Id] = result.Page.NextPageToken;
            }
        }
        var next = remaining.Count == 0 ? null : stateProtector.CreateCursor(new TasksPageCursor(
            householdId, includeCompleted, remaining,
            timeProvider.GetUtcNow() + _configuration.StaleCacheLifetime));
        return new GoogleTasksResponse(tasks.OrderBy(item => item.TaskListName)
            .ThenBy(item => item.Position, StringComparer.Ordinal).ToArray(), next, stale, warnings,
            _configuration.MutationsEnabled && sources.Any(item => item.IsWriteTarget
                && HasWriteScope(item.GoogleTasksConnection)));
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

    private async Task<HouseholdTaskListSource> FindWriteTargetAsync(
        Guid householdId, CancellationToken cancellationToken)
    {
        var source = await dbContext.HouseholdTaskListSources
            .Include(item => item.GoogleTasksConnection).ThenInclude(item => item.UserAccount)
            .SingleOrDefaultAsync(item => item.HouseholdId == householdId && item.IsActive
                && item.IsWriteTarget && item.GoogleTasksConnection.Status == GoogleTasksConnectionStatus.Active
                && item.GoogleTasksConnection.UserAccount.IsActive, cancellationToken)
            ?? throw Error(409, Common.ApiProblemCodes.TasksWriteTargetRequired,
                "Choose a writable Google task list first.");
        if (!HasWriteScope(source.GoogleTasksConnection))
            throw Error(409, Common.ApiProblemCodes.TasksWriteAuthorizationRequired,
                "Authorize Google Tasks write access first.");
        return source;
    }

    private async Task<(Guid UserId, Guid MemberId, bool Shared)> RequireActorAsync(
        Guid householdId, Guid userAccountId, Guid sessionId, Guid? requestedMemberId,
        CancellationToken cancellationToken)
    {
        var session = await dbContext.UserSessions.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == sessionId && item.UserAccountId == userAccountId && item.RevokedAt == null
            && item.SelectedHouseholdId == householdId, cancellationToken)
            ?? throw Error(409, Common.ApiProblemCodes.HouseholdSelectionRequired,
                "Select this household before changing a task.");
        Guid? memberId;
        if (session.IsSharedDisplay)
        {
            memberId = requestedMemberId;
            if (memberId is null || !await dbContext.HouseholdMembers.AsNoTracking().AnyAsync(item =>
                    item.HouseholdId == householdId && item.Id == memberId && item.IsActive,
                    cancellationToken))
                throw Error(400, Common.ApiProblemCodes.ValidationFailed,
                    "Choose the active household member performing this task action.");
        }
        else
        {
            memberId = await dbContext.HouseholdMemberships.AsNoTracking()
                .Where(item => item.HouseholdId == householdId && item.UserAccountId == userAccountId
                    && item.HouseholdMember.IsActive)
                .Select(item => (Guid?)item.HouseholdMemberId).SingleOrDefaultAsync(cancellationToken);
            if (memberId is null)
                throw Error(403, Common.ApiProblemCodes.AdultAccessRequired,
                    "An active adult household member is required.");
        }
        return (userAccountId, memberId.Value, session.IsSharedDisplay);
    }

    private async Task<(GoogleTaskMutationReceipt Receipt, bool Recovered)> GetOrCreateReceiptAsync(
        HouseholdTaskListSource source, (Guid UserId, Guid MemberId, bool Shared) actor,
        Guid id, GoogleTaskMutationOperation operation, byte[] fingerprint, string traceId,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            throw Error(400, Common.ApiProblemCodes.ValidationFailed, "A request identifier is required.");
        var existing = await dbContext.GoogleTaskMutationReceipts.SingleOrDefaultAsync(item =>
            item.HouseholdId == source.HouseholdId && item.Id == id, cancellationToken);
        if (existing is not null)
        {
            if (!CryptographicOperations.FixedTimeEquals(existing.RequestFingerprint, fingerprint))
                throw Error(409, Common.ApiProblemCodes.TasksIdempotencyConflict,
                    "This request identifier was already used for a different task action.");
            return (existing, true);
        }
        var receipt = new GoogleTaskMutationReceipt
        {
            Id = id,
            HouseholdId = source.HouseholdId,
            HouseholdTaskListSourceId = source.Id,
            GoogleTasksConnectionId = source.GoogleTasksConnectionId,
            Operation = operation,
            RequestFingerprint = fingerprint,
            RequestedByUserAccountId = actor.UserId,
            AttributedHouseholdMemberId = actor.MemberId,
            RequestedFromSharedDisplay = actor.Shared,
            TraceId = traceId.Length <= 128 ? traceId : traceId[..128],
            CreatedAt = timeProvider.GetUtcNow(),
        };
        dbContext.GoogleTaskMutationReceipts.Add(receipt);
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            dbContext.Entry(receipt).State = EntityState.Detached;
            existing = await dbContext.GoogleTaskMutationReceipts.SingleAsync(item =>
                item.HouseholdId == source.HouseholdId && item.Id == id, cancellationToken);
            if (!CryptographicOperations.FixedTimeEquals(existing.RequestFingerprint, fingerprint))
                throw Error(409, Common.ApiProblemCodes.TasksIdempotencyConflict,
                    "This request identifier was already used for a different task action.");
            return (existing, true);
        }
        return (receipt, false);
    }

    private void Complete(GoogleTaskMutationReceipt receipt, GoogleProviderTask task)
    {
        receipt.ProviderTaskId = task.Id;
        receipt.ResultProviderETag = task.ETag;
        receipt.Status = GoogleTaskMutationReceiptStatus.Succeeded;
        receipt.CompletedAt = timeProvider.GetUtcNow();
    }

    private GoogleTaskMutationResponse MutationResponse(
        GoogleTaskMutationReceipt receipt, string status, bool recovered, string? dueDate = null) => new(
            receipt.Operation.ToString()[..1].ToLowerInvariant() + receipt.Operation.ToString()[1..],
            receipt.ProviderTaskId!, receipt.HouseholdTaskListSourceId, status, dueDate,
            mutationProtector.Protect(receipt.HouseholdId, receipt.HouseholdTaskListSourceId,
                receipt.ProviderTaskId!, receipt.ResultProviderETag ?? string.Empty),
            receipt.AttributedHouseholdMemberId, recovered);

    private static (string Title, string? Notes, string? DueDate) NormalizeCreate(CreateGoogleTaskRequest request)
    {
        if (request.IdempotencyKey == Guid.Empty)
            throw Error(400, Common.ApiProblemCodes.ValidationFailed, "A request identifier is required.");
        var title = request.Title?.Trim() ?? string.Empty;
        var notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        if (title.Length is < 1 or > 200 || notes?.Length > 2000)
            throw Error(400, Common.ApiProblemCodes.ValidationFailed,
                "Task titles must be 1–200 characters and notes at most 2,000 characters.");
        string? dueDate = null;
        if (!string.IsNullOrWhiteSpace(request.DueDate))
        {
            if (!DateOnly.TryParseExact(request.DueDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var parsed))
                throw Error(400, Common.ApiProblemCodes.ValidationFailed,
                    "Task due dates must use YYYY-MM-DD.");
            dueDate = parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        return (title, notes, dueDate);
    }

    private static byte[] Fingerprint(object value) => SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value));
    private static HashSet<string> ScopeSet(string scopes) => scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .ToHashSet(StringComparer.Ordinal);
    private static bool HasWriteScope(GoogleTasksConnection connection) =>
        ScopeSet(connection.GrantedScopes).Contains(GoogleTasksScopes.Tasks);
    private static bool HasReadScope(GoogleTasksConnection connection)
    {
        var scopes = ScopeSet(connection.GrantedScopes);
        return scopes.Contains(GoogleTasksScopes.TasksReadOnly) || scopes.Contains(GoogleTasksScopes.Tasks);
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

    private void RequireMutationsAvailable()
    {
        RequireAvailable();
        if (!_configuration.MutationsEnabled)
            throw Error(503, Common.ApiProblemCodes.TasksWriteUnavailable,
                "Google Tasks actions are not enabled.");
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

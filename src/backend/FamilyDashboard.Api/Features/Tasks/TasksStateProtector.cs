using System.Text.Json;
using FamilyDashboard.Api.Configuration;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace FamilyDashboard.Api.Features.Tasks;

public sealed record TasksAuthorizationState(
    Guid UserAccountId,
    Guid UserSessionId,
    Guid HouseholdId,
    string ReturnPath,
    string Nonce,
    DateTimeOffset ExpiresAt);

public sealed record TasksPageCursor(
    Guid HouseholdId,
    bool IncludeCompleted,
    Dictionary<Guid, string> RemainingSources,
    DateTimeOffset ExpiresAt);

public sealed class TasksStateProtector(
    IDataProtectionProvider provider,
    TimeProvider timeProvider,
    IOptions<GoogleTasksConfiguration> options)
{
    private readonly IDataProtector _authorization = provider.CreateProtector(
        "FamilyDashboard.GoogleTasksIntegration.AuthorizationState.v1");
    private readonly IDataProtector _cursor = provider.CreateProtector(
        "FamilyDashboard.GoogleTasksIntegration.PageCursor.v1");
    private readonly GoogleTasksConfiguration _configuration = options.Value;

    public (string State, DateTimeOffset ExpiresAt) CreateAuthorization(
        Guid userAccountId, Guid userSessionId, Guid householdId, string returnPath)
    {
        var expiresAt = timeProvider.GetUtcNow() + _configuration.AuthorizationLifetime;
        var value = new TasksAuthorizationState(
            userAccountId,
            userSessionId,
            householdId,
            returnPath,
            Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)),
            expiresAt);
        return (_authorization.Protect(JsonSerializer.Serialize(value)), expiresAt);
    }

    public bool TryReadAuthorization(string? state, out TasksAuthorizationState? value) =>
        TryRead(_authorization, state, out value) && value!.ExpiresAt > timeProvider.GetUtcNow();

    public string CreateCursor(TasksPageCursor cursor) =>
        _cursor.Protect(JsonSerializer.Serialize(cursor));

    public bool TryReadCursor(string? cursor, out TasksPageCursor? value) =>
        TryRead(_cursor, cursor, out value) && value!.ExpiresAt > timeProvider.GetUtcNow();

    private static bool TryRead<T>(IDataProtector protector, string? protectedValue, out T? value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(protectedValue)) return false;
        try
        {
            value = JsonSerializer.Deserialize<T>(protector.Unprotect(protectedValue));
            return value is not null;
        }
        catch (Exception exception) when (exception is System.Security.Cryptography.CryptographicException or JsonException)
        {
            return false;
        }
    }
}

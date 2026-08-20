using System.Text.Json;
using FamilyDashboard.Api.Configuration;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace FamilyDashboard.Api.Features.Calendar;

public sealed record CalendarAuthorizationState(
    Guid UserAccountId,
    Guid UserSessionId,
    Guid HouseholdId,
    string ReturnPath,
    string Capability,
    string Nonce,
    DateTimeOffset ExpiresAt);

public sealed record CalendarPageCursor(
    Guid HouseholdId,
    DateTimeOffset From,
    DateTimeOffset To,
    Dictionary<Guid, string?> RemainingSources,
    DateTimeOffset ExpiresAt);

public sealed class CalendarStateProtector(
    IDataProtectionProvider provider,
    TimeProvider timeProvider,
    IOptions<GoogleCalendarConfiguration> options)
{
    private readonly IDataProtector _authorization = provider.CreateProtector(
        "FamilyDashboard.GoogleCalendarIntegration.AuthorizationState.v2");
    private readonly IDataProtector _cursor = provider.CreateProtector(
        "FamilyDashboard.GoogleCalendarIntegration.EventCursor.v1");
    private readonly GoogleCalendarConfiguration _configuration = options.Value;

    public (string State, DateTimeOffset ExpiresAt) CreateAuthorization(
        Guid userAccountId, Guid userSessionId, Guid householdId, string returnPath,
        string capability = CalendarAuthorizationCapabilities.ReadOnly)
    {
        var expiresAt = timeProvider.GetUtcNow() + _configuration.AuthorizationLifetime;
        var value = new CalendarAuthorizationState(
            userAccountId, userSessionId, householdId, returnPath, capability,
            Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)),
            expiresAt);
        return (_authorization.Protect(JsonSerializer.Serialize(value)), expiresAt);
    }

    public bool TryReadAuthorization(string? state, out CalendarAuthorizationState? value) =>
        TryRead(_authorization, state, out value) && value!.ExpiresAt > timeProvider.GetUtcNow();

    public string CreateCursor(CalendarPageCursor cursor) =>
        _cursor.Protect(JsonSerializer.Serialize(cursor));

    public bool TryReadCursor(string? cursor, out CalendarPageCursor? value) =>
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

public static class CalendarAuthorizationCapabilities
{
    public const string ReadOnly = "readOnly";
    public const string EventCreation = "eventCreation";
}

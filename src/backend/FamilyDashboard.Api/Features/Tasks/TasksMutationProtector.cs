using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace FamilyDashboard.Api.Features.Tasks;

public sealed record TasksMutationVersion(
    Guid HouseholdId,
    Guid SourceId,
    string TaskId,
    string ProviderETag,
    DateTimeOffset ExpiresAt);

public sealed class TasksMutationProtector(IDataProtectionProvider provider, TimeProvider timeProvider)
{
    private readonly IDataProtector _protector = provider.CreateProtector(
        "FamilyDashboard.GoogleTasksIntegration.MutationVersion.v1");

    public string Protect(Guid householdId, Guid sourceId, string taskId, string providerETag) =>
        _protector.Protect(JsonSerializer.Serialize(new TasksMutationVersion(
            householdId, sourceId, taskId, providerETag, timeProvider.GetUtcNow() + TimeSpan.FromHours(1))));

    public bool TryUnprotect(string? value, out TasksMutationVersion? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(value)) return false;
        try
        {
            version = JsonSerializer.Deserialize<TasksMutationVersion>(_protector.Unprotect(value));
            return version is not null && version.ExpiresAt > timeProvider.GetUtcNow();
        }
        catch (Exception exception) when (exception is System.Security.Cryptography.CryptographicException or JsonException)
        {
            return false;
        }
    }
}

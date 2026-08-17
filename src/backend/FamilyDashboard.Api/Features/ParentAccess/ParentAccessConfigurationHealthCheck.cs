using FamilyDashboard.Api.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace FamilyDashboard.Api.Features.ParentAccess;

public sealed class ParentAccessConfigurationHealthCheck(
    ParentPinHasher hasher,
    IOptions<ParentAccessConfiguration> options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
            return Task.FromResult(HealthCheckResult.Healthy("Parent access is disabled."));
        return Task.FromResult(hasher.IsAvailable
            ? HealthCheckResult.Healthy("Parent access cryptography is configured.")
            : HealthCheckResult.Unhealthy("Parent access is enabled without a valid 32-byte pepper."));
    }
}

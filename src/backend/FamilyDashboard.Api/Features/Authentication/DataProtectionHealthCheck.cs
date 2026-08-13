using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FamilyDashboard.Api.Features.Authentication;

public sealed class DataProtectionHealthCheck(IDataProtectionProvider provider) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var protector = provider.CreateProtector("FamilyDashboard.HealthCheck");
            const string value = "healthy";
            var protectedValue = protector.Protect(value);
            return Task.FromResult(protector.Unprotect(protectedValue) == value
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Data Protection round trip failed."));
        }
        catch (Exception exception)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Data Protection is unavailable.",
                exception));
        }
    }
}

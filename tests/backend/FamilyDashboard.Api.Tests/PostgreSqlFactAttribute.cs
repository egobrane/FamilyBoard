namespace FamilyDashboard.Api.Tests;

[AttributeUsage(AttributeTargets.Method)]
public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION_STRING")))
        {
            Skip = "TEST_POSTGRES_CONNECTION_STRING is not configured.";
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FamilyDashboard.Api.Persistence;

public sealed class FamilyDashboardDbContextFactory : IDesignTimeDbContextFactory<FamilyDashboardDbContext>
{
    public FamilyDashboardDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("FAMILY_DASHBOARD_DESIGN_CONNECTION")
            ?? "Host=localhost;Database=family_dashboard;Username=family_dashboard";

        var options = new DbContextOptionsBuilder<FamilyDashboardDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new FamilyDashboardDbContext(options);
    }
}

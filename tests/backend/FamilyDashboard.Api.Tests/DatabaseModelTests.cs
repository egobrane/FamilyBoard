using FamilyDashboard.Api.Domain.Chores;
using FamilyDashboard.Api.Domain.Households;
using FamilyDashboard.Api.Domain.Rewards;
using FamilyDashboard.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyDashboard.Api.Tests;

public sealed class DatabaseModelTests
{
    [Fact]
    public void ModelContainsTheFoundationalEntities()
    {
        var options = new DbContextOptionsBuilder<FamilyDashboardDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only;Username=model_only")
            .Options;

        using var context = new FamilyDashboardDbContext(options);
        var model = context.Model;

        Assert.NotNull(model.FindEntityType(typeof(Household)));
        Assert.NotNull(model.FindEntityType(typeof(HouseholdMember)));
        Assert.NotNull(model.FindEntityType(typeof(ChoreDefinition)));
        Assert.NotNull(model.FindEntityType(typeof(ChoreAssignment)));
        Assert.NotNull(model.FindEntityType(typeof(ChoreCompletion)));
        Assert.NotNull(model.FindEntityType(typeof(PointTransaction)));
        Assert.NotNull(model.FindEntityType(typeof(Reward)));
        Assert.NotNull(model.FindEntityType(typeof(RewardRedemption)));
    }
}

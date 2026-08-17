using FamilyDashboard.Api.Configuration;
using FamilyDashboard.Api.Features.ParentAccess;
using Microsoft.Extensions.Options;

namespace FamilyDashboard.Api.Tests.ParentAccess;

public sealed class ParentPinHasherTests
{
    [Fact]
    public void HashesWithUniqueSaltAndVerifiesOnlyTheCorrectPin()
    {
        var hasher = CreateHasher();
        var householdId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var first = hasher.Create(householdId, accountId, "482913", now);
        var second = hasher.Create(householdId, accountId, "482913", now);

        Assert.NotEqual(first.Salt, second.Salt);
        Assert.NotEqual(first.PinHash, second.PinHash);
        Assert.True(hasher.Verify(first, "482913"));
        Assert.False(hasher.Verify(first, "482914"));
        Assert.Equal(32, first.PinHash.Length);
        Assert.Equal(16, first.Salt.Length);
    }

    [Fact]
    public void IsUnavailableWhenPepperIsMissing()
    {
        var hasher = new ParentPinHasher(Options.Create(new ParentAccessConfiguration
        {
            Enabled = true,
            Pepper = string.Empty,
        }));

        Assert.False(hasher.IsAvailable);
    }

    private static ParentPinHasher CreateHasher() => new(Options.Create(
        new ParentAccessConfiguration
        {
            Enabled = true,
            Pepper = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=",
            WorkFactor = 1000,
        }));
}

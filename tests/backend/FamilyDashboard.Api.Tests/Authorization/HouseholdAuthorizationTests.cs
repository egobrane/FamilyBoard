using FamilyDashboard.Api.Security;
using FamilyDashboard.Api.Tests.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FamilyDashboard.Api.Tests.Authorization;

public sealed class HouseholdAuthorizationTests
{
    [Fact]
    public async Task AuthenticatedUserWithoutPersistedHouseholdAccessIsDenied()
    {
        await using var services = CreateServices(new FakeHouseholdAccessEvaluator());
        using var scope = services.CreateScope();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();

        var result = await authorization.AuthorizeAsync(
            TestClaims.AuthenticatedAdult(Guid.NewGuid()),
            new HouseholdAccessResource(Guid.NewGuid()),
            HouseholdAuthorizationPolicies.Member);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task AnonymousUserIsDenied()
    {
        var result = await AuthorizeAsync(
            TestClaims.Anonymous(),
            HouseholdAccessLevel.Adult,
            HouseholdAuthorizationPolicies.Member);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task AuthenticatedPrincipalWithoutInternalUserIdIsDenied()
    {
        var result = await AuthorizeAsync(
            TestClaims.AuthenticatedWithoutUserAccountId(),
            HouseholdAccessLevel.Adult,
            HouseholdAuthorizationPolicies.Member);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task HouseholdMemberCanUseMemberPolicyButNotAdultPolicy()
    {
        var memberResult = await AuthorizeAsync(
            HouseholdAccessLevel.Member,
            HouseholdAuthorizationPolicies.Member);
        var adultResult = await AuthorizeAsync(
            HouseholdAccessLevel.Member,
            HouseholdAuthorizationPolicies.Adult);

        Assert.True(memberResult.Succeeded);
        Assert.False(adultResult.Succeeded);
    }

    [Fact]
    public async Task HouseholdAdultCanUseBothPolicies()
    {
        var memberResult = await AuthorizeAsync(
            HouseholdAccessLevel.Adult,
            HouseholdAuthorizationPolicies.Member);
        var adultResult = await AuthorizeAsync(
            HouseholdAccessLevel.Adult,
            HouseholdAuthorizationPolicies.Adult);

        Assert.True(memberResult.Succeeded);
        Assert.True(adultResult.Succeeded);
    }

    [Fact]
    public async Task AccessToOneHouseholdDoesNotGrantAccessToAnother()
    {
        var userAccountId = Guid.NewGuid();
        var allowedHouseholdId = Guid.NewGuid();
        var otherHouseholdId = Guid.NewGuid();
        var evaluator = new FakeHouseholdAccessEvaluator();
        evaluator.SetAccess(userAccountId, allowedHouseholdId, HouseholdAccessLevel.Adult);

        await using var services = CreateServices(evaluator);
        using var scope = services.CreateScope();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();

        var result = await authorization.AuthorizeAsync(
            TestClaims.AuthenticatedAdult(userAccountId),
            new HouseholdAccessResource(otherHouseholdId),
            HouseholdAuthorizationPolicies.Member);

        Assert.False(result.Succeeded);
    }

    private static Task<AuthorizationResult> AuthorizeAsync(
        HouseholdAccessLevel access,
        string policyName)
    {
        return AuthorizeAsync(TestClaims.AuthenticatedAdult(Guid.NewGuid()), access, policyName);
    }

    private static async Task<AuthorizationResult> AuthorizeAsync(
        System.Security.Claims.ClaimsPrincipal principal,
        HouseholdAccessLevel access,
        string policyName)
    {
        var userAccountId = Guid.TryParse(
            principal.FindFirst(FamilyDashboardClaimTypes.UserAccountId)?.Value,
            out var parsedUserAccountId)
            ? parsedUserAccountId
            : Guid.NewGuid();
        var householdId = Guid.NewGuid();
        var evaluator = new FakeHouseholdAccessEvaluator();
        evaluator.SetAccess(userAccountId, householdId, access);

        await using var services = CreateServices(evaluator);
        using var scope = services.CreateScope();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();

        return await authorization.AuthorizeAsync(
            principal,
            new HouseholdAccessResource(householdId),
            policyName);
    }

    private static ServiceProvider CreateServices(FakeHouseholdAccessEvaluator? evaluator = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFamilyDashboardAuthorization();

        if (evaluator is not null)
        {
            services.RemoveAll<IHouseholdAccessEvaluator>();
            services.AddSingleton<IHouseholdAccessEvaluator>(evaluator);
        }

        return services.BuildServiceProvider();
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FamilyDashboard.Api.Security;

public static class AuthorizationServiceCollectionExtensions
{
    public static IServiceCollection AddFamilyDashboardAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(HouseholdAuthorizationPolicies.Member, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new HouseholdAccessRequirement(HouseholdAccessLevel.Member));
            });

            options.AddPolicy(HouseholdAuthorizationPolicies.Adult, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new HouseholdAccessRequirement(HouseholdAccessLevel.Adult));
            });

            options.AddPolicy(HouseholdAuthorizationPolicies.Administration, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new HouseholdAdministrationRequirement());
            });
        });

        services.TryAddScoped<IHouseholdAccessEvaluator, EfHouseholdAccessEvaluator>();
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IAuthorizationHandler, HouseholdAuthorizationHandler>());
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IAuthorizationHandler, HouseholdAdministrationAuthorizationHandler>());
        return services;
    }
}

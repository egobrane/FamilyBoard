using FamilyDashboard.Api.Features.Authentication;
using FamilyDashboard.Api.Features.Common;
using FamilyDashboard.Api.Security;
using Microsoft.AspNetCore.Authorization;

namespace FamilyDashboard.Api.Features.Households;

public static class HouseholdEndpoints
{
    public static IEndpointRouteBuilder MapHouseholdEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/households", ListHouseholdsAsync)
            .RequireAuthorization();
        endpoints.MapPost("/api/households", CreateHouseholdAsync)
            .RequireAuthorization()
            .RequireFamilyDashboardAntiforgery();
        endpoints.MapGet("/api/households/{householdId:guid}", GetHouseholdAsync)
            .RequireAuthorization();
        endpoints.MapPatch("/api/households/{householdId:guid}", UpdateHouseholdAsync)
            .RequireAuthorization()
            .RequireFamilyDashboardAntiforgery();
        return endpoints;
    }

    private static async Task<IResult> ListHouseholdsAsync(
        HttpContext context,
        HouseholdService householdService,
        CancellationToken cancellationToken)
    {
        var account = await ResolveAccountAsync(context, householdService, cancellationToken);
        if (account is null)
        {
            return AccountUnavailable(context);
        }

        return Results.Ok(await householdService.ListAsync(account.Value, cancellationToken));
    }

    private static async Task<IResult> CreateHouseholdAsync(
        CreateHouseholdRequest? request,
        HttpContext context,
        HouseholdService householdService,
        UserSessionService sessionService,
        CancellationToken cancellationToken)
    {
        if (!context.User.TryGetUserAccountId(out var userAccountId))
        {
            return AccountUnavailable(context);
        }

        var account = await householdService.FindActiveAccountAsync(userAccountId, cancellationToken);
        if (account is null)
        {
            return AccountUnavailable(context);
        }

        if (!HouseholdValidation.TryValidate(request, out var values, out var errors))
        {
            return ValidationFailed(context, errors);
        }

        var session = await sessionService.FindCurrentForUpdateAsync(context.User, cancellationToken);
        if (session?.IsSharedDisplay == true)
        {
            return Results.Problem(ApiProblems.Create(
                context,
                StatusCodes.Status403Forbidden,
                ApiProblemCodes.PrivateSessionRequired,
                "Use a private adult session to create a household."));
        }
        var response = await householdService.CreateAsync(
            account,
            values!,
            session,
            cancellationToken);
        return Results.Created($"/api/households/{response.Id}", response);
    }

    private static async Task<IResult> GetHouseholdAsync(
        Guid householdId,
        HttpContext context,
        IAuthorizationService authorizationService,
        HouseholdService householdService,
        CancellationToken cancellationToken)
    {
        var userAccountId = await ResolveAccountAsync(context, householdService, cancellationToken);
        if (userAccountId is null)
        {
            return AccountUnavailable(context);
        }

        if (!await HasAccessAsync(
                context,
                authorizationService,
                householdId,
                HouseholdAuthorizationPolicies.Member,
                cancellationToken))
        {
            return HouseholdNotFound(context);
        }

        if (!await HasAccessAsync(
                context,
                authorizationService,
                householdId,
                HouseholdAuthorizationPolicies.Adult,
                cancellationToken))
        {
            return AdultAccessRequired(context);
        }

        if (!await HasAccessAsync(
                context,
                authorizationService,
                householdId,
                HouseholdAuthorizationPolicies.Administration,
                cancellationToken))
        {
            return ParentAccess.ParentAccessEndpoints.ParentElevationRequired(context);
        }

        var response = await householdService.GetAsync(
            householdId,
            userAccountId.Value,
            cancellationToken);
        return response is null ? HouseholdNotFound(context) : Results.Ok(response);
    }

    private static async Task<IResult> UpdateHouseholdAsync(
        Guid householdId,
        UpdateHouseholdRequest? request,
        HttpContext context,
        IAuthorizationService authorizationService,
        HouseholdService householdService,
        CancellationToken cancellationToken)
    {
        var userAccountId = await ResolveAccountAsync(context, householdService, cancellationToken);
        if (userAccountId is null)
        {
            return AccountUnavailable(context);
        }

        if (!await HasAccessAsync(
                context,
                authorizationService,
                householdId,
                HouseholdAuthorizationPolicies.Member,
                cancellationToken))
        {
            return HouseholdNotFound(context);
        }

        if (!await HasAccessAsync(
                context,
                authorizationService,
                householdId,
                HouseholdAuthorizationPolicies.Adult,
                cancellationToken))
        {
            return AdultAccessRequired(context);
        }

        if (!await HasAccessAsync(
                context,
                authorizationService,
                householdId,
                HouseholdAuthorizationPolicies.Administration,
                cancellationToken))
        {
            return ParentAccess.ParentAccessEndpoints.ParentElevationRequired(context);
        }

        if (!HouseholdValidation.TryValidate(request, out var patch, out var errors))
        {
            return ValidationFailed(context, errors);
        }

        var response = await householdService.UpdateAsync(
            householdId,
            userAccountId.Value,
            patch!,
            cancellationToken);
        return response is null ? HouseholdNotFound(context) : Results.Ok(response);
    }

    internal static async Task<Guid?> ResolveAccountAsync(
        HttpContext context,
        HouseholdService householdService,
        CancellationToken cancellationToken)
    {
        if (!context.User.TryGetUserAccountId(out var userAccountId))
        {
            return null;
        }

        var account = await householdService.FindActiveAccountAsync(userAccountId, cancellationToken);
        return account?.Id;
    }

    internal static async Task<bool> HasAccessAsync(
        HttpContext context,
        IAuthorizationService authorizationService,
        Guid householdId,
        string policy,
        CancellationToken cancellationToken)
    {
        var result = await authorizationService.AuthorizeAsync(
            context.User,
            new HouseholdAccessResource(householdId, cancellationToken),
            policy);
        return result.Succeeded;
    }

    internal static IResult AccountUnavailable(HttpContext context)
    {
        return Results.Problem(ApiProblems.Create(
            context,
            StatusCodes.Status401Unauthorized,
            ApiProblemCodes.AccountUnavailable,
            "The authenticated account is unavailable."));
    }

    internal static IResult HouseholdNotFound(HttpContext context)
    {
        return Results.Problem(ApiProblems.Create(
            context,
            StatusCodes.Status404NotFound,
            ApiProblemCodes.HouseholdNotFound,
            "The household was not found."));
    }

    internal static IResult AdultAccessRequired(HttpContext context)
    {
        return Results.Problem(ApiProblems.Create(
            context,
            StatusCodes.Status403Forbidden,
            ApiProblemCodes.AdultAccessRequired,
            "Adult household access is required."));
    }

    internal static IResult ValidationFailed(
        HttpContext context,
        IDictionary<string, string[]> errors)
    {
        return Results.Problem(ApiProblems.Create(
            context,
            StatusCodes.Status400BadRequest,
            ApiProblemCodes.ValidationFailed,
            "One or more values are invalid.",
            errors: errors));
    }
}

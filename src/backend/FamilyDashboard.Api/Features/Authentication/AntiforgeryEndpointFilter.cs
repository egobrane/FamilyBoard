using FamilyDashboard.Api.Features.Common;
using Microsoft.AspNetCore.Antiforgery;

namespace FamilyDashboard.Api.Features.Authentication;

public sealed class AntiforgeryEndpointFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        if (!string.Equals(
                context.HttpContext.User.Identity?.AuthenticationType,
                AuthenticationSchemes.ApplicationCookie,
                StringComparison.Ordinal))
        {
            return await next(context);
        }

        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.Problem(ApiProblems.Create(
                context.HttpContext,
                StatusCodes.Status400BadRequest,
                ApiProblemCodes.AntiforgeryValidationFailed,
                "The antiforgery token is missing or invalid."));
        }

        return await next(context);
    }
}

public static class AntiforgeryEndpointConventionBuilderExtensions
{
    public static RouteHandlerBuilder RequireFamilyDashboardAntiforgery(
        this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter<AntiforgeryEndpointFilter>();
}

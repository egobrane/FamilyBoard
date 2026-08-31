using FamilyDashboard.Api.Features.Common;
using FamilyDashboard.Api.Features.Authentication;
using FamilyDashboard.Api.Features.Households;
using FamilyDashboard.Api.Persistence;
using FamilyDashboard.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace FamilyDashboard.Api.Features.Dashboard;

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/households/{householdId:guid}/dashboard-appearance", GetAppearanceAsync).RequireAuthorization();
        endpoints.MapPut("/api/households/{householdId:guid}/dashboard-appearance", UpdateAppearanceAsync)
            .RequireAuthorization().RequireFamilyDashboardAntiforgery();
        endpoints.MapPost("/api/households/{householdId:guid}/dashboard-photo", UploadPhotoAsync)
            .RequireAuthorization().RequireFamilyDashboardAntiforgery();
        endpoints.MapDelete("/api/households/{householdId:guid}/dashboard-photo", RemovePhotoAsync)
            .RequireAuthorization().RequireFamilyDashboardAntiforgery();
        endpoints.MapGet("/api/households/{householdId:guid}/dashboard-photo/{assetId:guid}/{variant}", GetPhotoAsync)
            .RequireAuthorization();
        endpoints.MapGet("/api/households/{householdId:guid}/weather", GetWeatherAsync).RequireAuthorization();
        endpoints.MapGet("/api/households/{householdId:guid}/weather-settings", GetWeatherSettingsAsync).RequireAuthorization();
        endpoints.MapPut("/api/households/{householdId:guid}/weather-settings", UpdateWeatherSettingsAsync)
            .RequireAuthorization().RequireFamilyDashboardAntiforgery();
        endpoints.MapDelete("/api/households/{householdId:guid}/weather-settings", DeleteWeatherSettingsAsync)
            .RequireAuthorization().RequireFamilyDashboardAntiforgery();
        return endpoints;
    }

    private static async Task<IResult> GetAppearanceAsync(Guid householdId, HttpContext context,
        IAuthorizationService authorization, DashboardAppearanceService service, CancellationToken cancellationToken)
    {
        if (!await HasAsync(context, authorization, householdId, HouseholdAuthorizationPolicies.Member, cancellationToken))
            return HouseholdEndpoints.HouseholdNotFound(context);
        return Results.Ok(await service.GetAsync(householdId, cancellationToken));
    }

    private static async Task<IResult> UpdateAppearanceAsync(Guid householdId, UpdateDashboardAppearanceRequest? request,
        HttpContext context, IAuthorizationService authorization, DashboardAppearanceService service, CancellationToken cancellationToken)
    {
        var denial = await RequireAdministrationAsync(householdId, context, authorization, cancellationToken);
        if (denial is not null) return denial;
        var errors = DashboardValidation.Validate(request);
        if (errors.Count > 0) return HouseholdEndpoints.ValidationFailed(context, errors);
        try
        {
            var value = await service.UpdateAsync(householdId, request!, cancellationToken);
            return value is null ? HouseholdEndpoints.HouseholdNotFound(context) : Results.Ok(value);
        }
        catch (DbUpdateConcurrencyException) { return Conflict(context); }
    }

    private static async Task<IResult> UploadPhotoAsync(Guid householdId, IFormFile? photo, HttpContext context,
        IAuthorizationService authorization, DashboardAppearanceService service, FamilyDashboardDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var denial = await RequireAdministrationAsync(householdId, context, authorization, cancellationToken);
        if (denial is not null) return denial;
        if (photo is null) return HouseholdEndpoints.ValidationFailed(context, new Dictionary<string, string[]> { ["photo"] = ["Choose a photo to upload."] });
        var memberId = await CurrentMemberIdAsync(context, householdId, dbContext, cancellationToken);
        if (memberId is null) return HouseholdEndpoints.HouseholdNotFound(context);
        try
        {
            await using var stream = photo.OpenReadStream();
            return Results.Ok(await service.UploadAsync(householdId, memberId.Value, stream, photo.Length, cancellationToken));
        }
        catch (InvalidHouseholdPhotoException exception)
        {
            return HouseholdEndpoints.ValidationFailed(context, new Dictionary<string, string[]> { ["photo"] = [exception.Message] });
        }
        catch (HouseholdMediaUnavailableException)
        {
            return Results.Problem(ApiProblems.Create(context, 503, ApiProblemCodes.HouseholdMediaUnavailable,
                "Household photo storage is currently unavailable."));
        }
    }

    private static async Task<IResult> RemovePhotoAsync(Guid householdId, HttpContext context,
        IAuthorizationService authorization, DashboardAppearanceService service, CancellationToken cancellationToken)
    {
        var denial = await RequireAdministrationAsync(householdId, context, authorization, cancellationToken);
        if (denial is not null) return denial;
        return Results.Ok(await service.RemovePhotoAsync(householdId, cancellationToken));
    }

    private static async Task<IResult> GetPhotoAsync(Guid householdId, Guid assetId, string variant, HttpContext context,
        IAuthorizationService authorization, DashboardAppearanceService service, CancellationToken cancellationToken)
    {
        if (!await HasAsync(context, authorization, householdId, HouseholdAuthorizationPolicies.Member, cancellationToken))
            return HouseholdEndpoints.HouseholdNotFound(context);
        var photo = await service.ReadAsync(householdId, assetId, variant.ToLowerInvariant(), cancellationToken);
        if (photo is null) return Results.NotFound();
        context.Response.Headers.CacheControl = "private, no-cache";
        context.Response.Headers.ETag = photo.ETag;
        return Results.Stream(photo.Content, photo.ContentType, enableRangeProcessing: true);
    }

    private static async Task<IResult> GetWeatherAsync(Guid householdId, HttpContext context,
        IAuthorizationService authorization, WeatherService service, CancellationToken cancellationToken)
    {
        if (!await HasAsync(context, authorization, householdId, HouseholdAuthorizationPolicies.Member, cancellationToken))
            return HouseholdEndpoints.HouseholdNotFound(context);
        try
        {
            var value = await service.GetWeatherAsync(householdId, cancellationToken);
            return value is null
                ? Results.Ok(new { status = "locationRequired", attribution = "Weather data from the National Weather Service" })
                : Results.Ok(value);
        }
        catch (WeatherProviderRateLimitedException)
        {
            return Results.Problem(ApiProblems.Create(context, 429, ApiProblemCodes.WeatherProviderRateLimited,
                "Weather is temporarily rate limited. Try again shortly."));
        }
        catch (Exception exception) when (exception is WeatherUnavailableException or WeatherProviderException)
        {
            return Results.Problem(ApiProblems.Create(context, 503, ApiProblemCodes.WeatherUnavailable,
                "Weather is temporarily unavailable."));
        }
    }

    private static async Task<IResult> GetWeatherSettingsAsync(Guid householdId, HttpContext context,
        IAuthorizationService authorization, WeatherService service, CancellationToken cancellationToken)
    {
        var denial = await RequireAdministrationAsync(householdId, context, authorization, cancellationToken);
        if (denial is not null) return denial;
        var value = await service.GetSettingsAsync(householdId, cancellationToken);
        return value is null ? Results.NoContent() : Results.Ok(WeatherService.Map(value));
    }

    private static async Task<IResult> UpdateWeatherSettingsAsync(Guid householdId, UpdateWeatherSettingsRequest? request,
        HttpContext context, IAuthorizationService authorization, WeatherService service, CancellationToken cancellationToken)
    {
        var denial = await RequireAdministrationAsync(householdId, context, authorization, cancellationToken);
        if (denial is not null) return denial;
        var errors = DashboardValidation.Validate(request);
        if (errors.Count > 0) return HouseholdEndpoints.ValidationFailed(context, errors);
        try { return Results.Ok(await service.UpsertSettingsAsync(householdId, request!, cancellationToken)); }
        catch (DbUpdateConcurrencyException) { return Conflict(context); }
    }

    private static async Task<IResult> DeleteWeatherSettingsAsync(Guid householdId, HttpContext context,
        IAuthorizationService authorization, WeatherService service, CancellationToken cancellationToken)
    {
        var denial = await RequireAdministrationAsync(householdId, context, authorization, cancellationToken);
        if (denial is not null) return denial;
        await service.DeleteSettingsAsync(householdId, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult?> RequireAdministrationAsync(Guid householdId, HttpContext context,
        IAuthorizationService authorization, CancellationToken cancellationToken)
    {
        if (!await HasAsync(context, authorization, householdId, HouseholdAuthorizationPolicies.Member, cancellationToken))
            return HouseholdEndpoints.HouseholdNotFound(context);
        if (!await HasAsync(context, authorization, householdId, HouseholdAuthorizationPolicies.Adult, cancellationToken))
            return HouseholdEndpoints.AdultAccessRequired(context);
        return await HasAsync(context, authorization, householdId, HouseholdAuthorizationPolicies.Administration, cancellationToken)
            ? null : ParentAccess.ParentAccessEndpoints.ParentElevationRequired(context);
    }

    private static Task<bool> HasAsync(HttpContext context, IAuthorizationService authorization, Guid householdId,
        string policy, CancellationToken cancellationToken) => HouseholdEndpoints.HasAccessAsync(context, authorization, householdId, policy, cancellationToken);

    private static async Task<Guid?> CurrentMemberIdAsync(HttpContext context, Guid householdId,
        FamilyDashboardDbContext dbContext, CancellationToken cancellationToken)
    {
        if (!context.User.TryGetUserAccountId(out var accountId)) return null;
        return await dbContext.HouseholdMemberships.Where(value => value.UserAccountId == accountId
            && value.HouseholdId == householdId && value.HouseholdMember.IsActive)
            .Select(value => (Guid?)value.HouseholdMemberId).SingleOrDefaultAsync(cancellationToken);
    }

    private static IResult Conflict(HttpContext context) => Results.Problem(ApiProblems.Create(context, 409,
        ApiProblemCodes.DashboardPreferencesConflict, "These dashboard settings changed. Refresh and try again."));
}

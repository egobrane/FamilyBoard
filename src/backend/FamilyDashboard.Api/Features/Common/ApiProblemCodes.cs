using Microsoft.AspNetCore.Mvc;

namespace FamilyDashboard.Api.Features.Common;

public static class ApiProblemCodes
{
    public const string AuthenticationRequired = "authentication_required";
    public const string AuthenticationUnavailable = "authentication_unavailable";
    public const string AuthenticationFailed = "authentication_failed";
    public const string InvalidReturnUrl = "invalid_return_url";
    public const string AntiforgeryValidationFailed = "antiforgery_validation_failed";
    public const string AccountUnavailable = "account_unavailable";
    public const string HouseholdNotFound = "household_not_found";
    public const string HouseholdMemberNotFound = "household_member_not_found";
    public const string AdultAccessRequired = "adult_access_required";
    public const string LastActiveAdult = "last_active_adult";
    public const string SelfDeactivationRequiresLeaveFlow = "self_deactivation_requires_leave_flow";
    public const string ValidationFailed = "validation_failed";
    public const string Conflict = "conflict";
    public const string ActiveInvitationExists = "active_invitation_exists";
    public const string InvitationNotFound = "invitation_not_found";
    public const string InvitationExpired = "invitation_expired";
    public const string InvitationRevoked = "invitation_revoked";
    public const string InvitationUsed = "invitation_used";
    public const string InvitationUnavailable = "invitation_unavailable";
    public const string InvitationEmailMismatch = "invitation_email_mismatch";
    public const string InvitationOriginNotAllowed = "invitation_origin_not_allowed";
    public const string InvitationConflict = "invitation_conflict";
    public const string ParentAccessUnavailable = "parent_access_unavailable";
    public const string ParentElevationRequired = "parent_elevation_required";
    public const string ParentPinNotConfigured = "parent_pin_not_configured";
    public const string ParentPinAlreadyConfigured = "parent_pin_already_configured";
    public const string ParentPinInvalid = "parent_pin_invalid";
    public const string ParentPinLocked = "parent_pin_locked";
    public const string ParentPinRateLimited = "parent_pin_rate_limited";
    public const string RecentAuthenticationRequired = "recent_authentication_required";
    public const string PrivateSessionRequired = "private_session_required";
    public const string SharedDisplayRequiresPin = "shared_display_requires_pin";
    public const string ParentAccessConflict = "parent_access_conflict";
    public const string UnexpectedError = "unexpected_error";
}

public static class ApiProblems
{
    public static ProblemDetails Create(
        HttpContext httpContext,
        int status,
        string code,
        string title,
        string? detail = null,
        IDictionary<string, string[]>? errors = null)
    {
        ProblemDetails problem = errors is null
            ? new ProblemDetails()
            : new ValidationProblemDetails(errors);

        problem.Type = $"urn:family-dashboard:problem:{code}";
        problem.Title = title;
        problem.Status = status;
        problem.Detail = detail;
        problem.Instance = httpContext.Request.Path;
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        return problem;
    }
}

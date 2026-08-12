using Microsoft.AspNetCore.Mvc;

namespace FamilyDashboard.Api.Features.Common;

public static class ApiProblemCodes
{
    public const string AuthenticationRequired = "authentication_required";
    public const string AccountUnavailable = "account_unavailable";
    public const string HouseholdNotFound = "household_not_found";
    public const string HouseholdMemberNotFound = "household_member_not_found";
    public const string AdultAccessRequired = "adult_access_required";
    public const string LastActiveAdult = "last_active_adult";
    public const string ValidationFailed = "validation_failed";
    public const string Conflict = "conflict";
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

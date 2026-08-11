using System.Text.Json;
using FamilyDashboard.Api.Features.Authentication;
using FamilyDashboard.Api.Features.Common;
using FamilyDashboard.Api.Features.HouseholdMembers;
using FamilyDashboard.Api.Features.Households;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FamilyDashboard.Api.Tests.Contracts;

public sealed class IdentityHouseholdContractTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public void CurrentUserContractUsesCamelCaseAndLowercaseHouseholdRole()
    {
        var selectedHouseholdId = Guid.NewGuid();
        var response = new CurrentUserResponse(
            new UserAccountResponse(Guid.NewGuid(), "Adult", "adult@example.test"),
            [new HouseholdSummaryResponse(
                selectedHouseholdId,
                "Family",
                Guid.NewGuid(),
                HouseholdContractRoles.Adult)],
            selectedHouseholdId);

        var json = JsonSerializer.Serialize(response, WebJson);
        using var document = JsonDocument.Parse(json);

        Assert.True(document.RootElement.TryGetProperty("user", out _));
        var household = document.RootElement.GetProperty("households")[0];
        Assert.Equal("adult", household.GetProperty("role").GetString());
        Assert.Equal(selectedHouseholdId, document.RootElement.GetProperty("selectedHouseholdId").GetGuid());
    }

    [Fact]
    public void HouseholdMemberContractKeepsChildrenProfileOnly()
    {
        var response = new HouseholdMemberResponse(
            Guid.NewGuid(),
            "Child",
            HouseholdContractRoles.Child,
            null,
            true);

        var json = JsonSerializer.Serialize(response, WebJson);
        using var document = JsonDocument.Parse(json);

        Assert.Equal("child", document.RootElement.GetProperty("role").GetString());
        Assert.False(document.RootElement.TryGetProperty("userAccountId", out _));
        Assert.False(document.RootElement.TryGetProperty("email", out _));
    }

    [Fact]
    public void ProblemContractIncludesStableCodeAndTraceIdentifier()
    {
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "trace-123",
        };
        context.Request.Path = "/api/households/example";

        var problem = ApiProblems.Create(
            context,
            StatusCodes.Status403Forbidden,
            ApiProblemCodes.AdultAccessRequired,
            "Adult access is required.");

        Assert.Equal(StatusCodes.Status403Forbidden, problem.Status);
        Assert.Equal("urn:family-dashboard:problem:adult_access_required", problem.Type);
        Assert.Equal("/api/households/example", problem.Instance);
        Assert.Equal(ApiProblemCodes.AdultAccessRequired, problem.Extensions["code"]);
        Assert.Equal("trace-123", problem.Extensions["traceId"]);
    }

    [Fact]
    public void ValidationProblemContractPreservesFieldErrors()
    {
        var context = new DefaultHttpContext();
        var errors = new Dictionary<string, string[]>
        {
            ["name"] = ["Name is required."],
        };

        var problem = ApiProblems.Create(
            context,
            StatusCodes.Status400BadRequest,
            ApiProblemCodes.ValidationFailed,
            "Validation failed.",
            errors: errors);

        var validationProblem = Assert.IsType<ValidationProblemDetails>(problem);
        Assert.Equal(errors["name"], validationProblem.Errors["name"]);
        Assert.Equal(ApiProblemCodes.ValidationFailed, problem.Extensions["code"]);
    }
}

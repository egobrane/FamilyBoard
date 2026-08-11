namespace FamilyDashboard.Api.Features.Households;

public static class HouseholdContractRoles
{
    public const string Adult = "adult";
    public const string Child = "child";
}

public sealed record HouseholdSummaryResponse(
    Guid Id,
    string Name,
    Guid MemberId,
    string Role);

public sealed record CreateHouseholdRequest(
    string Name,
    string TimeZone,
    string Locale,
    string WeekStartsOn);

public sealed record HouseholdResponse(
    Guid Id,
    string Name,
    string TimeZone,
    string Locale,
    string WeekStartsOn,
    HouseholdAccessResponse Access);

public sealed record UpdateHouseholdRequest(
    string? Name,
    string? TimeZone,
    string? Locale,
    string? WeekStartsOn);

public sealed record HouseholdAccessResponse(
    Guid MemberId,
    string Role,
    bool CanAdminister);

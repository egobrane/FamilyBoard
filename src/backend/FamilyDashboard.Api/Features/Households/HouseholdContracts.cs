using FamilyDashboard.Api.Domain.Households;
using FamilyDashboard.Api.Features.HouseholdMembers;

namespace FamilyDashboard.Api.Features.Households;

public static class HouseholdContractRoles
{
    public const string Adult = "adult";
    public const string Child = "child";

    public static string FromDomain(HouseholdMemberRole role)
    {
        return role switch
        {
            HouseholdMemberRole.Adult => Adult,
            HouseholdMemberRole.Child => Child,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unsupported household role."),
        };
    }
}

public sealed record HouseholdSummaryResponse(
    Guid Id,
    string Name,
    Guid MemberId,
    string Role,
    string? AvatarColor,
    HouseholdMemberPhotoResponse? Photo);

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

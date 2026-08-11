namespace FamilyDashboard.Api.Features.HouseholdMembers;

public sealed record CreateChildMemberRequest(
    string DisplayName,
    string? AvatarColor);

public sealed record UpdateHouseholdMemberRequest(
    string? DisplayName,
    string? AvatarColor,
    bool? IsActive);

public sealed record HouseholdMemberResponse(
    Guid Id,
    string DisplayName,
    string Role,
    string? AvatarColor,
    bool IsActive);

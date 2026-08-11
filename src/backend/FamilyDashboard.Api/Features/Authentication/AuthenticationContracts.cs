using FamilyDashboard.Api.Features.Households;

namespace FamilyDashboard.Api.Features.Authentication;

public sealed record CurrentUserResponse(
    UserAccountResponse User,
    IReadOnlyList<HouseholdSummaryResponse> Households,
    Guid? SelectedHouseholdId);

public sealed record UserAccountResponse(
    Guid Id,
    string DisplayName,
    string PrimaryEmail);

using FamilyDashboard.Api.Features.Households;

namespace FamilyDashboard.Api.Features.Authentication;

public sealed record CurrentUserResponse(
    UserAccountResponse User,
    IReadOnlyList<HouseholdSummaryResponse> Households,
    Guid? SelectedHouseholdId,
    CurrentSessionResponse? Session = null);

public sealed record UserAccountResponse(
    Guid Id,
    string DisplayName,
    string PrimaryEmail);

public sealed record CurrentSessionResponse(
    DateTimeOffset ExpiresAt,
    bool IsSharedDisplay,
    DateTimeOffset? AdministrativeElevationExpiresAt);

public sealed record AntiforgeryTokenResponse(
    string RequestToken,
    string HeaderName);

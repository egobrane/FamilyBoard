namespace FamilyDashboard.Api.Features.Invitations;

public sealed record CreateInvitationRequest(string IntendedEmail);
public sealed record PrepareInvitationRequest(string Token);

public sealed record HouseholdInvitationResponse(
    Guid Id,
    Guid HouseholdId,
    string IntendedEmail,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? AcceptedAt,
    DateTimeOffset? RevokedAt);

public sealed record CreatedInvitationResponse(
    HouseholdInvitationResponse Invitation,
    string Token);

public sealed record PendingInvitationResponse(
    string HouseholdName,
    string IntendedEmailMasked,
    DateTimeOffset ExpiresAt);

public sealed record AcceptedInvitationResponse(
    AcceptedInvitationHouseholdResponse Household,
    Guid SelectedHouseholdId,
    bool ReusedExistingMembership);

public sealed record AcceptedInvitationHouseholdResponse(
    Guid Id,
    string Name,
    Guid MemberId,
    string Role);

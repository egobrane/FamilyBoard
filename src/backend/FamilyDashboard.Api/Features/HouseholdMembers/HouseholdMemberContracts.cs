namespace FamilyDashboard.Api.Features.HouseholdMembers;

public sealed record CreateChildMemberRequest(
    string DisplayName,
    string? AvatarColor);

public sealed record UpdateHouseholdMemberRequest(
    string? DisplayName,
    string? AvatarColor,
    bool? IsActive);

public sealed record UpdateHouseholdMemberPhotoPositionRequest(
    long ExpectedPhotoVersion,
    decimal FocalX,
    decimal FocalY);

public sealed record RemoveHouseholdMemberPhotoRequest(long ExpectedPhotoVersion);

public sealed record HouseholdMemberPhotoResponse(
    Guid AssetId,
    string SmallUrl,
    string MediumUrl,
    string LargeUrl,
    int PixelWidth,
    int PixelHeight,
    decimal FocalX,
    decimal FocalY);

public sealed record HouseholdMemberResponse(
    Guid Id,
    string DisplayName,
    string Role,
    string? AvatarColor,
    bool IsActive,
    long PhotoVersion,
    HouseholdMemberPhotoResponse? Photo);

internal static class HouseholdMemberPhotoContracts
{
    public static HouseholdMemberPhotoResponse? Map(Domain.Households.HouseholdMember member)
    {
        var asset = member.CurrentPhotoAsset;
        if (asset is null || asset.State != Domain.Households.HouseholdMemberPhotoAssetState.Active) return null;
        var root = $"/api/households/{member.HouseholdId}/members/{member.Id}/photo/{asset.Id}";
        return new(asset.Id, $"{root}/small", $"{root}/medium", $"{root}/large",
            asset.PixelWidth, asset.PixelHeight, member.PhotoFocalX, member.PhotoFocalY);
    }
}

using FamilyDashboard.Api.Configuration;
using FamilyDashboard.Api.Domain.Households;
using FamilyDashboard.Api.Features.Dashboard;
using FamilyDashboard.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace FamilyDashboard.Api.Features.HouseholdMembers;

internal enum HouseholdMemberPhotoMutationStatus
{
    Success,
    MemberNotFound,
    Conflict,
}

internal sealed record HouseholdMemberPhotoMutationResult(
    HouseholdMemberPhotoMutationStatus Status,
    HouseholdMemberResponse? Member = null);

internal sealed class HouseholdMemberPhotoService(
    FamilyDashboardDbContext dbContext,
    IHouseholdPhotoStore photoStore,
    PrivateHouseholdImageProcessor imageProcessor,
    IOptions<HouseholdMediaConfiguration> mediaOptions,
    ILogger<HouseholdMemberPhotoService> logger)
{
    private static readonly Action<ILogger, Guid, Exception?> LogMetadataCleanupDeferred = LoggerMessage.Define<Guid>(
        LogLevel.Warning, new EventId(4201, nameof(LogMetadataCleanupDeferred)),
        "Member photo metadata cleanup was deferred for asset {AssetId}.");
    private static readonly Action<ILogger, Exception?> LogBlobCleanupDeferred = LoggerMessage.Define(
        LogLevel.Warning, new EventId(4202, nameof(LogBlobCleanupDeferred)),
        "Retired member photo blob cleanup was deferred.");
    private static readonly Dictionary<string, int> VariantMaximumEdges = new()
    {
        ["small"] = 128,
        ["medium"] = 320,
        ["large"] = 640,
    };

    public async Task<HouseholdMemberPhotoMutationResult> UploadAsync(
        Guid householdId,
        Guid memberId,
        Guid createdByMemberId,
        long expectedPhotoVersion,
        Stream upload,
        long uploadLength,
        CancellationToken cancellationToken)
    {
        if (!mediaOptions.Value.Enabled) throw new HouseholdMediaUnavailableException();
        var member = await dbContext.HouseholdMembers.Include(value => value.CurrentPhotoAsset)
            .SingleOrDefaultAsync(value => value.HouseholdId == householdId && value.Id == memberId, cancellationToken);
        if (member is null) return new(HouseholdMemberPhotoMutationStatus.MemberNotFound);
        if (expectedPhotoVersion != member.PhotoVersion) return new(HouseholdMemberPhotoMutationStatus.Conflict);

        var processed = await imageProcessor.ProcessAsync(upload, uploadLength, VariantMaximumEdges, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var asset = new HouseholdMemberPhotoAsset
        {
            HouseholdId = householdId,
            HouseholdMemberId = memberId,
            StoragePrefix = $"members/{householdId:N}/{memberId:N}/{Guid.NewGuid():N}",
            PixelWidth = processed.PixelWidth,
            PixelHeight = processed.PixelHeight,
            TotalByteLength = processed.TotalByteLength,
            CreatedByHouseholdMemberId = createdByMemberId,
            CreatedAt = now,
        };
        dbContext.Add(asset);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            foreach (var variant in processed.Variants)
            {
                using var content = new MemoryStream(variant.Value, writable: false);
                await photoStore.WriteAsync($"{asset.StoragePrefix}/{variant.Key}.jpg", content, "image/jpeg", cancellationToken);
            }
        }
        catch
        {
            await RetireAndDeleteAsync(asset.Id, asset.StoragePrefix, CancellationToken.None);
            throw;
        }

        var oldAsset = member.CurrentPhotoAsset;
        asset.State = HouseholdMemberPhotoAssetState.Active;
        asset.ActivatedAt = now;
        member.CurrentPhotoAssetId = asset.Id;
        member.CurrentPhotoAsset = asset;
        member.PhotoVersion++;
        member.UpdatedAt = now;
        if (oldAsset is not null)
        {
            oldAsset.State = HouseholdMemberPhotoAssetState.Retired;
            oldAsset.RetiredAt = now;
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is DbUpdateConcurrencyException
            || exception is DbUpdateException { InnerException: PostgresException })
        {
            await RetireAndDeleteAsync(asset.Id, asset.StoragePrefix, CancellationToken.None);
            return new(HouseholdMemberPhotoMutationStatus.Conflict);
        }

        if (oldAsset is not null) await DeleteRetiredPrefixAsync(oldAsset.StoragePrefix, CancellationToken.None);
        return new(HouseholdMemberPhotoMutationStatus.Success, HouseholdMemberService.Map(member));
    }

    public async Task<HouseholdMemberPhotoMutationResult> UpdatePositionAsync(
        Guid householdId, Guid memberId, UpdateHouseholdMemberPhotoPositionRequest request,
        CancellationToken cancellationToken)
    {
        var member = await dbContext.HouseholdMembers.Include(value => value.CurrentPhotoAsset)
            .SingleOrDefaultAsync(value => value.HouseholdId == householdId && value.Id == memberId, cancellationToken);
        if (member is null) return new(HouseholdMemberPhotoMutationStatus.MemberNotFound);
        if (request.ExpectedPhotoVersion != member.PhotoVersion) return new(HouseholdMemberPhotoMutationStatus.Conflict);
        member.PhotoFocalX = request.FocalX;
        member.PhotoFocalY = request.FocalY;
        member.PhotoVersion++;
        member.UpdatedAt = DateTimeOffset.UtcNow;
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return new(HouseholdMemberPhotoMutationStatus.Conflict); }
        return new(HouseholdMemberPhotoMutationStatus.Success, HouseholdMemberService.Map(member));
    }

    public async Task<HouseholdMemberPhotoMutationResult> RemoveAsync(
        Guid householdId, Guid memberId, long expectedPhotoVersion, CancellationToken cancellationToken)
    {
        var member = await dbContext.HouseholdMembers.Include(value => value.CurrentPhotoAsset)
            .SingleOrDefaultAsync(value => value.HouseholdId == householdId && value.Id == memberId, cancellationToken);
        if (member is null) return new(HouseholdMemberPhotoMutationStatus.MemberNotFound);
        if (expectedPhotoVersion != member.PhotoVersion) return new(HouseholdMemberPhotoMutationStatus.Conflict);
        var asset = member.CurrentPhotoAsset;
        if (asset is null) return new(HouseholdMemberPhotoMutationStatus.Success, HouseholdMemberService.Map(member));
        var now = DateTimeOffset.UtcNow;
        member.CurrentPhotoAssetId = null;
        member.CurrentPhotoAsset = null;
        member.PhotoVersion++;
        member.UpdatedAt = now;
        asset.State = HouseholdMemberPhotoAssetState.Retired;
        asset.RetiredAt = now;
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return new(HouseholdMemberPhotoMutationStatus.Conflict); }
        await DeleteRetiredPrefixAsync(asset.StoragePrefix, CancellationToken.None);
        return new(HouseholdMemberPhotoMutationStatus.Success, HouseholdMemberService.Map(member));
    }

    public async Task<HouseholdPhotoContent?> ReadAsync(
        Guid householdId, Guid memberId, Guid assetId, string variant, CancellationToken cancellationToken)
    {
        if (!VariantMaximumEdges.ContainsKey(variant)) return null;
        var prefix = await dbContext.HouseholdMemberPhotoAssets.AsNoTracking()
            .Where(value => value.HouseholdId == householdId && value.HouseholdMemberId == memberId
                && value.Id == assetId && value.State == HouseholdMemberPhotoAssetState.Active)
            .Select(value => value.StoragePrefix).SingleOrDefaultAsync(cancellationToken);
        return prefix is null ? null : await photoStore.ReadAsync($"{prefix}/{variant}.jpg", cancellationToken);
    }

    private async Task RetireAndDeleteAsync(Guid assetId, string prefix, CancellationToken cancellationToken)
    {
        try
        {
            dbContext.ChangeTracker.Clear();
            var value = await dbContext.HouseholdMemberPhotoAssets.SingleOrDefaultAsync(item => item.Id == assetId, cancellationToken);
            if (value is not null)
            {
                value.State = HouseholdMemberPhotoAssetState.Retired;
                value.RetiredAt = DateTimeOffset.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogMetadataCleanupDeferred(logger, assetId, exception);
        }
        await DeleteRetiredPrefixAsync(prefix, cancellationToken);
    }

    private async Task DeleteRetiredPrefixAsync(string prefix, CancellationToken cancellationToken)
    {
        try { await photoStore.DeletePrefixAsync(prefix, cancellationToken); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogBlobCleanupDeferred(logger, exception);
        }
    }
}

using FamilyDashboard.Api.Configuration;
using FamilyDashboard.Api.Domain.Households;
using FamilyDashboard.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FamilyDashboard.Api.Features.Dashboard;

internal sealed class DashboardAppearanceService(
    FamilyDashboardDbContext dbContext,
    IHouseholdPhotoStore photoStore,
    PrivateHouseholdImageProcessor imageProcessor,
    IOptions<HouseholdMediaConfiguration> mediaOptions,
    ILogger<DashboardAppearanceService> logger)
{
    private static readonly Action<ILogger, Exception?> LogRetiredPhotoCleanupDeferred = LoggerMessage.Define(
        LogLevel.Warning,
        new EventId(4101, nameof(LogRetiredPhotoCleanupDeferred)),
        "Retired household photo cleanup will be retried after a later photo change.");

    private static readonly Dictionary<string, int> VariantWidths = new()
    {
        ["small"] = 720,
        ["medium"] = 1440,
        ["large"] = 2560,
    };

    public async Task<DashboardAppearanceResponse> GetAsync(Guid householdId, CancellationToken cancellationToken)
    {
        var value = await dbContext.HouseholdDashboardAppearances.AsNoTracking()
            .Include(candidate => candidate.CurrentPhotoAsset)
            .SingleOrDefaultAsync(candidate => candidate.HouseholdId == householdId, cancellationToken);
        return Map(householdId, await GetTimeZoneAsync(householdId, cancellationToken), value);
    }

    public async Task<DashboardAppearanceResponse?> UpdateAsync(
        Guid householdId, UpdateDashboardAppearanceRequest request, CancellationToken cancellationToken)
    {
        var householdExists = await dbContext.Households.AnyAsync(value => value.Id == householdId && value.IsActive, cancellationToken);
        if (!householdExists) return null;
        var value = await dbContext.HouseholdDashboardAppearances
            .Include(candidate => candidate.CurrentPhotoAsset)
            .SingleOrDefaultAsync(candidate => candidate.HouseholdId == householdId, cancellationToken);
        if (value is null)
        {
            if (request.ExpectedVersion != 1) throw new DbUpdateConcurrencyException();
            value = new HouseholdDashboardAppearance { HouseholdId = householdId };
            dbContext.Add(value);
        }
        else if (value.Version != request.ExpectedVersion) throw new DbUpdateConcurrencyException();

        value.GreetingTitle = EmptyToNull(request.GreetingTitle);
        value.GreetingMessage = EmptyToNull(request.GreetingMessage);
        value.PhotoFocalX = request.PhotoFocalX;
        value.PhotoFocalY = request.PhotoFocalY;
        value.Version++;
        value.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(householdId, await GetTimeZoneAsync(householdId, cancellationToken), value);
    }

    public async Task<DashboardAppearanceResponse> UploadAsync(
        Guid householdId, Guid createdByMemberId, Stream upload, long uploadLength, CancellationToken cancellationToken)
    {
        var options = mediaOptions.Value;
        if (!options.Enabled) throw new HouseholdMediaUnavailableException();
        var processed = await imageProcessor.ProcessAsync(upload, uploadLength, VariantWidths, cancellationToken);
        var assetId = Guid.NewGuid();
        var prefix = $"{householdId:N}/{assetId:N}";
        try
        {
            foreach (var variant in processed.Variants)
            {
                using var output = new MemoryStream(variant.Value, writable: false);
                await photoStore.WriteAsync($"{prefix}/{variant.Key}.jpg", output, "image/jpeg", cancellationToken);
            }
        }
        catch
        {
            await photoStore.DeletePrefixAsync(prefix, CancellationToken.None);
            throw;
        }

        var oldAsset = await dbContext.HouseholdPhotoAssets
            .SingleOrDefaultAsync(value => value.HouseholdId == householdId && value.RetiredAt == null, cancellationToken);
        var appearance = await dbContext.HouseholdDashboardAppearances
            .SingleOrDefaultAsync(value => value.HouseholdId == householdId, cancellationToken)
            ?? new HouseholdDashboardAppearance { HouseholdId = householdId };
        if (dbContext.Entry(appearance).State == EntityState.Detached) dbContext.Add(appearance);
        var asset = new HouseholdPhotoAsset
        {
            Id = assetId,
            HouseholdId = householdId,
            StoragePrefix = prefix,
            PixelWidth = processed.PixelWidth,
            PixelHeight = processed.PixelHeight,
            TotalByteLength = processed.TotalByteLength,
            CreatedByHouseholdMemberId = createdByMemberId,
        };
        if (oldAsset is not null) oldAsset.RetiredAt = DateTimeOffset.UtcNow;
        dbContext.Add(asset);
        appearance.CurrentPhotoAssetId = asset.Id;
        appearance.Version++;
        appearance.UpdatedAt = DateTimeOffset.UtcNow;
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch
        {
            await photoStore.DeletePrefixAsync(prefix, CancellationToken.None);
            throw;
        }
        await CleanupRetiredAssetsAsync(householdId, cancellationToken);
        appearance.CurrentPhotoAsset = asset;
        return Map(householdId, await GetTimeZoneAsync(householdId, cancellationToken), appearance);
    }

    public async Task<DashboardAppearanceResponse?> RemovePhotoAsync(Guid householdId, CancellationToken cancellationToken)
    {
        var appearance = await dbContext.HouseholdDashboardAppearances.Include(value => value.CurrentPhotoAsset)
            .SingleOrDefaultAsync(value => value.HouseholdId == householdId, cancellationToken);
        if (appearance is null) return await GetAsync(householdId, cancellationToken);
        var asset = appearance.CurrentPhotoAsset;
        appearance.CurrentPhotoAssetId = null;
        appearance.CurrentPhotoAsset = null;
        appearance.Version++;
        appearance.UpdatedAt = DateTimeOffset.UtcNow;
        if (asset is not null) asset.RetiredAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await CleanupRetiredAssetsAsync(householdId, cancellationToken);
        return Map(householdId, await GetTimeZoneAsync(householdId, cancellationToken), appearance);
    }

    public async Task<HouseholdPhotoContent?> ReadAsync(Guid householdId, Guid assetId, string variant, CancellationToken cancellationToken)
    {
        if (!VariantWidths.ContainsKey(variant)) return null;
        var asset = await dbContext.HouseholdPhotoAssets.AsNoTracking().SingleOrDefaultAsync(
            value => value.Id == assetId && value.HouseholdId == householdId && value.RetiredAt == null, cancellationToken);
        return asset is null ? null : await photoStore.ReadAsync($"{asset.StoragePrefix}/{variant}.jpg", cancellationToken);
    }

    private Task<string> GetTimeZoneAsync(Guid householdId, CancellationToken cancellationToken) =>
        dbContext.HouseholdConfigurations.Where(value => value.HouseholdId == householdId)
            .Select(value => value.TimeZone).SingleAsync(cancellationToken);

    private async Task CleanupRetiredAssetsAsync(Guid householdId, CancellationToken cancellationToken)
    {
        var prefixes = await dbContext.HouseholdPhotoAssets.AsNoTracking()
            .Where(value => value.HouseholdId == householdId && value.RetiredAt != null)
            .Select(value => value.StoragePrefix)
            .ToArrayAsync(cancellationToken);
        foreach (var prefix in prefixes)
        {
            try { await photoStore.DeletePrefixAsync(prefix, cancellationToken); }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogRetiredPhotoCleanupDeferred(logger, exception);
            }
        }
    }

    private static DashboardAppearanceResponse Map(Guid householdId, string timeZone, HouseholdDashboardAppearance? value)
    {
        var photo = value?.CurrentPhotoAsset is { RetiredAt: null } asset
            ? new DashboardPhotoResponse(asset.Id,
                $"/api/households/{householdId}/dashboard-photo/{asset.Id}/small",
                $"/api/households/{householdId}/dashboard-photo/{asset.Id}/medium",
                $"/api/households/{householdId}/dashboard-photo/{asset.Id}/large",
                asset.PixelWidth, asset.PixelHeight)
            : null;
        return new(householdId, timeZone, value?.GreetingTitle, value?.GreetingMessage,
            value?.PhotoFocalX ?? 0.5m, value?.PhotoFocalY ?? 0.5m, value?.Version ?? 1, photo);
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

internal sealed class HouseholdMediaUnavailableException : Exception;
internal sealed class InvalidHouseholdPhotoException(string message) : Exception(message);

using FamilyDashboard.Api.Domain.Households;
using FamilyDashboard.Api.Domain.Rewards;
using FamilyDashboard.Api.Features.Points;
using FamilyDashboard.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyDashboard.Api.Features.Rewards;

public sealed class RewardService(FamilyDashboardDbContext db, TimeProvider clock, PointLedgerLock ledgerLock)
{
    public async Task<RewardCatalogResponse> GetCatalogAsync(Guid householdId, CancellationToken token)
    {
        var rewards = await db.Rewards.AsNoTracking().Where(x => x.HouseholdId == householdId && x.IsActive)
            .OrderBy(x => x.PointCost).ThenBy(x => x.Title).ToListAsync(token);
        var members = await BalanceQuery(householdId).Where(x => x.IsActive).ToListAsync(token);
        return new(rewards.Select(MapReward).ToList(), members);
    }

    public async Task<IReadOnlyList<RewardResponse>> ListDefinitionsAsync(Guid householdId, CancellationToken token) =>
        await db.Rewards.AsNoTracking().Where(x => x.HouseholdId == householdId)
            .OrderByDescending(x => x.IsActive).ThenBy(x => x.Title).Select(x => MapReward(x)).ToListAsync(token);

    public async Task<RewardOperationResult<RewardResponse>> CreateDefinitionAsync(Guid householdId,
        Guid actorUserId, CreateRewardRequest request, CancellationToken token)
    {
        var existing = await db.Rewards.AsNoTracking().SingleOrDefaultAsync(x =>
            x.HouseholdId == householdId && x.ClientRequestId == request.ClientRequestId, token);
        if (existing is not null) return Same(existing, request) ? new(RewardOperationStatus.Success, MapReward(existing))
            : new(RewardOperationStatus.IdempotencyConflict);
        var actor = await AdultAsync(householdId, actorUserId, token);
        if (actor is null) return new(RewardOperationStatus.MemberNotFound);
        var now = clock.GetUtcNow();
        var reward = new Reward { HouseholdId = householdId, ClientRequestId = request.ClientRequestId,
            Title = request.Title, Description = request.Description, PointCost = request.PointCost,
            CreatedByMemberId = actor.Id, UpdatedByMemberId = actor.Id, CreatedAt = now, UpdatedAt = now };
        db.Rewards.Add(reward);
        try { await db.SaveChangesAsync(token); }
        catch (DbUpdateException) { return new(RewardOperationStatus.ConcurrencyConflict); }
        return new(RewardOperationStatus.Success, MapReward(reward));
    }

    public async Task<RewardOperationResult<RewardResponse>> UpdateDefinitionAsync(Guid householdId, Guid rewardId,
        Guid actorUserId, UpdateRewardRequest request, CancellationToken token)
    {
        var reward = await db.Rewards.SingleOrDefaultAsync(x => x.HouseholdId == householdId && x.Id == rewardId, token);
        if (reward is null) return new(RewardOperationStatus.NotFound);
        if (reward.Version != request.ExpectedVersion) return new(RewardOperationStatus.ConcurrencyConflict);
        var actor = await AdultAsync(householdId, actorUserId, token);
        if (actor is null) return new(RewardOperationStatus.MemberNotFound);
        reward.Title = request.Title; reward.Description = request.Description; reward.PointCost = request.PointCost;
        reward.UpdatedByMemberId = actor.Id; reward.UpdatedAt = clock.GetUtcNow(); reward.Version++;
        try { await db.SaveChangesAsync(token); }
        catch (DbUpdateException) { return new(RewardOperationStatus.ConcurrencyConflict); }
        return new(RewardOperationStatus.Success, MapReward(reward));
    }

    public async Task<RewardOperationResult<RewardResponse>> SetStateAsync(Guid householdId, Guid rewardId,
        Guid actorUserId, long expectedVersion, bool active, CancellationToken token)
    {
        var reward = await db.Rewards.SingleOrDefaultAsync(x => x.HouseholdId == householdId && x.Id == rewardId, token);
        if (reward is null) return new(RewardOperationStatus.NotFound);
        if (reward.Version != expectedVersion) return new(RewardOperationStatus.ConcurrencyConflict);
        var actor = await AdultAsync(householdId, actorUserId, token);
        if (actor is null) return new(RewardOperationStatus.MemberNotFound);
        reward.IsActive = active; reward.UpdatedByMemberId = actor.Id; reward.UpdatedAt = clock.GetUtcNow(); reward.Version++;
        await db.SaveChangesAsync(token);
        return new(RewardOperationStatus.Success, MapReward(reward));
    }

    public async Task<RewardOperationResult<RewardRedemptionResponse>> RequestAsync(Guid householdId,
        Guid actorUserId, Guid sessionId, CreateRewardRedemptionRequest request, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var existing = await RedemptionQuery(householdId).SingleOrDefaultAsync(x => x.ClientRequestId == request.ClientRequestId, token);
        if (existing is not null) return existing.RewardId == request.RewardId
            && (request.HouseholdMemberId is null ? existing.RequestedByUserAccountId == actorUserId
                : existing.HouseholdMemberId == request.HouseholdMemberId)
            ? new(RewardOperationStatus.Success, MapRedemption(existing)) : new(RewardOperationStatus.RedemptionIdempotencyConflict);
        var reward = await db.Rewards.SingleOrDefaultAsync(x => x.HouseholdId == householdId && x.Id == request.RewardId, token);
        if (reward is null) return new(RewardOperationStatus.NotFound);
        if (!reward.IsActive) return new(RewardOperationStatus.Inactive);
        var actor = await AdultAsync(householdId, actorUserId, token);
        var session = await db.UserSessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == sessionId && x.UserAccountId == actorUserId, token);
        if (actor is null || session is null) return new(RewardOperationStatus.MemberNotFound);
        var memberId = request.HouseholdMemberId ?? (session.IsSharedDisplay ? null : actor.Id);
        if (memberId is null) return new(RewardOperationStatus.MemberInactive);
        var member = await db.HouseholdMembers.SingleOrDefaultAsync(x => x.HouseholdId == householdId && x.Id == memberId, token);
        if (member is null) return new(RewardOperationStatus.MemberNotFound);
        if (!member.IsActive) return new(RewardOperationStatus.MemberInactive);
        await ledgerLock.AcquireAsync(householdId, member.Id, token);
        var balance = await db.PointTransactions.Where(x => x.HouseholdId == householdId && x.HouseholdMemberId == member.Id)
            .SumAsync(x => (long?)x.Amount, token) ?? 0;
        if (balance < reward.PointCost) return new(RewardOperationStatus.InsufficientPoints);
        var now = clock.GetUtcNow();
        var redemption = new RewardRedemption { HouseholdId = householdId, ClientRequestId = request.ClientRequestId,
            RewardId = reward.Id, HouseholdMemberId = member.Id, RewardTitleSnapshot = reward.Title,
            RewardDescriptionSnapshot = reward.Description, PointCostSnapshot = reward.PointCost,
            RequestedByUserAccountId = actorUserId, RequestedByMemberId = actor.Id,
            WasSharedDisplay = session.IsSharedDisplay, RequestedAt = now };
        var debit = new PointTransaction { HouseholdId = householdId, HouseholdMemberId = member.Id,
            CreatedByMemberId = actor.Id, Amount = -reward.PointCost, Type = PointTransactionType.RewardRedemption,
            Description = $"Redeemed: {reward.Title}", IdempotencyKey = $"reward-redemption:{request.ClientRequestId:N}",
            RewardRedemptionId = redemption.Id, CreatedAt = now };
        db.Add(redemption); db.Add(debit);
        try { await db.SaveChangesAsync(token); await transaction.CommitAsync(token); }
        catch (DbUpdateException) { return new(RewardOperationStatus.ConcurrencyConflict); }
        redemption.HouseholdMember = member; redemption.RequestedByMember = actor; redemption.PointTransaction = debit;
        return new(RewardOperationStatus.Success, MapRedemption(redemption));
    }

    public async Task<RewardRedemptionListResponse> ListRedemptionsAsync(Guid householdId, Guid? memberId,
        RedemptionStatus? status, int offset, int size, CancellationToken token)
    {
        var query = RedemptionQuery(householdId);
        if (memberId is not null) query = query.Where(x => x.HouseholdMemberId == memberId);
        if (status is not null) query = query.Where(x => x.Status == status);
        var rows = await query.OrderByDescending(x => x.RequestedAt).ThenByDescending(x => x.Id)
            .Skip(offset).Take(size + 1).ToListAsync(token);
        var more = rows.Count > size; if (more) rows.RemoveAt(rows.Count - 1);
        return new(rows.Select(MapRedemption).ToList(), more ? Convert.ToBase64String(BitConverter.GetBytes(offset + size)) : null);
    }

    public async Task<RewardOperationResult<RewardRedemptionResponse>> ReviewAsync(Guid householdId, Guid id,
        Guid actorUserId, ReviewRewardRedemptionRequest request, CancellationToken token)
    {
        var row = await RedemptionTracked(householdId, id, token);
        if (row is null) return new(RewardOperationStatus.RedemptionNotFound);
        if (row.Version != request.ExpectedVersion) return new(RewardOperationStatus.ConcurrencyConflict);
        if (row.Status != RedemptionStatus.Requested) return new(RewardOperationStatus.InvalidTransition);
        if (row.PointTransaction is null) return new(RewardOperationStatus.LegacyRequiresResolution);
        var actor = await AdultAsync(householdId, actorUserId, token);
        if (actor is null) return new(RewardOperationStatus.MemberNotFound);
        var now = clock.GetUtcNow(); row.ReviewedAt = now; row.ReviewedByMemberId = actor.Id; row.ReviewNote = request.Note;
        row.Status = request.Decision == "approved" ? RedemptionStatus.Approved : RedemptionStatus.Rejected; row.Version++;
        if (row.Status == RedemptionStatus.Rejected) AddRelease(row, actor, request.Note ?? "Redemption rejected", now);
        return await Save(row, token);
    }

    public async Task<RewardOperationResult<RewardRedemptionResponse>> FulfillAsync(Guid householdId, Guid id,
        Guid actorUserId, long expectedVersion, CancellationToken token)
    {
        var row = await RedemptionTracked(householdId, id, token);
        if (row is null) return new(RewardOperationStatus.RedemptionNotFound);
        if (row.Version != expectedVersion) return new(RewardOperationStatus.ConcurrencyConflict);
        if (row.Status != RedemptionStatus.Approved) return new(RewardOperationStatus.InvalidTransition);
        var actor = await AdultAsync(householdId, actorUserId, token); if (actor is null) return new(RewardOperationStatus.MemberNotFound);
        row.Status = RedemptionStatus.Fulfilled; row.FulfilledAt = clock.GetUtcNow(); row.FulfilledByMemberId = actor.Id; row.Version++;
        return await Save(row, token);
    }

    public async Task<RewardOperationResult<RewardRedemptionResponse>> CancelAsync(Guid householdId, Guid id,
        Guid actorUserId, long expectedVersion, string reason, CancellationToken token)
    {
        var row = await RedemptionTracked(householdId, id, token);
        if (row is null) return new(RewardOperationStatus.RedemptionNotFound);
        if (row.Version != expectedVersion) return new(RewardOperationStatus.ConcurrencyConflict);
        if (row.Status is not (RedemptionStatus.Requested or RedemptionStatus.Approved)) return new(RewardOperationStatus.InvalidTransition);
        var actor = await AdultAsync(householdId, actorUserId, token); if (actor is null) return new(RewardOperationStatus.MemberNotFound);
        var now = clock.GetUtcNow(); row.Status = RedemptionStatus.Cancelled; row.CancelledAt = now;
        row.CancelledByMemberId = actor.Id; row.CancellationReason = reason; row.Version++;
        if (row.PointTransaction is not null) AddRelease(row, actor, reason, now);
        return await Save(row, token);
    }

    private void AddRelease(RewardRedemption row, HouseholdMember actor, string reason, DateTimeOffset now) =>
        db.PointTransactions.Add(new PointTransaction { HouseholdId = row.HouseholdId,
            HouseholdMemberId = row.HouseholdMemberId, CreatedByMemberId = actor.Id,
            Amount = row.PointCostSnapshot, Type = PointTransactionType.Reversal,
            Description = reason, IdempotencyKey = $"reward-release:{row.Id:N}",
            ReversesPointTransactionId = row.PointTransaction!.Id, CreatedAt = now });

    private async Task<RewardOperationResult<RewardRedemptionResponse>> Save(RewardRedemption row, CancellationToken token)
    {
        try { await db.SaveChangesAsync(token); }
        catch (DbUpdateException) { return new(RewardOperationStatus.ConcurrencyConflict); }
        return new(RewardOperationStatus.Success, MapRedemption(await RedemptionQuery(row.HouseholdId).SingleAsync(x => x.Id == row.Id, token)));
    }

    private Task<RewardRedemption?> RedemptionTracked(Guid householdId, Guid id, CancellationToken token) =>
        db.RewardRedemptions.Include(x => x.PointTransaction).SingleOrDefaultAsync(x => x.HouseholdId == householdId && x.Id == id, token);
    private IQueryable<RewardRedemption> RedemptionQuery(Guid householdId) => db.RewardRedemptions.AsNoTracking()
        .Include(x => x.HouseholdMember).Include(x => x.RequestedByMember).Include(x => x.ReviewedByMember)
        .Include(x => x.FulfilledByMember).Include(x => x.CancelledByMember).Include(x => x.PointTransaction)
        .Where(x => x.HouseholdId == householdId);
    private IQueryable<PointMemberBalanceResponse> BalanceQuery(Guid householdId) => db.HouseholdMembers.AsNoTracking()
        .Where(m => m.HouseholdId == householdId).Select(m => new PointMemberBalanceResponse(m.Id, m.DisplayName,
            m.Role == HouseholdMemberRole.Adult ? "adult" : "child", m.AvatarColor, m.IsActive,
            db.PointTransactions.Where(t => t.HouseholdId == householdId && t.HouseholdMemberId == m.Id).Sum(t => (long?)t.Amount) ?? 0));
    private Task<HouseholdMember?> AdultAsync(Guid householdId, Guid userId, CancellationToken token) => db.HouseholdMemberships
        .Where(x => x.HouseholdId == householdId && x.UserAccountId == userId && x.HouseholdMember.IsActive)
        .Select(x => x.HouseholdMember).SingleOrDefaultAsync(token);
    private static bool Same(Reward reward, CreateRewardRequest request) => reward.Title == request.Title
        && reward.Description == request.Description && reward.PointCost == request.PointCost;
    private static RewardResponse MapReward(Reward x) => new(x.Id, x.Title, x.Description, x.PointCost,
        x.IsActive, x.Version, x.CreatedAt, x.UpdatedAt);
    private static PointMemberResponse Member(HouseholdMember x) => new(x.Id, x.DisplayName,
        x.Role.ToString().ToLowerInvariant(), x.AvatarColor, x.IsActive);
    private static RewardRedemptionResponse MapRedemption(RewardRedemption x) => new(x.Id, x.RewardId,
        x.RewardTitleSnapshot, x.RewardDescriptionSnapshot, x.PointCostSnapshot, Member(x.HouseholdMember),
        x.Status.ToString()[..1].ToLowerInvariant() + x.Status.ToString()[1..],
        x.RequestedByMember is null ? null : Member(x.RequestedByMember), x.WasSharedDisplay, x.RequestedAt,
        x.ReviewedByMember is null ? null : Member(x.ReviewedByMember), x.ReviewedAt, x.ReviewNote,
        x.FulfilledByMember is null ? null : Member(x.FulfilledByMember), x.FulfilledAt,
        x.CancelledByMember is null ? null : Member(x.CancelledByMember), x.CancelledAt, x.CancellationReason, x.Version);
}

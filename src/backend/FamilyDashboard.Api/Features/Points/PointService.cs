using FamilyDashboard.Api.Domain.Households;
using FamilyDashboard.Api.Domain.Rewards;
using FamilyDashboard.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyDashboard.Api.Features.Points;

public sealed class PointService(FamilyDashboardDbContext dbContext, TimeProvider timeProvider)
{
    public async Task<HouseholdPointSummaryResponse> GetSummaryAsync(
        Guid householdId, CancellationToken cancellationToken)
    {
        var members = await dbContext.HouseholdMembers.AsNoTracking()
            .Where(member => member.HouseholdId == householdId)
            .OrderByDescending(member => member.IsActive)
            .ThenBy(member => member.DisplayName)
            .ToListAsync(cancellationToken);
        var balances = await dbContext.PointTransactions.AsNoTracking()
            .Where(transaction => transaction.HouseholdId == householdId)
            .GroupBy(transaction => transaction.HouseholdMemberId)
            .Select(group => new { MemberId = group.Key, Balance = group.Sum(item => (long)item.Amount) })
            .ToDictionaryAsync(item => item.MemberId, item => item.Balance, cancellationToken);
        var recent = await TransactionQuery(householdId)
            .OrderByDescending(transaction => transaction.CreatedAt)
            .ThenByDescending(transaction => transaction.Id)
            .Take(10)
            .ToListAsync(cancellationToken);
        var mappedMembers = members.Select(member => new PointMemberBalanceResponse(
            member.Id, member.DisplayName, Role(member), member.AvatarColor, member.IsActive,
            balances.GetValueOrDefault(member.Id))).ToList();
        return new(mappedMembers.Sum(member => member.Balance), mappedMembers,
            recent.Select(MapTransaction).ToList());
    }

    public async Task<PointTransactionListResponse> ListAsync(Guid householdId, Guid? memberId,
        int offset, int pageSize, CancellationToken cancellationToken)
    {
        var query = TransactionQuery(householdId);
        if (memberId is not null) query = query.Where(item => item.HouseholdMemberId == memberId);
        var rows = await query.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
            .Skip(offset).Take(pageSize + 1).ToListAsync(cancellationToken);
        var hasMore = rows.Count > pageSize;
        if (hasMore) rows.RemoveAt(rows.Count - 1);
        return new(rows.Select(MapTransaction).ToList(),
            hasMore ? Convert.ToBase64String(BitConverter.GetBytes(offset + pageSize)) : null);
    }

    public async Task<PointOperationResult<PointTransactionResponse>> AdjustAsync(
        Guid householdId, Guid actorUserAccountId, CreatePointAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        var key = $"adjustment:{request.ClientRequestId:N}";
        var existing = await TransactionQuery(householdId)
            .SingleOrDefaultAsync(item => item.IdempotencyKey == key, cancellationToken);
        if (existing is not null)
            return existing.Type == PointTransactionType.Adjustment
                && existing.HouseholdMemberId == request.HouseholdMemberId
                && existing.Amount == request.Amount && existing.Description == request.Reason
                ? new(PointOperationStatus.Success, MapTransaction(existing))
                : new(PointOperationStatus.IdempotencyConflict);
        var member = await dbContext.HouseholdMembers.SingleOrDefaultAsync(item =>
            item.HouseholdId == householdId && item.Id == request.HouseholdMemberId, cancellationToken);
        if (member is null) return new(PointOperationStatus.MemberNotFound);
        var actor = await ResolveAdultMemberAsync(householdId, actorUserAccountId, cancellationToken);
        if (actor is null) return new(PointOperationStatus.MemberNotFound);
        var transaction = new PointTransaction
        {
            HouseholdId = householdId,
            HouseholdMemberId = member.Id,
            CreatedByMemberId = actor.Id,
            Amount = request.Amount,
            Type = PointTransactionType.Adjustment,
            Description = request.Reason,
            IdempotencyKey = key,
            CreatedAt = timeProvider.GetUtcNow(),
            HouseholdMember = member,
            CreatedByMember = actor,
        };
        dbContext.PointTransactions.Add(transaction);
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException) { return new(PointOperationStatus.ConcurrencyConflict); }
        return new(PointOperationStatus.Success, MapTransaction(transaction));
    }

    public async Task<PointOperationResult<PointTransactionResponse>> ReverseAsync(
        Guid householdId, Guid transactionId, Guid actorUserAccountId,
        ReversePointTransactionRequest request, CancellationToken cancellationToken)
    {
        var key = $"reversal:{request.ClientRequestId:N}";
        var existing = await TransactionQuery(householdId)
            .SingleOrDefaultAsync(item => item.IdempotencyKey == key, cancellationToken);
        if (existing is not null)
            return existing.Type == PointTransactionType.Reversal
                && existing.ReversesPointTransactionId == transactionId
                && existing.Description == request.Reason
                ? new(PointOperationStatus.Success, MapTransaction(existing))
                : new(PointOperationStatus.IdempotencyConflict);
        var original = await TransactionQuery(householdId)
            .SingleOrDefaultAsync(item => item.Id == transactionId, cancellationToken);
        if (original is null) return new(PointOperationStatus.TransactionNotFound);
        if (original.Type is not (PointTransactionType.ChoreCompletion or PointTransactionType.Adjustment))
            return new(PointOperationStatus.NotReversible);
        if (original.ReversalTransaction is not null) return new(PointOperationStatus.AlreadyReversed);
        var actor = await ResolveAdultMemberAsync(householdId, actorUserAccountId, cancellationToken);
        if (actor is null) return new(PointOperationStatus.MemberNotFound);
        var reversal = new PointTransaction
        {
            HouseholdId = householdId,
            HouseholdMemberId = original.HouseholdMemberId,
            CreatedByMemberId = actor.Id,
            Amount = -original.Amount,
            Type = PointTransactionType.Reversal,
            Description = request.Reason,
            IdempotencyKey = key,
            ReversesPointTransactionId = original.Id,
            CreatedAt = timeProvider.GetUtcNow(),
        };
        dbContext.PointTransactions.Add(reversal);
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException) { return new(PointOperationStatus.ConcurrencyConflict); }
        reversal.HouseholdMember = original.HouseholdMember;
        reversal.CreatedByMember = actor;
        return new(PointOperationStatus.Success, MapTransaction(reversal));
    }

    private IQueryable<PointTransaction> TransactionQuery(Guid householdId) =>
        dbContext.PointTransactions.AsNoTracking()
            .Include(item => item.HouseholdMember)
            .Include(item => item.CreatedByMember)
            .Include(item => item.ReversalTransaction)
            .Where(item => item.HouseholdId == householdId);

    private async Task<HouseholdMember?> ResolveAdultMemberAsync(Guid householdId,
        Guid userAccountId, CancellationToken cancellationToken) =>
        await dbContext.HouseholdMemberships.Where(item => item.HouseholdId == householdId
                && item.UserAccountId == userAccountId && item.HouseholdMember.IsActive)
            .Select(item => item.HouseholdMember).SingleOrDefaultAsync(cancellationToken);

    private static string Role(HouseholdMember member) =>
        member.Role.ToString().ToLowerInvariant();

    private static PointMemberResponse MapMember(HouseholdMember member) =>
        new(member.Id, member.DisplayName, Role(member), member.AvatarColor, member.IsActive);

    private static PointTransactionResponse MapTransaction(PointTransaction item) =>
        new(item.Id, MapMember(item.HouseholdMember), item.Amount,
            item.Type.ToString()[..1].ToLowerInvariant() + item.Type.ToString()[1..],
            item.Description, item.ChoreCompletionId, item.ReversesPointTransactionId,
            item.CreatedByMember is null ? null : MapMember(item.CreatedByMember), item.CreatedAt,
            item.ReversalTransaction is not null);
}

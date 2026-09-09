using FamilyDashboard.Api.Domain.Households;
using FamilyDashboard.Api.Domain.Rewards;
using FamilyDashboard.Api.Persistence;
using FamilyDashboard.Api.Features.HouseholdMembers;
using Microsoft.EntityFrameworkCore;

namespace FamilyDashboard.Api.Features.Points;

public sealed class PointService(FamilyDashboardDbContext dbContext, TimeProvider timeProvider,
    PointLedgerLock ledgerLock)
{
    public async Task<HouseholdPointSummaryResponse> GetSummaryAsync(
        Guid householdId, CancellationToken cancellationToken)
    {
        var members = await dbContext.HouseholdMembers.AsNoTracking()
            .Include(member => member.CurrentPhotoAsset)
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
            balances.GetValueOrDefault(member.Id), HouseholdMemberPhotoContracts.Map(member))).ToList();
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

    public async Task<IReadOnlyList<PointDeductionDefinitionResponse>> ListDeductionDefinitionsAsync(
        Guid householdId, CancellationToken cancellationToken) =>
        await dbContext.PointDeductionDefinitions.AsNoTracking()
            .Where(item => item.HouseholdId == householdId)
            .OrderByDescending(item => item.IsActive)
            .ThenBy(item => item.Title)
            .Select(item => MapDeductionDefinition(item))
            .ToListAsync(cancellationToken);

    public async Task<PointOperationResult<PointDeductionDefinitionResponse>>
        CreateDeductionDefinitionAsync(Guid householdId, Guid actorUserAccountId,
            CreatePointDeductionDefinitionRequest request, CancellationToken cancellationToken)
    {
        var existing = await dbContext.PointDeductionDefinitions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.HouseholdId == householdId
                && item.ClientRequestId == request.ClientRequestId, cancellationToken);
        if (existing is not null)
            return SameDefinition(existing, request)
                ? new(PointOperationStatus.Success, MapDeductionDefinition(existing))
                : new(PointOperationStatus.IdempotencyConflict);
        var actor = await ResolveAdultMemberAsync(householdId, actorUserAccountId, cancellationToken);
        if (actor is null) return new(PointOperationStatus.MemberNotFound);
        var now = timeProvider.GetUtcNow();
        var definition = new PointDeductionDefinition
        {
            HouseholdId = householdId,
            ClientRequestId = request.ClientRequestId,
            Title = request.Title,
            DeductionAmount = request.DeductionAmount,
            CreatedByMemberId = actor.Id,
            UpdatedByMemberId = actor.Id,
            CreatedAt = now,
            UpdatedAt = now,
        };
        dbContext.PointDeductionDefinitions.Add(definition);
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException) { return new(PointOperationStatus.ConcurrencyConflict); }
        return new(PointOperationStatus.Success, MapDeductionDefinition(definition));
    }

    public async Task<PointOperationResult<PointDeductionDefinitionResponse>>
        UpdateDeductionDefinitionAsync(Guid householdId, Guid definitionId,
            Guid actorUserAccountId, UpdatePointDeductionDefinitionRequest request,
            CancellationToken cancellationToken)
    {
        var definition = await dbContext.PointDeductionDefinitions.SingleOrDefaultAsync(item =>
            item.HouseholdId == householdId && item.Id == definitionId, cancellationToken);
        if (definition is null) return new(PointOperationStatus.DeductionDefinitionNotFound);
        if (definition.Version != request.ExpectedVersion)
            return new(PointOperationStatus.ConcurrencyConflict);
        var actor = await ResolveAdultMemberAsync(householdId, actorUserAccountId, cancellationToken);
        if (actor is null) return new(PointOperationStatus.MemberNotFound);
        definition.Title = request.Title;
        definition.DeductionAmount = request.DeductionAmount;
        definition.UpdatedByMemberId = actor.Id;
        definition.UpdatedAt = timeProvider.GetUtcNow();
        definition.Version++;
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException) { return new(PointOperationStatus.ConcurrencyConflict); }
        return new(PointOperationStatus.Success, MapDeductionDefinition(definition));
    }

    public async Task<PointOperationResult<PointDeductionDefinitionResponse>>
        SetDeductionDefinitionStateAsync(Guid householdId, Guid definitionId,
            Guid actorUserAccountId, long expectedVersion, bool active,
            CancellationToken cancellationToken)
    {
        var definition = await dbContext.PointDeductionDefinitions.SingleOrDefaultAsync(item =>
            item.HouseholdId == householdId && item.Id == definitionId, cancellationToken);
        if (definition is null) return new(PointOperationStatus.DeductionDefinitionNotFound);
        if (definition.Version != expectedVersion)
            return new(PointOperationStatus.ConcurrencyConflict);
        var actor = await ResolveAdultMemberAsync(householdId, actorUserAccountId, cancellationToken);
        if (actor is null) return new(PointOperationStatus.MemberNotFound);
        definition.IsActive = active;
        definition.UpdatedByMemberId = actor.Id;
        definition.UpdatedAt = timeProvider.GetUtcNow();
        definition.Version++;
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException) { return new(PointOperationStatus.ConcurrencyConflict); }
        return new(PointOperationStatus.Success, MapDeductionDefinition(definition));
    }

    public async Task<PointOperationResult<PointTransactionResponse>> ApplyDeductionAsync(
        Guid householdId, Guid definitionId, Guid actorUserAccountId,
        ApplyPointDeductionRequest request, CancellationToken cancellationToken)
    {
        await using var databaseTransaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await ledgerLock.AcquireAsync(householdId, request.HouseholdMemberId, cancellationToken);
        var key = $"deduction:{request.ClientRequestId:N}";
        var existing = await TransactionQuery(householdId)
            .SingleOrDefaultAsync(item => item.IdempotencyKey == key, cancellationToken);
        if (existing is not null)
            return existing.Type == PointTransactionType.Adjustment
                && existing.HouseholdMemberId == request.HouseholdMemberId
                && existing.PointDeductionDefinitionId == definitionId
                ? new(PointOperationStatus.Success, MapTransaction(existing))
                : new(PointOperationStatus.IdempotencyConflict);
        var definition = await dbContext.PointDeductionDefinitions.SingleOrDefaultAsync(item =>
            item.HouseholdId == householdId && item.Id == definitionId, cancellationToken);
        if (definition is null) return new(PointOperationStatus.DeductionDefinitionNotFound);
        if (!definition.IsActive) return new(PointOperationStatus.DeductionDefinitionInactive);
        if (definition.Version != request.ExpectedDefinitionVersion)
            return new(PointOperationStatus.ConcurrencyConflict);
        var member = await dbContext.HouseholdMembers.SingleOrDefaultAsync(item =>
            item.HouseholdId == householdId && item.Id == request.HouseholdMemberId,
            cancellationToken);
        if (member is null) return new(PointOperationStatus.MemberNotFound);
        if (!member.IsActive) return new(PointOperationStatus.MemberInactive);
        var actor = await ResolveAdultMemberAsync(householdId, actorUserAccountId, cancellationToken);
        if (actor is null) return new(PointOperationStatus.MemberNotFound);
        var transaction = new PointTransaction
        {
            HouseholdId = householdId,
            HouseholdMemberId = member.Id,
            CreatedByMemberId = actor.Id,
            Amount = -definition.DeductionAmount,
            Type = PointTransactionType.Adjustment,
            Description = definition.Title,
            IdempotencyKey = key,
            PointDeductionDefinitionId = definition.Id,
            CreatedAt = timeProvider.GetUtcNow(),
            HouseholdMember = member,
            CreatedByMember = actor,
            PointDeductionDefinition = definition,
        };
        dbContext.PointTransactions.Add(transaction);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await databaseTransaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException) { return new(PointOperationStatus.ConcurrencyConflict); }
        return new(PointOperationStatus.Success, MapTransaction(transaction));
    }

    public async Task<PointOperationResult<PointTransactionResponse>> AdjustAsync(
        Guid householdId, Guid actorUserAccountId, CreatePointAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        await using var databaseTransaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await ledgerLock.AcquireAsync(householdId, request.HouseholdMemberId, cancellationToken);
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
        try { await dbContext.SaveChangesAsync(cancellationToken); await databaseTransaction.CommitAsync(cancellationToken); }
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
        await using var databaseTransaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await ledgerLock.AcquireAsync(householdId, original.HouseholdMemberId, cancellationToken);
        existing = await TransactionQuery(householdId)
            .SingleOrDefaultAsync(item => item.IdempotencyKey == key, cancellationToken);
        if (existing is not null)
            return existing.Type == PointTransactionType.Reversal
                && existing.ReversesPointTransactionId == transactionId
                && existing.Description == request.Reason
                ? new(PointOperationStatus.Success, MapTransaction(existing))
                : new(PointOperationStatus.IdempotencyConflict);
        original = await TransactionQuery(householdId)
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
        try { await dbContext.SaveChangesAsync(cancellationToken); await databaseTransaction.CommitAsync(cancellationToken); }
        catch (DbUpdateException) { return new(PointOperationStatus.ConcurrencyConflict); }
        reversal.HouseholdMember = original.HouseholdMember;
        reversal.CreatedByMember = actor;
        return new(PointOperationStatus.Success, MapTransaction(reversal));
    }

    private IQueryable<PointTransaction> TransactionQuery(Guid householdId) =>
        dbContext.PointTransactions.AsNoTracking()
            .Include(item => item.HouseholdMember).ThenInclude(item => item.CurrentPhotoAsset)
            .Include(item => item.CreatedByMember).ThenInclude(item => item!.CurrentPhotoAsset)
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
        new(member.Id, member.DisplayName, Role(member), member.AvatarColor, member.IsActive,
            HouseholdMemberPhotoContracts.Map(member));

    private static PointTransactionResponse MapTransaction(PointTransaction item) =>
        new(item.Id, MapMember(item.HouseholdMember), item.Amount,
            item.Type.ToString()[..1].ToLowerInvariant() + item.Type.ToString()[1..],
            item.Description, item.ChoreCompletionId, item.RewardRedemptionId,
            item.PointDeductionDefinitionId, item.ReversesPointTransactionId,
            item.CreatedByMember is null ? null : MapMember(item.CreatedByMember), item.CreatedAt,
            item.ReversalTransaction is not null);

    private static PointDeductionDefinitionResponse MapDeductionDefinition(
        PointDeductionDefinition item) => new(item.Id, item.Title, item.DeductionAmount,
            item.IsActive, item.Version, item.CreatedAt, item.UpdatedAt);

    private static bool SameDefinition(PointDeductionDefinition definition,
        CreatePointDeductionDefinitionRequest request) =>
        definition.Title == request.Title && definition.DeductionAmount == request.DeductionAmount;
}

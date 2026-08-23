using FamilyDashboard.Api.Domain.Chores;
using FamilyDashboard.Api.Domain.Households;
using FamilyDashboard.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyDashboard.Api.Features.Chores;

public sealed class ChoreService(
    FamilyDashboardDbContext dbContext,
    ChoreDueTimeService dueTimeService,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<ChoreParticipantResponse>> ListParticipantsAsync(
        Guid householdId, CancellationToken cancellationToken) =>
        await dbContext.HouseholdMembers.AsNoTracking()
            .Where(member => member.HouseholdId == householdId && member.IsActive)
            .OrderBy(member => member.DisplayName)
            .Select(member => new ChoreParticipantResponse(member.Id, member.DisplayName,
                member.Role.ToString().ToLowerInvariant(), member.AvatarColor))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ChoreDefinitionResponse>> ListDefinitionsAsync(
        Guid householdId, bool includeInactive, CancellationToken cancellationToken) =>
        await dbContext.ChoreDefinitions.AsNoTracking()
            .Where(definition => definition.HouseholdId == householdId
                && (includeInactive || definition.IsActive))
            .OrderByDescending(definition => definition.IsActive)
            .ThenBy(definition => definition.Title)
            .Select(definition => MapDefinition(definition))
            .ToListAsync(cancellationToken);

    public async Task<ChoreDashboardResponse> GetDashboardAsync(
        Guid householdId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var assignments = await AssignmentQuery(householdId)
            .Where(assignment => assignment.Status == ChoreAssignmentStatus.Pending
                || assignment.Status == ChoreAssignmentStatus.AwaitingReview)
            .OrderBy(assignment => assignment.DueAt)
            .ThenBy(assignment => assignment.Id)
            .Take(40)
            .ToListAsync(cancellationToken);
        var mapped = assignments.Select(assignment => MapAssignment(assignment, now)).ToList();
        var zone = await dbContext.HouseholdConfigurations.AsNoTracking()
            .Where(item => item.HouseholdId == householdId).Select(item => item.TimeZone)
            .SingleAsync(cancellationToken);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now,
            TimeZoneInfo.FindSystemTimeZoneById(zone)).Date);
        return new ChoreDashboardResponse(
            mapped.Where(item => item.IsOverdue).Take(5).ToList(),
            mapped.Where(item => !item.IsOverdue && item.DueLocalDate == today).Take(5).ToList(),
            mapped.Where(item => !item.IsOverdue && item.DueLocalDate > today).Take(3).ToList(),
            mapped.Count(item => item.Status == "awaitingReview"));
    }

    public async Task<ChoreListResponse> ListAssignmentsAsync(Guid householdId, string view,
        Guid? memberId, int offset, int pageSize, CancellationToken cancellationToken)
    {
        var query = AssignmentQuery(householdId);
        query = view.Equals("history", StringComparison.OrdinalIgnoreCase)
            ? query.Where(item => item.Status == ChoreAssignmentStatus.Completed || item.Status == ChoreAssignmentStatus.Skipped)
            : query.Where(item => item.Status == ChoreAssignmentStatus.Pending || item.Status == ChoreAssignmentStatus.AwaitingReview);
        if (memberId is not null) query = query.Where(item => item.HouseholdMemberId == memberId);
        query = view.Equals("history", StringComparison.OrdinalIgnoreCase)
            ? query.OrderByDescending(item => item.UpdatedAt).ThenBy(item => item.Id)
            : query.OrderBy(item => item.DueAt).ThenBy(item => item.Id);
        var rows = await query.Skip(offset).Take(pageSize + 1).ToListAsync(cancellationToken);
        var hasMore = rows.Count > pageSize;
        if (hasMore) rows.RemoveAt(rows.Count - 1);
        var now = timeProvider.GetUtcNow();
        return new ChoreListResponse(rows.Select(row => MapAssignment(row, now)).ToList(),
            hasMore ? Convert.ToBase64String(BitConverter.GetBytes(offset + pageSize)) : null);
    }

    public async Task<ChoreOperationResult<ChoreDefinitionResponse>> CreateDefinitionAsync(
        Guid householdId, (Guid ClientRequestId, string Title, string? Description) values,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.ChoreDefinitions.AsNoTracking().SingleOrDefaultAsync(
            item => item.HouseholdId == householdId && item.ClientRequestId == values.ClientRequestId,
            cancellationToken);
        if (existing is not null)
            return existing.Title == values.Title && existing.Description == values.Description
                ? new(ChoreOperationStatus.Success, MapDefinition(existing))
                : new(ChoreOperationStatus.IdempotencyConflict);
        var now = timeProvider.GetUtcNow();
        var definition = new ChoreDefinition
        {
            HouseholdId = householdId,
            ClientRequestId = values.ClientRequestId,
            Title = values.Title,
            Description = values.Description,
            DefaultPointValue = 0,
            CreatedAt = now,
            UpdatedAt = now,
        };
        dbContext.ChoreDefinitions.Add(definition);
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException) { return new(ChoreOperationStatus.ConcurrencyConflict); }
        return new(ChoreOperationStatus.Success, MapDefinition(definition));
    }

    public async Task<ChoreOperationResult<ChoreDefinitionResponse>> UpdateDefinitionAsync(
        Guid householdId, Guid definitionId, long expectedVersion, string title, string? description,
        bool? active, CancellationToken cancellationToken)
    {
        var definition = await dbContext.ChoreDefinitions.SingleOrDefaultAsync(
            item => item.HouseholdId == householdId && item.Id == definitionId, cancellationToken);
        if (definition is null) return new(ChoreOperationStatus.NotFound);
        if (definition.Version != expectedVersion) return new(ChoreOperationStatus.ConcurrencyConflict);
        definition.Title = title;
        definition.Description = description;
        if (active is not null) definition.IsActive = active.Value;
        definition.UpdatedAt = timeProvider.GetUtcNow();
        definition.Version++;
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return new(ChoreOperationStatus.ConcurrencyConflict); }
        return new(ChoreOperationStatus.Success, MapDefinition(definition));
    }

    public async Task<ChoreOperationResult<ChoreDefinitionResponse>> SetDefinitionStateAsync(
        Guid householdId, Guid definitionId, long expectedVersion, bool active,
        CancellationToken cancellationToken)
    {
        var definition = await dbContext.ChoreDefinitions.SingleOrDefaultAsync(
            item => item.HouseholdId == householdId && item.Id == definitionId, cancellationToken);
        if (definition is null) return new(ChoreOperationStatus.NotFound);
        if (definition.Version != expectedVersion) return new(ChoreOperationStatus.ConcurrencyConflict);
        if (definition.IsActive == active) return new(ChoreOperationStatus.Success, MapDefinition(definition));
        definition.IsActive = active;
        var now = timeProvider.GetUtcNow();
        definition.UpdatedAt = now;
        definition.Version++;
        if (!active)
        {
            var schedules = await dbContext.ChoreSchedules.Where(item =>
                item.HouseholdId == householdId && item.ChoreDefinitionId == definitionId
                && item.Status == ChoreScheduleStatus.Active).ToListAsync(cancellationToken);
            foreach (var schedule in schedules)
            {
                schedule.Status = ChoreScheduleStatus.Blocked;
                schedule.BlockedReason = "definitionInactive";
                schedule.UpdatedAt = now;
                schedule.Version++;
            }
        }
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return new(ChoreOperationStatus.ConcurrencyConflict); }
        return new(ChoreOperationStatus.Success, MapDefinition(definition));
    }

    public async Task<ChoreOperationResult<ChoreAssignmentResponse>> CreateAssignmentAsync(
        Guid householdId, Guid actorUserAccountId, CreateChoreAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        var existing = await AssignmentQuery(householdId).SingleOrDefaultAsync(
            item => item.ClientRequestId == request.ClientRequestId, cancellationToken);
        if (existing is not null)
            return existing.ChoreDefinitionId == request.ChoreDefinitionId
                && existing.HouseholdMemberId == request.AssignedMemberId
                && existing.DueLocalDate == request.DueLocalDate
                && existing.DueLocalTime == request.DueLocalTime
                ? new(ChoreOperationStatus.Success, MapAssignment(existing, timeProvider.GetUtcNow()))
                : new(ChoreOperationStatus.IdempotencyConflict);
        var definition = await dbContext.ChoreDefinitions.SingleOrDefaultAsync(
            item => item.HouseholdId == householdId && item.Id == request.ChoreDefinitionId,
            cancellationToken);
        if (definition is null) return new(ChoreOperationStatus.NotFound);
        if (!definition.IsActive) return new(ChoreOperationStatus.DefinitionInactive);
        var assigned = await dbContext.HouseholdMembers.SingleOrDefaultAsync(
            item => item.HouseholdId == householdId && item.Id == request.AssignedMemberId,
            cancellationToken);
        if (assigned is null) return new(ChoreOperationStatus.NotFound);
        if (!assigned.IsActive) return new(ChoreOperationStatus.MemberInactive);
        var actor = await ResolveAdultMemberAsync(householdId, actorUserAccountId, cancellationToken);
        if (actor is null) return new(ChoreOperationStatus.NotFound);
        var zone = await dbContext.HouseholdConfigurations.AsNoTracking()
            .Where(item => item.HouseholdId == householdId).Select(item => item.TimeZone)
            .SingleAsync(cancellationToken);
        if (!dueTimeService.TryResolve(request.DueLocalDate, request.DueLocalTime, zone,
                out var dueAt, out _)) return new(ChoreOperationStatus.InvalidDueDate);
        var now = timeProvider.GetUtcNow();
        var assignment = new ChoreAssignment
        {
            HouseholdId = householdId,
            ChoreDefinitionId = definition.Id,
            HouseholdMemberId = assigned.Id,
            CreatedByMemberId = actor.Id,
            ClientRequestId = request.ClientRequestId,
            TitleSnapshot = definition.Title,
            DescriptionSnapshot = definition.Description,
            DueAt = dueAt,
            DueLocalDate = request.DueLocalDate,
            DueLocalTime = request.DueLocalTime,
            DueTimeZone = zone,
            DueHasExplicitTime = request.DueLocalTime is not null,
            CreatedAt = now,
            UpdatedAt = now,
            ChoreDefinition = definition,
            HouseholdMember = assigned,
            CreatedByMember = actor,
        };
        dbContext.ChoreAssignments.Add(assignment);
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException) { return new(ChoreOperationStatus.ConcurrencyConflict); }
        return new(ChoreOperationStatus.Success, MapAssignment(assignment, now));
    }

    public async Task<ChoreOperationResult<ChoreCompletionResponse>> CompleteAsync(
        Guid householdId, Guid assignmentId, Guid actorUserAccountId, Guid sessionId,
        CompleteChoreRequest request, CancellationToken cancellationToken)
    {
        var existing = await dbContext.ChoreCompletions.AsNoTracking()
            .Include(item => item.CompletedByMember).Include(item => item.ReviewedByMember)
            .SingleOrDefaultAsync(item => item.HouseholdId == householdId
                && item.ClientRequestId == request.ClientRequestId, cancellationToken);
        if (existing is not null)
            return existing.ChoreAssignmentId == assignmentId
                && (request.CompletedByMemberId is null
                    || existing.CompletedByMemberId == request.CompletedByMemberId)
                ? new(ChoreOperationStatus.Success, MapCompletion(existing))
                : new(ChoreOperationStatus.IdempotencyConflict);
        var assignment = await dbContext.ChoreAssignments.Include(item => item.HouseholdMember)
            .SingleOrDefaultAsync(item => item.HouseholdId == householdId && item.Id == assignmentId,
                cancellationToken);
        if (assignment is null) return new(ChoreOperationStatus.NotFound);
        if (assignment.Version != request.ExpectedAssignmentVersion) return new(ChoreOperationStatus.ConcurrencyConflict);
        if (assignment.Status == ChoreAssignmentStatus.AwaitingReview) return new(ChoreOperationStatus.PendingReview);
        if (assignment.Status != ChoreAssignmentStatus.Pending) return new(ChoreOperationStatus.NotActionable);
        var session = await dbContext.UserSessions.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == sessionId && item.UserAccountId == actorUserAccountId && item.RevokedAt == null,
            cancellationToken);
        if (session is null) return new(ChoreOperationStatus.NotActionable);
        var memberId = request.CompletedByMemberId;
        if (session.IsSharedDisplay && memberId is null) return new(ChoreOperationStatus.MemberInactive);
        if (memberId is null)
            memberId = (await ResolveAdultMemberAsync(householdId, actorUserAccountId, cancellationToken))?.Id;
        var member = await dbContext.HouseholdMembers.SingleOrDefaultAsync(item =>
            item.HouseholdId == householdId && item.Id == memberId, cancellationToken);
        if (member is null || !member.IsActive) return new(ChoreOperationStatus.MemberInactive);
        var now = timeProvider.GetUtcNow();
        var completion = new ChoreCompletion
        {
            HouseholdId = householdId,
            ChoreAssignmentId = assignmentId,
            ClientRequestId = request.ClientRequestId,
            CompletedByMemberId = member.Id,
            SubmittedByUserAccountId = actorUserAccountId,
            WasSharedDisplay = session.IsSharedDisplay,
            CompletedAt = now,
            CompletedByMember = member,
        };
        assignment.Status = ChoreAssignmentStatus.AwaitingReview;
        assignment.UpdatedAt = now;
        assignment.Version++;
        dbContext.ChoreCompletions.Add(completion);
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException) { return new(ChoreOperationStatus.ConcurrencyConflict); }
        return new(ChoreOperationStatus.Success, MapCompletion(completion));
    }

    public async Task<IReadOnlyList<ChoreCompletionResponse>> ListPendingReviewsAsync(
        Guid householdId, CancellationToken cancellationToken) =>
        (await dbContext.ChoreCompletions.AsNoTracking()
            .Include(item => item.CompletedByMember).Include(item => item.ReviewedByMember)
            .Where(item => item.HouseholdId == householdId && item.Status == ChoreCompletionStatus.PendingReview)
            .OrderBy(item => item.CompletedAt).ToListAsync(cancellationToken))
        .Select(MapCompletion).ToList();

    public async Task<ChoreOperationResult<ChoreCompletionResponse>> ReviewAsync(
        Guid householdId, Guid completionId, Guid actorUserAccountId, ReviewChoreCompletionRequest request,
        CancellationToken cancellationToken)
    {
        var completion = await dbContext.ChoreCompletions
            .Include(item => item.CompletedByMember).Include(item => item.ReviewedByMember)
            .Include(item => item.ChoreAssignment)
            .SingleOrDefaultAsync(item => item.HouseholdId == householdId && item.Id == completionId,
                cancellationToken);
        if (completion is null) return new(ChoreOperationStatus.NotFound);
        var approved = request.Decision.Equals("approved", StringComparison.OrdinalIgnoreCase);
        var target = approved ? ChoreCompletionStatus.Approved : ChoreCompletionStatus.Rejected;
        if (completion.Status != ChoreCompletionStatus.PendingReview)
            return completion.Status == target
                ? new(ChoreOperationStatus.Success, MapCompletion(completion))
                : new(ChoreOperationStatus.AlreadyReviewed);
        if (completion.Version != request.ExpectedVersion) return new(ChoreOperationStatus.ConcurrencyConflict);
        var reviewer = await ResolveAdultMemberAsync(householdId, actorUserAccountId, cancellationToken);
        if (reviewer is null) return new(ChoreOperationStatus.NotFound);
        var now = timeProvider.GetUtcNow();
        completion.Status = target;
        completion.ReviewedByMemberId = reviewer.Id;
        completion.ReviewedByMember = reviewer;
        completion.ReviewedAt = now;
        completion.ReviewNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        completion.Version++;
        completion.ChoreAssignment.Status = approved ? ChoreAssignmentStatus.Completed : ChoreAssignmentStatus.Pending;
        completion.ChoreAssignment.UpdatedAt = now;
        completion.ChoreAssignment.Version++;
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException) { return new(ChoreOperationStatus.ConcurrencyConflict); }
        return new(ChoreOperationStatus.Success, MapCompletion(completion));
    }

    public async Task<ChoreOperationResult<ChoreAssignmentResponse>> SkipAsync(
        Guid householdId, Guid assignmentId, Guid actorUserAccountId, SkipChoreAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        var assignment = await dbContext.ChoreAssignments
            .Include(item => item.HouseholdMember)
            .Include(item => item.Completions).ThenInclude(item => item.CompletedByMember)
            .Include(item => item.Completions).ThenInclude(item => item.ReviewedByMember)
            .SingleOrDefaultAsync(item => item.HouseholdId == householdId && item.Id == assignmentId,
                cancellationToken);
        if (assignment is null) return new(ChoreOperationStatus.NotFound);
        if (assignment.Version != request.ExpectedVersion) return new(ChoreOperationStatus.ConcurrencyConflict);
        if (assignment.Status != ChoreAssignmentStatus.Pending) return new(ChoreOperationStatus.NotActionable);
        var actor = await ResolveAdultMemberAsync(householdId, actorUserAccountId, cancellationToken);
        if (actor is null) return new(ChoreOperationStatus.NotFound);
        var now = timeProvider.GetUtcNow();
        assignment.Status = ChoreAssignmentStatus.Skipped;
        assignment.SkippedAt = now;
        assignment.SkippedByMemberId = actor.Id;
        assignment.SkipReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        assignment.UpdatedAt = now;
        assignment.Version++;
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException) { return new(ChoreOperationStatus.ConcurrencyConflict); }
        return new(ChoreOperationStatus.Success, MapAssignment(assignment, now));
    }

    private IQueryable<ChoreAssignment> AssignmentQuery(Guid householdId) =>
        dbContext.ChoreAssignments.AsNoTracking()
            .Include(item => item.HouseholdMember)
            .Include(item => item.Completions).ThenInclude(item => item.CompletedByMember)
            .Include(item => item.Completions).ThenInclude(item => item.ReviewedByMember)
            .Where(item => item.HouseholdId == householdId);

    private async Task<HouseholdMember?> ResolveAdultMemberAsync(
        Guid householdId, Guid userAccountId, CancellationToken cancellationToken) =>
        await dbContext.HouseholdMemberships.Where(item => item.HouseholdId == householdId
                && item.UserAccountId == userAccountId && item.HouseholdMember.IsActive)
            .Select(item => item.HouseholdMember).SingleOrDefaultAsync(cancellationToken);

    private static ChoreDefinitionResponse MapDefinition(ChoreDefinition item) =>
        new(item.Id, item.Title, item.Description, item.IsActive, item.Version, item.CreatedAt, item.UpdatedAt);

    private static ChoreParticipantResponse MapMember(HouseholdMember member) =>
        new(member.Id, member.DisplayName, member.Role.ToString().ToLowerInvariant(), member.AvatarColor);

    private static ChoreCompletionResponse MapCompletion(ChoreCompletion item) =>
        new(item.Id, item.ChoreAssignmentId, MapMember(item.CompletedByMember),
            item.Status.ToString()[..1].ToLowerInvariant() + item.Status.ToString()[1..], item.WasSharedDisplay,
            item.CompletedAt, item.ReviewedByMember is null ? null : MapMember(item.ReviewedByMember),
            item.ReviewedAt, item.ReviewNote, item.Version);

    private static ChoreAssignmentResponse MapAssignment(ChoreAssignment item, DateTimeOffset now)
    {
        var pending = item.Completions.SingleOrDefault(c => c.Status == ChoreCompletionStatus.PendingReview);
        var status = item.Status.ToString();
        return new(item.Id, item.ChoreDefinitionId, item.TitleSnapshot, item.DescriptionSnapshot,
            MapMember(item.HouseholdMember), item.DueLocalDate, item.DueLocalTime, item.DueAt,
            item.DueTimeZone, item.DueHasExplicitTime,
            status[..1].ToLowerInvariant() + status[1..],
            item.Status == ChoreAssignmentStatus.Pending && item.DueAt < now, item.Version,
            pending is null ? null : MapCompletion(pending), item.CreatedAt, item.UpdatedAt);
    }
}

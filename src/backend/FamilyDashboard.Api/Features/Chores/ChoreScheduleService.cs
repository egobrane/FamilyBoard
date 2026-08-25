using FamilyDashboard.Api.Domain.Chores;
using FamilyDashboard.Api.Domain.Households;
using FamilyDashboard.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyDashboard.Api.Features.Chores;

public sealed class ChoreScheduleService(
    FamilyDashboardDbContext dbContext,
    ChoreRecurrenceCalculator recurrenceCalculator,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<ChoreScheduleResponse>> ListAsync(Guid householdId,
        bool includeInactive, CancellationToken cancellationToken)
    {
        var zone = await GetTimeZoneAsync(householdId, cancellationToken);
        var rows = await ScheduleQuery(householdId)
            .Where(item => includeInactive || item.Status == ChoreScheduleStatus.Active)
            .OrderBy(item => item.Status).ThenBy(item => item.NextOccurrenceLocalDate)
            .ThenBy(item => item.ChoreDefinition.Title).ToListAsync(cancellationToken);
        return rows.Select(item => Map(item, zone)).ToList();
    }

    public async Task<ChoreScheduleOperationResult<ChoreScheduleResponse>> GetAsync(Guid householdId,
        Guid scheduleId, CancellationToken cancellationToken)
    {
        var schedule = await ScheduleQuery(householdId).SingleOrDefaultAsync(
            item => item.Id == scheduleId, cancellationToken);
        return schedule is null
            ? new(ChoreScheduleOperationStatus.NotFound)
            : new(ChoreScheduleOperationStatus.Success,
                Map(schedule, await GetTimeZoneAsync(householdId, cancellationToken)));
    }

    public async Task<ChoreScheduleOperationResult<ChoreScheduleResponse>> CreateAsync(
        Guid householdId, Guid actorUserAccountId, CreateChoreScheduleRequest request,
        (ChoreRecurrenceKind Kind, int Interval, int? DaysMask) recurrence,
        CancellationToken cancellationToken)
    {
        var existing = await ScheduleQuery(householdId).SingleOrDefaultAsync(
            item => item.ClientRequestId == request.ClientRequestId, cancellationToken);
        var zone = await GetTimeZoneAsync(householdId, cancellationToken);
        if (existing is not null)
            return Matches(existing, request, recurrence)
                ? new(ChoreScheduleOperationStatus.Success, Map(existing, zone))
                : new(ChoreScheduleOperationStatus.IdempotencyConflict);

        var definition = await dbContext.ChoreDefinitions.SingleOrDefaultAsync(item =>
            item.HouseholdId == householdId && item.Id == request.ChoreDefinitionId, cancellationToken);
        if (definition is null) return new(ChoreScheduleOperationStatus.NotFound);
        if (!definition.IsActive) return new(ChoreScheduleOperationStatus.DefinitionInactive);
        var member = await dbContext.HouseholdMembers.SingleOrDefaultAsync(item =>
            item.HouseholdId == householdId && item.Id == request.AssignedMemberId, cancellationToken);
        if (member is null) return new(ChoreScheduleOperationStatus.NotFound);
        if (!member.IsActive) return new(ChoreScheduleOperationStatus.MemberInactive);
        var actor = await ResolveAdultMemberAsync(householdId, actorUserAccountId, cancellationToken);
        if (actor is null) return new(ChoreScheduleOperationStatus.NotFound);
        var today = LocalToday(zone);
        if (request.StartLocalDate < today || request.StartLocalDate > today.AddYears(2))
            return new(ChoreScheduleOperationStatus.InvalidSchedule);
        var next = recurrenceCalculator.FindNext(recurrence.Kind, recurrence.Interval,
            recurrence.DaysMask, request.StartLocalDate, request.EndLocalDate, request.StartLocalDate);
        if (next is null) return new(ChoreScheduleOperationStatus.InvalidSchedule);
        var now = timeProvider.GetUtcNow();
        var schedule = new ChoreSchedule
        {
            HouseholdId = householdId,
            ChoreDefinitionId = definition.Id,
            HouseholdMemberId = member.Id,
            CreatedByMemberId = actor.Id,
            ClientRequestId = request.ClientRequestId,
            RecurrenceKind = recurrence.Kind,
            Interval = recurrence.Interval,
            DaysOfWeekMask = recurrence.DaysMask,
            StartLocalDate = request.StartLocalDate,
            EndLocalDate = request.EndLocalDate,
            DueLocalTime = request.DueLocalTime,
            NextOccurrenceLocalDate = next,
            CreatedAt = now,
            UpdatedAt = now,
            ChoreDefinition = definition,
            HouseholdMember = member,
            CreatedByMember = actor,
        };
        dbContext.ChoreSchedules.Add(schedule);
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException) { return new(ChoreScheduleOperationStatus.ConcurrencyConflict); }
        return new(ChoreScheduleOperationStatus.Success, Map(schedule, zone));
    }

    public async Task<ChoreScheduleOperationResult<ChoreScheduleResponse>> UpdateAsync(
        Guid householdId, Guid scheduleId, UpdateChoreScheduleRequest request,
        (ChoreRecurrenceKind Kind, int Interval, int? DaysMask) recurrence,
        CancellationToken cancellationToken)
    {
        var schedule = await dbContext.ChoreSchedules.Include(item => item.ChoreDefinition)
            .Include(item => item.HouseholdMember).Include(item => item.CreatedByMember)
            .SingleOrDefaultAsync(item => item.HouseholdId == householdId && item.Id == scheduleId,
                cancellationToken);
        if (schedule is null) return new(ChoreScheduleOperationStatus.NotFound);
        if (schedule.Version != request.ExpectedVersion) return new(ChoreScheduleOperationStatus.ConcurrencyConflict);
        var definition = await dbContext.ChoreDefinitions.SingleOrDefaultAsync(item =>
            item.HouseholdId == householdId && item.Id == request.ChoreDefinitionId, cancellationToken);
        if (definition is null) return new(ChoreScheduleOperationStatus.NotFound);
        if (!definition.IsActive) return new(ChoreScheduleOperationStatus.DefinitionInactive);
        var member = await dbContext.HouseholdMembers.SingleOrDefaultAsync(item =>
            item.HouseholdId == householdId && item.Id == request.AssignedMemberId, cancellationToken);
        if (member is null) return new(ChoreScheduleOperationStatus.NotFound);
        if (!member.IsActive) return new(ChoreScheduleOperationStatus.MemberInactive);
        var zone = await GetTimeZoneAsync(householdId, cancellationToken);
        var today = LocalToday(zone);
        if (request.StartLocalDate > today.AddYears(2)) return new(ChoreScheduleOperationStatus.InvalidSchedule);
        var searchFrom = schedule.LastGeneratedOccurrenceLocalDate?.AddDays(1) ?? today;
        if (searchFrom < today) searchFrom = today;
        var next = recurrenceCalculator.FindNext(recurrence.Kind, recurrence.Interval,
            recurrence.DaysMask, request.StartLocalDate, request.EndLocalDate, searchFrom);
        schedule.ChoreDefinitionId = definition.Id;
        schedule.HouseholdMemberId = member.Id;
        schedule.RecurrenceKind = recurrence.Kind;
        schedule.Interval = recurrence.Interval;
        schedule.DaysOfWeekMask = recurrence.DaysMask;
        schedule.StartLocalDate = request.StartLocalDate;
        schedule.EndLocalDate = request.EndLocalDate;
        schedule.DueLocalTime = request.DueLocalTime;
        schedule.NextOccurrenceLocalDate = next;
        schedule.Status = next is null ? ChoreScheduleStatus.Completed : ChoreScheduleStatus.Active;
        schedule.BlockedReason = null;
        schedule.PausedAt = null;
        schedule.UpdatedAt = timeProvider.GetUtcNow();
        schedule.Version++;
        schedule.ChoreDefinition = definition;
        schedule.HouseholdMember = member;
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return new(ChoreScheduleOperationStatus.ConcurrencyConflict); }
        return new(ChoreScheduleOperationStatus.Success, Map(schedule, zone));
    }

    public async Task<ChoreScheduleOperationResult<ChoreScheduleResponse>> SetStateAsync(
        Guid householdId, Guid scheduleId, long expectedVersion, bool active,
        CancellationToken cancellationToken)
    {
        var schedule = await dbContext.ChoreSchedules.Include(item => item.ChoreDefinition)
            .Include(item => item.HouseholdMember).Include(item => item.CreatedByMember)
            .SingleOrDefaultAsync(item => item.HouseholdId == householdId && item.Id == scheduleId,
                cancellationToken);
        if (schedule is null) return new(ChoreScheduleOperationStatus.NotFound);
        if (schedule.Version != expectedVersion) return new(ChoreScheduleOperationStatus.ConcurrencyConflict);
        var zone = await GetTimeZoneAsync(householdId, cancellationToken);
        if (!active)
        {
            if (schedule.Status == ChoreScheduleStatus.Paused)
                return new(ChoreScheduleOperationStatus.Success, Map(schedule, zone));
            schedule.Status = ChoreScheduleStatus.Paused;
            schedule.PausedAt = timeProvider.GetUtcNow();
        }
        else
        {
            if (!schedule.ChoreDefinition.IsActive || !schedule.HouseholdMember.IsActive)
                return new(ChoreScheduleOperationStatus.DependencyInactive);
            var today = LocalToday(zone);
            var next = recurrenceCalculator.FindNext(schedule.RecurrenceKind, schedule.Interval,
                schedule.DaysOfWeekMask, schedule.StartLocalDate, schedule.EndLocalDate, today);
            if (next is null) return new(ChoreScheduleOperationStatus.InvalidSchedule);
            schedule.Status = ChoreScheduleStatus.Active;
            schedule.NextOccurrenceLocalDate = next;
            schedule.BlockedReason = null;
            schedule.PausedAt = null;
        }
        schedule.UpdatedAt = timeProvider.GetUtcNow();
        schedule.Version++;
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return new(ChoreScheduleOperationStatus.ConcurrencyConflict); }
        return new(ChoreScheduleOperationStatus.Success, Map(schedule, zone));
    }

    public ChoreSchedulePreviewResponse Preview(PreviewChoreScheduleRequest request,
        (ChoreRecurrenceKind Kind, int Interval, int? DaysMask) recurrence)
    {
        var dates = new List<DateOnly>();
        var next = recurrenceCalculator.FindNext(recurrence.Kind, recurrence.Interval,
            recurrence.DaysMask, request.StartLocalDate, request.EndLocalDate, request.StartLocalDate);
        while (next is not null && dates.Count < 10)
        {
            dates.Add(next.Value);
            next = recurrenceCalculator.FindNext(recurrence.Kind, recurrence.Interval,
                recurrence.DaysMask, request.StartLocalDate, request.EndLocalDate, next.Value.AddDays(1));
        }
        return new(dates);
    }

    private IQueryable<ChoreSchedule> ScheduleQuery(Guid householdId) =>
        dbContext.ChoreSchedules.AsNoTracking().Include(item => item.ChoreDefinition)
            .Include(item => item.HouseholdMember).Include(item => item.CreatedByMember)
            .Where(item => item.HouseholdId == householdId);

    private async Task<string> GetTimeZoneAsync(Guid householdId, CancellationToken cancellationToken) =>
        await dbContext.HouseholdConfigurations.AsNoTracking().Where(item => item.HouseholdId == householdId)
            .Select(item => item.TimeZone).SingleAsync(cancellationToken);

    private DateOnly LocalToday(string zone) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
        timeProvider.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(zone)).Date);

    private async Task<HouseholdMember?> ResolveAdultMemberAsync(Guid householdId, Guid accountId,
        CancellationToken cancellationToken) => await dbContext.HouseholdMemberships
        .Where(item => item.HouseholdId == householdId && item.UserAccountId == accountId
            && item.HouseholdMember.IsActive).Select(item => item.HouseholdMember)
        .SingleOrDefaultAsync(cancellationToken);

    private static bool Matches(ChoreSchedule item, CreateChoreScheduleRequest request,
        (ChoreRecurrenceKind Kind, int Interval, int? DaysMask) recurrence) =>
        item.ChoreDefinitionId == request.ChoreDefinitionId
        && item.HouseholdMemberId == request.AssignedMemberId
        && item.RecurrenceKind == recurrence.Kind && item.Interval == recurrence.Interval
        && item.DaysOfWeekMask == recurrence.DaysMask && item.StartLocalDate == request.StartLocalDate
        && item.EndLocalDate == request.EndLocalDate && item.DueLocalTime == request.DueLocalTime;

    private static ChoreScheduleResponse Map(ChoreSchedule item, string zone) => new(
        item.Id,
        new(item.ChoreDefinition.Id, item.ChoreDefinition.Title, item.ChoreDefinition.Description,
            item.ChoreDefinition.DefaultPointValue, item.ChoreDefinition.IsActive, item.ChoreDefinition.Version,
            item.ChoreDefinition.CreatedAt, item.ChoreDefinition.UpdatedAt),
        new(item.HouseholdMember.Id, item.HouseholdMember.DisplayName,
            item.HouseholdMember.Role.ToString().ToLowerInvariant(), item.HouseholdMember.AvatarColor),
        new(item.RecurrenceKind.ToString().ToLowerInvariant(), item.Interval, Days(item.DaysOfWeekMask)),
        item.StartLocalDate, item.EndLocalDate, item.DueLocalTime, zone,
        LowerCamel(item.Status.ToString()), item.BlockedReason, item.NextOccurrenceLocalDate,
        item.LastGeneratedOccurrenceLocalDate, item.LastEvaluatedAt, item.Version,
        item.CreatedAt, item.UpdatedAt);

    private static List<string> Days(int? mask)
    {
        if (mask is null) return [];
        var result = new List<string>();
        var names = new[] { "monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday" };
        for (var index = 0; index < names.Length; index++) if ((mask.Value & (1 << index)) != 0) result.Add(names[index]);
        return result;
    }

    private static string LowerCamel(string value) => value[..1].ToLowerInvariant() + value[1..];
}

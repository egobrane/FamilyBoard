using System.Security.Cryptography;
using System.Text;
using FamilyDashboard.Api.Configuration;
using FamilyDashboard.Api.Domain.Chores;
using FamilyDashboard.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FamilyDashboard.Api.Features.Chores;

public sealed record ChoreGenerationResult(int SchedulesExamined, int AssignmentsGenerated,
    int AssignmentsExpired, int SchedulesBlocked, bool AlreadyRunning);

public sealed class ChoreAssignmentGenerator(
    FamilyDashboardDbContext dbContext,
    ChoreRecurrenceCalculator recurrenceCalculator,
    ChoreDueTimeService dueTimeService,
    TimeProvider timeProvider,
    IOptions<ChoreGenerationConfiguration> options,
    ILogger<ChoreAssignmentGenerator> logger)
{
    private static readonly Action<ILogger, Exception?> GenerationAlreadyRunning =
        LoggerMessage.Define(LogLevel.Information, new EventId(1, nameof(GenerationAlreadyRunning)),
            "Chore generation skipped because another generator owns the lock.");
    private static readonly Action<ILogger, int, int, int, int, Exception?> GenerationCompleted =
        LoggerMessage.Define<int, int, int, int>(LogLevel.Information,
            new EventId(2, nameof(GenerationCompleted)),
            "Chore generation completed. Schedules={SchedulesExamined}, Generated={AssignmentsGenerated}, Expired={AssignmentsExpired}, Blocked={SchedulesBlocked}");

    public async Task<ChoreGenerationResult> GenerateAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var acquired = await dbContext.Database
            .SqlQueryRaw<bool>("SELECT pg_try_advisory_xact_lock(73421901) AS \"Value\"")
            .SingleAsync(cancellationToken);
        if (!acquired)
        {
            GenerationAlreadyRunning(logger, null);
            return new(0, 0, 0, 0, true);
        }

        var settings = options.Value;
        var now = timeProvider.GetUtcNow();
        var expired = await dbContext.ChoreAssignments
            .Where(item => item.Status == ChoreAssignmentStatus.Pending
                && item.AssignmentMode == ChoreAssignmentMode.Open
                && item.HouseholdMemberId == null
                && item.ClaimExpiresAt != null
                && item.ClaimExpiresAt <= now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, ChoreAssignmentStatus.Skipped)
                .SetProperty(item => item.ExpiredAt, now)
                .SetProperty(item => item.SkipReason, "Expired unclaimed at the end of its due day.")
                .SetProperty(item => item.UpdatedAt, now)
                .SetProperty(item => item.Version, item => item.Version + 1), cancellationToken);
        var horizon = now.AddHours(settings.HorizonHours);
        var schedules = await dbContext.ChoreSchedules
            .Include(item => item.ChoreDefinition).Include(item => item.HouseholdMember)
            .Where(item => item.Status == ChoreScheduleStatus.Active
                && item.NextOccurrenceLocalDate != null)
            .OrderBy(item => item.NextOccurrenceLocalDate).ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var zones = await dbContext.HouseholdConfigurations.AsNoTracking()
            .Where(item => schedules.Select(schedule => schedule.HouseholdId).Contains(item.HouseholdId))
            .ToDictionaryAsync(item => item.HouseholdId, item => item.TimeZone, cancellationToken);
        var generated = 0;
        var blocked = 0;

        foreach (var schedule in schedules)
        {
            schedule.LastEvaluatedAt = now;
            if (!schedule.ChoreDefinition.IsActive
                || (schedule.AssignmentMode == ChoreAssignmentMode.Assigned
                    && schedule.HouseholdMember?.IsActive != true))
            {
                schedule.Status = ChoreScheduleStatus.Blocked;
                schedule.BlockedReason = !schedule.ChoreDefinition.IsActive
                    ? "definitionInactive" : "memberInactive";
                schedule.UpdatedAt = now;
                schedule.Version++;
                blocked++;
                continue;
            }
            if (!zones.TryGetValue(schedule.HouseholdId, out var zone))
            {
                schedule.Status = ChoreScheduleStatus.Blocked;
                schedule.BlockedReason = "householdTimeZoneUnavailable";
                schedule.UpdatedAt = now;
                schedule.Version++;
                blocked++;
                continue;
            }

            var next = schedule.NextOccurrenceLocalDate;
            while (next is not null && generated < settings.MaximumAssignmentsPerRun)
            {
                if (!dueTimeService.TryResolveRecurring(next.Value, schedule.DueLocalTime, zone,
                        out var dueAt, out var resolution, out _))
                {
                    schedule.Status = ChoreScheduleStatus.Blocked;
                    schedule.BlockedReason = "dueTimeUnavailable";
                    schedule.UpdatedAt = now;
                    schedule.Version++;
                    blocked++;
                    break;
                }
                if (dueAt > horizon) break;
                DateTimeOffset? claimExpiresAt = null;
                if (schedule.AssignmentMode == ChoreAssignmentMode.Open)
                {
                    if (!dueTimeService.TryResolveOpenClaimExpiration(next.Value, zone,
                            out var resolvedClaimExpiresAt, out _))
                    {
                        schedule.Status = ChoreScheduleStatus.Blocked;
                        schedule.BlockedReason = "claimExpirationUnavailable";
                        schedule.UpdatedAt = now;
                        schedule.Version++;
                        blocked++;
                        break;
                    }
                    claimExpiresAt = resolvedClaimExpiresAt;
                }

                var occurrence = next.Value;
                var isExpiredOpenOccurrence = claimExpiresAt is not null && claimExpiresAt <= now;
                dbContext.ChoreAssignments.Add(new ChoreAssignment
                {
                    HouseholdId = schedule.HouseholdId,
                    ChoreDefinitionId = schedule.ChoreDefinitionId,
                    HouseholdMemberId = schedule.HouseholdMemberId,
                    AssignmentMode = schedule.AssignmentMode,
                    ClaimExpiresAt = claimExpiresAt,
                    CreatedByMemberId = schedule.CreatedByMemberId,
                    ChoreScheduleId = schedule.Id,
                    ClientRequestId = OccurrenceRequestId(schedule.Id, occurrence),
                    TitleSnapshot = schedule.ChoreDefinition.Title,
                    DescriptionSnapshot = schedule.ChoreDefinition.Description,
                    PointValueSnapshot = schedule.ChoreDefinition.DefaultPointValue,
                    DueAt = dueAt,
                    DueLocalDate = occurrence,
                    DueLocalTime = schedule.DueLocalTime,
                    DueTimeZone = zone,
                    DueHasExplicitTime = schedule.DueLocalTime is not null,
                    ScheduleOccurrenceLocalDate = occurrence,
                    GeneratedAt = now,
                    DueTimeResolution = resolution,
                    Status = isExpiredOpenOccurrence
                        ? ChoreAssignmentStatus.Skipped
                        : ChoreAssignmentStatus.Pending,
                    ExpiredAt = isExpiredOpenOccurrence ? now : null,
                    SkipReason = isExpiredOpenOccurrence
                        ? "Expired unclaimed at the end of its due day."
                        : null,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
                generated++;
                if (isExpiredOpenOccurrence) expired++;
                schedule.LastGeneratedOccurrenceLocalDate = occurrence;
                next = recurrenceCalculator.FindNext(schedule.RecurrenceKind, schedule.Interval,
                    schedule.DaysOfWeekMask, schedule.StartLocalDate, schedule.EndLocalDate,
                    occurrence.AddDays(1));
                schedule.NextOccurrenceLocalDate = next;
                schedule.UpdatedAt = now;
                schedule.Version++;
                if (next is null) schedule.Status = ChoreScheduleStatus.Completed;
            }

            if (generated >= settings.MaximumAssignmentsPerRun) break;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        GenerationCompleted(logger, schedules.Count, generated, expired, blocked, null);
        return new(schedules.Count, generated, expired, blocked, false);
    }

    private static Guid OccurrenceRequestId(Guid scheduleId, DateOnly date)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"chore-schedule:{scheduleId:N}:{date:yyyy-MM-dd}"));
        return new Guid(bytes.AsSpan(0, 16));
    }
}

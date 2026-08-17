using System.Data;
using System.Security.Claims;
using FamilyDashboard.Api.Configuration;
using FamilyDashboard.Api.Domain.Households;
using FamilyDashboard.Api.Domain.Identity;
using FamilyDashboard.Api.Persistence;
using FamilyDashboard.Api.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace FamilyDashboard.Api.Features.ParentAccess;

public sealed class ParentAccessService(
    FamilyDashboardDbContext dbContext,
    ParentPinHasher hasher,
    TimeProvider timeProvider,
    IOptions<ParentAccessConfiguration> parentOptions,
    IOptions<AuthenticationConfiguration> authenticationOptions)
{
    private readonly ParentAccessConfiguration _options = parentOptions.Value;
    private readonly AuthenticationConfiguration _authentication = authenticationOptions.Value;

    public bool IsAvailable => hasher.IsAvailable;

    public async Task<ParentAccessOperationResult> GetStateAsync(
        Guid householdId, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        if (!IsAvailable) return new(ParentAccessOperationStatus.Unavailable);
        var session = await FindSessionAsync(principal, false, cancellationToken);
        if (session is null) return new(ParentAccessOperationStatus.SessionUnavailable);
        var configured = await dbContext.HouseholdAccessPins.AsNoTracking()
            .AnyAsync(pin => pin.HouseholdId == householdId, cancellationToken);
        return new(ParentAccessOperationStatus.Success, State(householdId, configured, session));
    }

    public async Task<ParentAccessOperationResult> SetupOrChangeAsync(
        Guid householdId, ClaimsPrincipal principal, string pin, string? traceId,
        CancellationToken cancellationToken)
    {
        if (!IsAvailable) return new(ParentAccessOperationStatus.Unavailable);
        var session = await FindSessionAsync(principal, true, cancellationToken);
        if (session is null) return new(ParentAccessOperationStatus.SessionUnavailable);
        var now = timeProvider.GetUtcNow();
        var stored = await dbContext.HouseholdAccessPins.SingleOrDefaultAsync(
            candidate => candidate.HouseholdId == householdId, cancellationToken);

        if (stored is null)
        {
            if (session.IsSharedDisplay) return new(ParentAccessOperationStatus.PrivateSessionRequired);
            if (now - session.CreatedAt > _options.RecentAuthenticationLifetime)
                return new(ParentAccessOperationStatus.RecentAuthenticationRequired);
            stored = hasher.Create(householdId, session.UserAccountId, pin, now);
            dbContext.HouseholdAccessPins.Add(stored);
            Elevate(session, householdId, now);
            Audit(session, householdId, ParentAccessAuditEventType.PinSetup,
                ParentAccessAuditOutcome.Succeeded, traceId, now);
        }
        else
        {
            if (!IsElevated(session, householdId, now))
                return new(ParentAccessOperationStatus.ElevationRequired);
            hasher.Upgrade(stored, session.UserAccountId, pin, now);
            await ClearOtherElevationsAsync(householdId, session.Id, cancellationToken);
            Elevate(session, householdId, now);
            Audit(session, householdId, ParentAccessAuditEventType.PinChanged,
                ParentAccessAuditOutcome.Succeeded, traceId, now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new(ParentAccessOperationStatus.Success, State(householdId, true, session));
    }

    public async Task<ParentAccessOperationResult> RecoverAsync(
        Guid householdId, ClaimsPrincipal principal, string pin, string? traceId,
        CancellationToken cancellationToken)
    {
        if (!IsAvailable) return new(ParentAccessOperationStatus.Unavailable);
        var session = await FindSessionAsync(principal, true, cancellationToken);
        if (session is null) return new(ParentAccessOperationStatus.SessionUnavailable);
        var now = timeProvider.GetUtcNow();
        if (session.IsSharedDisplay) return new(ParentAccessOperationStatus.PrivateSessionRequired);
        if (now - session.CreatedAt > _options.RecentAuthenticationLifetime)
            return new(ParentAccessOperationStatus.RecentAuthenticationRequired);
        var stored = await dbContext.HouseholdAccessPins.SingleOrDefaultAsync(
            candidate => candidate.HouseholdId == householdId, cancellationToken);
        if (stored is null) return new(ParentAccessOperationStatus.PinNotConfigured);

        hasher.Upgrade(stored, session.UserAccountId, pin, now);
        await ClearOtherElevationsAsync(householdId, session.Id, cancellationToken);
        Elevate(session, householdId, now);
        ResetFailures(session);
        Audit(session, householdId, ParentAccessAuditEventType.PinRecovered,
            ParentAccessAuditOutcome.Succeeded, traceId, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new(ParentAccessOperationStatus.Success, State(householdId, true, session));
    }

    public async Task<ParentAccessOperationResult> VerifyAsync(
        Guid householdId, ClaimsPrincipal principal, string pin, string? traceId,
        CancellationToken cancellationToken)
    {
        if (!IsAvailable) return new(ParentAccessOperationStatus.Unavailable);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var session = await FindSessionAsync(principal, true, cancellationToken);
        if (session is null) return new(ParentAccessOperationStatus.SessionUnavailable);
        var now = timeProvider.GetUtcNow();
        if (session.ParentAccessLockedUntil > now)
            return new(ParentAccessOperationStatus.Locked, RetryAt: session.ParentAccessLockedUntil);

        var stored = await dbContext.HouseholdAccessPins.SingleOrDefaultAsync(
            candidate => candidate.HouseholdId == householdId, cancellationToken);
        if (stored is null) return new(ParentAccessOperationStatus.PinNotConfigured);

        if (!hasher.Verify(stored, pin))
        {
            RegisterFailure(session, now);
            var locked = session.ParentAccessLockedUntil > now;
            Audit(session, householdId,
                locked ? ParentAccessAuditEventType.CooldownStarted : ParentAccessAuditEventType.VerificationFailed,
                ParentAccessAuditOutcome.Rejected, traceId, now, session.ParentAccessLockedUntil);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception exception) when (IsSerializationFailure(exception))
            {
                return new(ParentAccessOperationStatus.Conflict);
            }
            return locked
                ? new(ParentAccessOperationStatus.Locked, RetryAt: session.ParentAccessLockedUntil)
                : new(ParentAccessOperationStatus.InvalidPin);
        }

        ResetFailures(session);
        Elevate(session, householdId, now);
        if (hasher.NeedsUpgrade(stored)) hasher.Upgrade(stored, session.UserAccountId, pin, now);
        Audit(session, householdId, ParentAccessAuditEventType.VerificationSucceeded,
            ParentAccessAuditOutcome.Succeeded, traceId, now);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception) when (IsSerializationFailure(exception))
        {
            return new(ParentAccessOperationStatus.Conflict);
        }
        return new(ParentAccessOperationStatus.Success, State(householdId, true, session));
    }

    public async Task<ParentAccessOperationResult> LockAsync(
        Guid householdId, ClaimsPrincipal principal, string? traceId,
        CancellationToken cancellationToken)
    {
        var session = await FindSessionAsync(principal, true, cancellationToken);
        if (session is null) return new(ParentAccessOperationStatus.SessionUnavailable);
        ClearElevation(session);
        Audit(session, householdId, ParentAccessAuditEventType.ExplicitlyLocked,
            ParentAccessAuditOutcome.Succeeded, traceId, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return new(ParentAccessOperationStatus.Success);
    }

    public async Task<ParentAccessOperationResult> UpdateSharedDisplayAsync(
        Guid householdId, ClaimsPrincipal principal, bool isSharedDisplay, string? deviceLabel,
        string? traceId, CancellationToken cancellationToken)
    {
        if (!IsAvailable) return new(ParentAccessOperationStatus.Unavailable);
        var session = await FindSessionAsync(principal, true, cancellationToken);
        if (session is null) return new(ParentAccessOperationStatus.SessionUnavailable);
        var now = timeProvider.GetUtcNow();
        if (session.SelectedHouseholdId != householdId)
            return new(ParentAccessOperationStatus.HouseholdNotFound);
        if (!await dbContext.HouseholdAccessPins.AsNoTracking()
                .AnyAsync(pin => pin.HouseholdId == householdId, cancellationToken))
            return new(ParentAccessOperationStatus.SharedDisplayRequiresPin);
        if (!IsElevated(session, householdId, now))
            return new(ParentAccessOperationStatus.ElevationRequired);

        session.IsSharedDisplay = isSharedDisplay;
        session.DeviceLabel = isSharedDisplay ? deviceLabel : null;
        session.LastSeenAt = now;
        session.AbsoluteExpiresAt = now.Add(isSharedDisplay
            ? _authentication.SharedDisplayAbsoluteLifetime
            : _authentication.SessionAbsoluteLifetime);
        session.ExpiresAt = now.Add(isSharedDisplay
            ? _authentication.SharedDisplayIdleLifetime
            : _authentication.SessionIdleLifetime);
        if (session.ExpiresAt > session.AbsoluteExpiresAt) session.ExpiresAt = session.AbsoluteExpiresAt;
        ClearElevation(session);
        ResetFailures(session);
        Audit(session, householdId,
            isSharedDisplay ? ParentAccessAuditEventType.SharedDisplayEnabled : ParentAccessAuditEventType.SharedDisplayDisabled,
            ParentAccessAuditOutcome.Succeeded, traceId, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new(ParentAccessOperationStatus.Success, State(householdId, true, session));
    }

    private async Task<UserSession?> FindSessionAsync(
        ClaimsPrincipal principal, bool tracking, CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserAccountId(out var userAccountId)
            || !principal.TryGetUserSessionId(out var sessionId)) return null;
        var query = dbContext.UserSessions.Where(session =>
            session.Id == sessionId && session.UserAccountId == userAccountId && session.RevokedAt == null);
        return await (tracking ? query : query.AsNoTracking()).SingleOrDefaultAsync(cancellationToken);
    }

    private ParentAccessStateResponse State(Guid householdId, bool configured, UserSession session)
    {
        var now = timeProvider.GetUtcNow();
        var elevated = IsElevated(session, householdId, now);
        return new(householdId, configured, _options.PinLength, session.IsSharedDisplay, elevated,
            elevated ? session.AdministrativeElevationExpiresAt : null,
            session.ParentAccessLockedUntil > now ? session.ParentAccessLockedUntil : null);
    }

    private static bool IsElevated(UserSession session, Guid householdId, DateTimeOffset now) =>
        session.AdministrativeElevationHouseholdId == householdId
        && session.AdministrativeElevationExpiresAt > now;

    private void Elevate(UserSession session, Guid householdId, DateTimeOffset now)
    {
        session.AdministrativeElevationHouseholdId = householdId;
        session.AdministrativeElevationExpiresAt = now.Add(_options.ElevationLifetime);
    }

    private static void ClearElevation(UserSession session)
    {
        session.AdministrativeElevationHouseholdId = null;
        session.AdministrativeElevationExpiresAt = null;
    }

    private void RegisterFailure(UserSession session, DateTimeOffset now)
    {
        if (session.ParentAccessFailureWindowStartedAt is null
            || now - session.ParentAccessFailureWindowStartedAt >= _options.FailureWindow)
        {
            session.ParentAccessFailureWindowStartedAt = now;
            session.ParentAccessFailedAttemptCount = 0;
        }
        session.ParentAccessFailedAttemptCount++;
        ClearElevation(session);
        if (session.ParentAccessFailedAttemptCount >= _options.MaximumFailures)
            session.ParentAccessLockedUntil = now.Add(_options.LockoutLifetime);
    }

    private static void ResetFailures(UserSession session)
    {
        session.ParentAccessFailedAttemptCount = 0;
        session.ParentAccessFailureWindowStartedAt = null;
        session.ParentAccessLockedUntil = null;
    }

    private async Task ClearOtherElevationsAsync(
        Guid householdId, Guid currentSessionId, CancellationToken cancellationToken)
    {
        await dbContext.UserSessions
            .Where(session => session.Id != currentSessionId
                && session.AdministrativeElevationHouseholdId == householdId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(session => session.AdministrativeElevationHouseholdId, (Guid?)null)
                .SetProperty(session => session.AdministrativeElevationExpiresAt, (DateTimeOffset?)null),
                cancellationToken);
    }

    private void Audit(
        UserSession session, Guid householdId, ParentAccessAuditEventType eventType,
        ParentAccessAuditOutcome outcome, string? traceId, DateTimeOffset now,
        DateTimeOffset? cooldownUntil = null)
    {
        dbContext.ParentAccessAuditEvents.Add(new ParentAccessAuditEvent
        {
            HouseholdId = householdId,
            UserAccountId = session.UserAccountId,
            UserSessionId = session.Id,
            EventType = eventType,
            Outcome = outcome,
            OccurredAt = now,
            TraceId = traceId,
            CooldownUntil = cooldownUntil,
        });
    }

    private static bool IsSerializationFailure(Exception exception) =>
        exception is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure }
        || exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure };
}

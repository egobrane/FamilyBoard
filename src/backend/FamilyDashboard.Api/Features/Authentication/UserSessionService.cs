using System.Security.Claims;
using FamilyDashboard.Api.Configuration;
using FamilyDashboard.Api.Domain.Identity;
using FamilyDashboard.Api.Persistence;
using FamilyDashboard.Api.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FamilyDashboard.Api.Features.Authentication;

public sealed record SessionValidationResult(bool IsValid, bool WasRenewed, UserSession? Session);

public enum HouseholdSelectionStatus
{
    Success,
    SessionUnavailable,
    HouseholdNotFound,
}

public sealed record HouseholdSelectionResult(
    HouseholdSelectionStatus Status,
    SelectedHouseholdResponse? Selection = null);

public sealed class UserSessionService(
    FamilyDashboardDbContext dbContext,
    TimeProvider timeProvider,
    IOptions<AuthenticationConfiguration> options)
{
    private readonly AuthenticationConfiguration _options = options.Value;

    public async Task<UserSession> CreateAsync(
        UserAccount account,
        bool isSharedDisplay,
        string? deviceLabel,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var idleLifetime = isSharedDisplay
            ? _options.SharedDisplayIdleLifetime
            : _options.SessionIdleLifetime;
        var absoluteLifetime = isSharedDisplay
            ? _options.SharedDisplayAbsoluteLifetime
            : _options.SessionAbsoluteLifetime;
        var absoluteExpiresAt = now.Add(absoluteLifetime);
        var session = new UserSession
        {
            UserAccountId = account.Id,
            UserAccount = account,
            CreatedAt = now,
            LastSeenAt = now,
            ExpiresAt = Min(now.Add(idleLifetime), absoluteExpiresAt),
            AbsoluteExpiresAt = absoluteExpiresAt,
            IsSharedDisplay = isSharedDisplay,
            DeviceLabel = string.IsNullOrWhiteSpace(deviceLabel) ? null : deviceLabel.Trim(),
        };
        dbContext.UserSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        return session;
    }

    public async Task<SessionValidationResult> ValidateAndRenewAsync(
        Guid sessionId,
        Guid userAccountId,
        CancellationToken cancellationToken)
    {
        var session = await dbContext.UserSessions
            .Include(candidate => candidate.UserAccount)
            .SingleOrDefaultAsync(candidate =>
                candidate.Id == sessionId && candidate.UserAccountId == userAccountId,
                cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (session is null
            || session.RevokedAt is not null
            || !session.UserAccount.IsActive
            || session.ExpiresAt <= now
            || session.AbsoluteExpiresAt <= now)
        {
            return new SessionValidationResult(false, false, session);
        }

        if (now - session.LastSeenAt < _options.LastSeenWriteInterval)
        {
            return new SessionValidationResult(true, false, session);
        }

        var idleLifetime = session.IsSharedDisplay
            ? _options.SharedDisplayIdleLifetime
            : _options.SessionIdleLifetime;
        session.LastSeenAt = now;
        session.ExpiresAt = Min(now.Add(idleLifetime), session.AbsoluteExpiresAt);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new SessionValidationResult(true, true, session);
    }

    public async Task<UserSession?> FindCurrentAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserAccountId(out var userAccountId)
            || !principal.TryGetUserSessionId(out var sessionId))
        {
            return null;
        }

        return await dbContext.UserSessions
            .AsNoTracking()
            .SingleOrDefaultAsync(session =>
                session.Id == sessionId && session.UserAccountId == userAccountId,
                cancellationToken);
    }

    public async Task<UserSession?> FindCurrentForUpdateAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserAccountId(out var userAccountId)
            || !principal.TryGetUserSessionId(out var sessionId))
        {
            return null;
        }

        return await dbContext.UserSessions.SingleOrDefaultAsync(session =>
            session.Id == sessionId && session.UserAccountId == userAccountId,
            cancellationToken);
    }

    public async Task<HouseholdSelectionResult> SelectHouseholdAsync(
        ClaimsPrincipal principal,
        Guid householdId,
        CancellationToken cancellationToken)
    {
        var session = await FindCurrentForUpdateAsync(principal, cancellationToken);
        if (session is null)
        {
            return new HouseholdSelectionResult(HouseholdSelectionStatus.SessionUnavailable);
        }

        var hasAccess = await dbContext.HouseholdMemberships
            .AsNoTracking()
            .AnyAsync(membership =>
                membership.UserAccountId == session.UserAccountId
                && membership.HouseholdId == householdId
                && membership.UserAccount.IsActive
                && membership.Household.IsActive
                && membership.HouseholdMember.IsActive,
                cancellationToken);
        if (!hasAccess)
        {
            return new HouseholdSelectionResult(HouseholdSelectionStatus.HouseholdNotFound);
        }

        session.SelectedHouseholdId = householdId;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new HouseholdSelectionResult(
            HouseholdSelectionStatus.Success,
            new SelectedHouseholdResponse(householdId));
    }

    public async Task RevokeCurrentAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        if (!principal.TryGetUserAccountId(out var userAccountId)
            || !principal.TryGetUserSessionId(out var sessionId))
        {
            return;
        }

        var session = await dbContext.UserSessions.SingleOrDefaultAsync(candidate =>
            candidate.Id == sessionId && candidate.UserAccountId == userAccountId,
            cancellationToken);
        if (session is null || session.RevokedAt is not null)
        {
            return;
        }

        session.RevokedAt = timeProvider.GetUtcNow();
        session.AdministrativeElevationExpiresAt = null;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public static ClaimsPrincipal CreatePrincipal(UserSession session)
    {
        var identity = new ClaimsIdentity(AuthenticationSchemes.ApplicationCookie);
        identity.AddClaim(new Claim(
            FamilyDashboardClaimTypes.UserAccountId,
            session.UserAccountId.ToString()));
        identity.AddClaim(new Claim(
            FamilyDashboardClaimTypes.UserSessionId,
            session.Id.ToString()));
        return new ClaimsPrincipal(identity);
    }

    private static DateTimeOffset Min(DateTimeOffset first, DateTimeOffset second) =>
        first <= second ? first : second;
}

using System.Data;
using FamilyDashboard.Api.Configuration;
using FamilyDashboard.Api.Domain.Households;
using FamilyDashboard.Api.Domain.Identity;
using FamilyDashboard.Api.Features.Authentication;
using FamilyDashboard.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace FamilyDashboard.Api.Features.Invitations;

public enum InvitationOperationStatus
{
    Success,
    NotFound,
    Expired,
    Revoked,
    Used,
    EmailMismatch,
    Conflict,
    SessionUnavailable,
}

public sealed record InvitationOperationResult<T>(InvitationOperationStatus Status, T? Value = default);
public sealed record PreparedInvitation(Guid Id, PendingInvitationResponse Response);

public sealed class InvitationService(
    FamilyDashboardDbContext dbContext,
    InvitationTokenService tokenService,
    TimeProvider timeProvider,
    IOptions<InvitationConfiguration> options)
{
    private const int MaximumAttempts = 4;
    private readonly InvitationConfiguration _options = options.Value;

    public async Task<InvitationOperationResult<CreatedInvitationResponse>> CreateAsync(
        Guid householdId,
        Guid createdByUserAccountId,
        string intendedEmailNormalized,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            var now = timeProvider.GetUtcNow();
            var stale = await dbContext.HouseholdInvitations
                .Where(invitation =>
                    invitation.HouseholdId == householdId
                    && invitation.IntendedEmailNormalized == intendedEmailNormalized
                    && invitation.Status == HouseholdInvitationStatus.Pending
                    && invitation.ExpiresAt <= now)
                .ToListAsync(cancellationToken);
            foreach (var staleInvitation in stale)
            {
                staleInvitation.Status = HouseholdInvitationStatus.Expired;
            }
            if (stale.Count > 0)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            if (await dbContext.HouseholdInvitations.AnyAsync(invitation =>
                    invitation.HouseholdId == householdId
                    && invitation.IntendedEmailNormalized == intendedEmailNormalized
                    && invitation.Status == HouseholdInvitationStatus.Pending,
                    cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return new(InvitationOperationStatus.Conflict);
            }

            var (token, hash) = tokenService.Create();
            var invitation = new HouseholdInvitation
            {
                HouseholdId = householdId,
                CreatedByUserAccountId = createdByUserAccountId,
                IntendedEmailNormalized = intendedEmailNormalized,
                TokenHash = hash,
                CreatedAt = now,
                ExpiresAt = now.Add(_options.Lifetime),
            };
            dbContext.HouseholdInvitations.Add(invitation);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(
                InvitationOperationStatus.Success,
                new CreatedInvitationResponse(Map(invitation), token));
        }
        catch (Exception exception) when (IsConcurrencyConflict(exception))
        {
            dbContext.ChangeTracker.Clear();
            return new(InvitationOperationStatus.Conflict);
        }
    }

    public async Task<IReadOnlyList<HouseholdInvitationResponse>> ListAsync(
        Guid householdId,
        CancellationToken cancellationToken)
    {
        await ExpirePendingAsync(householdId, cancellationToken);
        return await dbContext.HouseholdInvitations
            .AsNoTracking()
            .Where(invitation => invitation.HouseholdId == householdId)
            .OrderByDescending(invitation => invitation.CreatedAt)
            .Select(invitation => new HouseholdInvitationResponse(
                invitation.Id,
                invitation.HouseholdId,
                invitation.IntendedEmailNormalized,
                invitation.Status.ToString().ToLowerInvariant(),
                invitation.CreatedAt,
                invitation.ExpiresAt,
                invitation.AcceptedAt,
                invitation.RevokedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<InvitationOperationResult<HouseholdInvitationResponse>> RevokeAsync(
        Guid householdId,
        Guid invitationId,
        Guid revokedByUserAccountId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            var invitation = await dbContext.HouseholdInvitations.SingleOrDefaultAsync(candidate =>
                candidate.Id == invitationId && candidate.HouseholdId == householdId,
                cancellationToken);
            if (invitation is null) return new(InvitationOperationStatus.NotFound);
            var now = timeProvider.GetUtcNow();
            if (invitation.Status == HouseholdInvitationStatus.Pending && invitation.ExpiresAt <= now)
            {
                invitation.Status = HouseholdInvitationStatus.Expired;
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new(InvitationOperationStatus.Expired);
            }
            if (invitation.Status == HouseholdInvitationStatus.Accepted)
                return new(InvitationOperationStatus.Used);
            if (invitation.Status == HouseholdInvitationStatus.Expired)
                return new(InvitationOperationStatus.Expired);
            if (invitation.Status == HouseholdInvitationStatus.Pending)
            {
                invitation.Status = HouseholdInvitationStatus.Revoked;
                invitation.RevokedAt = now;
                invitation.RevokedByUserAccountId = revokedByUserAccountId;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            return new(InvitationOperationStatus.Success, Map(invitation));
        }
        catch (Exception exception) when (IsConcurrencyConflict(exception))
        {
            dbContext.ChangeTracker.Clear();
            return new(InvitationOperationStatus.Conflict);
        }
    }

    public async Task<InvitationOperationResult<PreparedInvitation>> PrepareAsync(
        string? rawToken,
        CancellationToken cancellationToken)
    {
        if (!tokenService.TryHash(rawToken, out var hash))
            return new(InvitationOperationStatus.NotFound);
        var invitation = await dbContext.HouseholdInvitations
            .Include(candidate => candidate.Household)
            .SingleOrDefaultAsync(candidate => candidate.TokenHash == hash, cancellationToken);
        var pending = await PendingResultAsync(invitation, cancellationToken);
        return pending.Status == InvitationOperationStatus.Success
            ? new(InvitationOperationStatus.Success, new PreparedInvitation(invitation!.Id, pending.Value!))
            : new(pending.Status);
    }

    public async Task<InvitationOperationResult<PendingInvitationResponse>> GetPendingAsync(
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        var invitation = await dbContext.HouseholdInvitations
            .Include(candidate => candidate.Household)
            .SingleOrDefaultAsync(candidate => candidate.Id == invitationId, cancellationToken);
        return await PendingResultAsync(invitation, cancellationToken);
    }

    public async Task<InvitationOperationResult<AcceptedInvitationResponse>> AcceptAsync(
        Guid invitationId,
        Guid userAccountId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaximumAttempts; attempt++)
        {
            try
            {
                return await AcceptAttemptAsync(invitationId, userAccountId, sessionId, cancellationToken);
            }
            catch (Exception exception) when (IsConcurrencyConflict(exception))
            {
                dbContext.ChangeTracker.Clear();
                if (attempt == MaximumAttempts - 1)
                    return new(InvitationOperationStatus.Conflict);
                await Task.Delay(TimeSpan.FromMilliseconds(25 * (attempt + 1)), timeProvider, cancellationToken);
            }
        }
        throw new InvalidOperationException("Invitation acceptance retry was unexpectedly exhausted.");
    }

    private async Task<InvitationOperationResult<AcceptedInvitationResponse>> AcceptAttemptAsync(
        Guid invitationId,
        Guid userAccountId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var invitation = await dbContext.HouseholdInvitations
            .Include(candidate => candidate.Household)
            .SingleOrDefaultAsync(candidate => candidate.Id == invitationId, cancellationToken);
        if (invitation is null) return new(InvitationOperationStatus.NotFound);
        if (!invitation.Household.IsActive) return new(InvitationOperationStatus.NotFound);
        var now = timeProvider.GetUtcNow();
        if (invitation.Status == HouseholdInvitationStatus.Pending && invitation.ExpiresAt <= now)
        {
            invitation.Status = HouseholdInvitationStatus.Expired;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(InvitationOperationStatus.Expired);
        }
        if (invitation.Status == HouseholdInvitationStatus.Revoked)
            return new(InvitationOperationStatus.Revoked);
        if (invitation.Status == HouseholdInvitationStatus.Expired)
            return new(InvitationOperationStatus.Expired);

        var account = await dbContext.UserAccounts.SingleOrDefaultAsync(candidate =>
            candidate.Id == userAccountId && candidate.IsActive,
            cancellationToken);
        var session = await dbContext.UserSessions.SingleOrDefaultAsync(candidate =>
            candidate.Id == sessionId
            && candidate.UserAccountId == userAccountId
            && candidate.RevokedAt == null
            && candidate.ExpiresAt > now
            && candidate.AbsoluteExpiresAt > now,
            cancellationToken);
        if (account is null || session is null)
            return new(InvitationOperationStatus.SessionUnavailable);
        if (!string.Equals(
                account.PrimaryEmail.Trim(),
                invitation.IntendedEmailNormalized,
                StringComparison.OrdinalIgnoreCase))
            return new(InvitationOperationStatus.EmailMismatch);

        var membership = await dbContext.HouseholdMemberships
            .Include(candidate => candidate.HouseholdMember)
            .SingleOrDefaultAsync(candidate =>
                candidate.HouseholdId == invitation.HouseholdId
                && candidate.UserAccountId == userAccountId,
                cancellationToken);
        if (invitation.Status == HouseholdInvitationStatus.Accepted)
        {
            if (invitation.AcceptedByUserAccountId != userAccountId || membership is null)
                return new(InvitationOperationStatus.Used);
            session.SelectedHouseholdId = invitation.HouseholdId;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Success(invitation, membership, reused: true);
        }

        var reused = membership is not null;
        if (membership is null)
        {
            var member = new HouseholdMember
            {
                HouseholdId = invitation.HouseholdId,
                DisplayName = account.DisplayName,
                Role = HouseholdMemberRole.Adult,
                CreatedAt = now,
                UpdatedAt = now,
            };
            membership = new HouseholdMembership
            {
                HouseholdId = invitation.HouseholdId,
                UserAccountId = account.Id,
                HouseholdMemberId = member.Id,
                HouseholdMember = member,
                UserAccount = account,
                Household = invitation.Household,
                CreatedAt = now,
            };
            dbContext.HouseholdMembers.Add(member);
            dbContext.HouseholdMemberships.Add(membership);
        }
        else if (!membership.HouseholdMember.IsActive)
        {
            membership.HouseholdMember.IsActive = true;
            membership.HouseholdMember.UpdatedAt = now;
        }
        membership.HouseholdMember.Role = HouseholdMemberRole.Adult;

        invitation.Status = HouseholdInvitationStatus.Accepted;
        invitation.AcceptedAt = now;
        invitation.AcceptedByUserAccountId = userAccountId;
        session.SelectedHouseholdId = invitation.HouseholdId;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Success(invitation, membership, reused);
    }

    private static InvitationOperationResult<AcceptedInvitationResponse> Success(
        HouseholdInvitation invitation,
        HouseholdMembership membership,
        bool reused) => new(
            InvitationOperationStatus.Success,
            new AcceptedInvitationResponse(
                new AcceptedInvitationHouseholdResponse(
                    invitation.HouseholdId,
                    invitation.Household.Name,
                    membership.HouseholdMemberId,
                    "adult"),
                invitation.HouseholdId,
                reused));

    private async Task<InvitationOperationResult<PendingInvitationResponse>> PendingResultAsync(
        HouseholdInvitation? invitation,
        CancellationToken cancellationToken)
    {
        if (invitation is null) return new(InvitationOperationStatus.NotFound);
        if (!invitation.Household.IsActive) return new(InvitationOperationStatus.NotFound);
        var now = timeProvider.GetUtcNow();
        if (invitation.Status == HouseholdInvitationStatus.Pending && invitation.ExpiresAt <= now)
        {
            invitation.Status = HouseholdInvitationStatus.Expired;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        return invitation.Status switch
        {
            HouseholdInvitationStatus.Pending => new(
                InvitationOperationStatus.Success,
                new PendingInvitationResponse(
                    invitation.Household.Name,
                    MaskEmail(invitation.IntendedEmailNormalized),
                    invitation.ExpiresAt)),
            HouseholdInvitationStatus.Expired => new(InvitationOperationStatus.Expired),
            HouseholdInvitationStatus.Revoked => new(InvitationOperationStatus.Revoked),
            HouseholdInvitationStatus.Accepted => new(InvitationOperationStatus.Used),
            _ => throw new InvalidOperationException("Unsupported invitation status."),
        };
    }

    private async Task ExpirePendingAsync(Guid householdId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var invitations = await dbContext.HouseholdInvitations.Where(invitation =>
            invitation.HouseholdId == householdId
            && invitation.Status == HouseholdInvitationStatus.Pending
            && invitation.ExpiresAt <= now).ToListAsync(cancellationToken);
        foreach (var invitation in invitations) invitation.Status = HouseholdInvitationStatus.Expired;
        if (invitations.Count > 0) await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static HouseholdInvitationResponse Map(HouseholdInvitation invitation) => new(
        invitation.Id,
        invitation.HouseholdId,
        invitation.IntendedEmailNormalized,
        invitation.Status.ToString().ToLowerInvariant(),
        invitation.CreatedAt,
        invitation.ExpiresAt,
        invitation.AcceptedAt,
        invitation.RevokedAt);

    private static string MaskEmail(string email)
    {
        var separator = email.IndexOf('@');
        if (separator <= 0) return "•••";
        var local = email[..separator];
        return $"{local[0]}{new string('•', Math.Min(5, Math.Max(2, local.Length - 1)))}{email[separator..]}";
    }

    private static bool IsConcurrencyConflict(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres
                && postgres.SqlState is PostgresErrorCodes.UniqueViolation
                    or PostgresErrorCodes.SerializationFailure)
                return true;
        }
        return false;
    }
}

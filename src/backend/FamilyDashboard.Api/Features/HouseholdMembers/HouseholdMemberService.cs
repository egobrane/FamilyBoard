using System.Data;
using FamilyDashboard.Api.Domain.Households;
using FamilyDashboard.Api.Features.Households;
using FamilyDashboard.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FamilyDashboard.Api.Features.HouseholdMembers;

internal enum HouseholdMemberUpdateStatus
{
    Success,
    NotFound,
    LastActiveAdult,
    SelfDeactivationRequiresLeaveFlow,
    Conflict,
}

internal sealed record HouseholdMemberUpdateResult(
    HouseholdMemberUpdateStatus Status,
    HouseholdMemberResponse? Member = null);

internal sealed class HouseholdMemberService(FamilyDashboardDbContext dbContext)
{
    public async Task<IReadOnlyList<HouseholdMemberResponse>> ListAsync(
        Guid householdId,
        CancellationToken cancellationToken)
    {
        return await dbContext.HouseholdMembers
            .AsNoTracking()
            .Where(member => member.HouseholdId == householdId)
            .OrderByDescending(member => member.IsActive)
            .ThenBy(member => member.DisplayName)
            .ThenBy(member => member.Id)
            .Select(member => Map(member))
            .ToListAsync(cancellationToken);
    }

    public async Task<HouseholdMemberResponse?> CreateChildAsync(
        Guid householdId,
        string displayName,
        string? avatarColor,
        CancellationToken cancellationToken)
    {
        var householdExists = await dbContext.Households.AnyAsync(
            household => household.Id == householdId && household.IsActive,
            cancellationToken);
        if (!householdExists)
        {
            return null;
        }

        var member = new HouseholdMember
        {
            HouseholdId = householdId,
            DisplayName = displayName,
            Role = HouseholdMemberRole.Child,
            AvatarColor = avatarColor,
        };
        dbContext.HouseholdMembers.Add(member);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(member);
    }

    public async Task<HouseholdMemberUpdateResult> UpdateAsync(
        Guid householdId,
        Guid memberId,
        Guid actorUserAccountId,
        ValidatedHouseholdMemberPatch patch,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var member = await dbContext.HouseholdMembers
                .Include(candidate => candidate.Membership!)
                .ThenInclude(membership => membership.UserAccount)
                .SingleOrDefaultAsync(
                    candidate => candidate.Id == memberId
                        && candidate.HouseholdId == householdId,
                    cancellationToken);
            if (member is null)
            {
                return new HouseholdMemberUpdateResult(HouseholdMemberUpdateStatus.NotFound);
            }

            var deactivatesLinkedAdult = member.IsActive
                && patch.IsActive == false
                && member.Role == HouseholdMemberRole.Adult
                && member.Membership?.UserAccount.IsActive == true;
            if (deactivatesLinkedAdult)
            {
                if (member.Membership!.UserAccountId == actorUserAccountId)
                {
                    return new HouseholdMemberUpdateResult(
                        HouseholdMemberUpdateStatus.SelfDeactivationRequiresLeaveFlow);
                }

                var activeAdultCount = await dbContext.HouseholdMembers.CountAsync(
                    candidate => candidate.HouseholdId == householdId
                        && candidate.IsActive
                        && candidate.Role == HouseholdMemberRole.Adult
                        && candidate.Membership != null
                        && candidate.Membership.UserAccount.IsActive,
                    cancellationToken);
                if (activeAdultCount <= 1)
                {
                    return new HouseholdMemberUpdateResult(
                        HouseholdMemberUpdateStatus.LastActiveAdult);
                }
            }

            if (patch.DisplayName is not null)
            {
                member.DisplayName = patch.DisplayName;
            }

            if (patch.AvatarColor is not null)
            {
                member.AvatarColor = patch.AvatarColor;
            }

            if (patch.IsActive is not null)
            {
                member.IsActive = patch.IsActive.Value;
            }

            member.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new HouseholdMemberUpdateResult(
                HouseholdMemberUpdateStatus.Success,
                Map(member));
        }
        catch (PostgresException exception)
            when (exception.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            return new HouseholdMemberUpdateResult(HouseholdMemberUpdateStatus.Conflict);
        }
    }

    private static HouseholdMemberResponse Map(HouseholdMember member)
    {
        return new HouseholdMemberResponse(
            member.Id,
            member.DisplayName,
            HouseholdContractRoles.FromDomain(member.Role),
            member.AvatarColor,
            member.IsActive);
    }
}

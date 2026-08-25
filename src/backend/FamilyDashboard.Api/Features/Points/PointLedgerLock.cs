using System.Buffers.Binary;
using FamilyDashboard.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyDashboard.Api.Features.Points;

public sealed class PointLedgerLock(FamilyDashboardDbContext dbContext)
{
    public Task AcquireAsync(Guid householdId, Guid memberId, CancellationToken cancellationToken)
    {
        Span<byte> householdBytes = stackalloc byte[16];
        Span<byte> memberBytes = stackalloc byte[16];
        householdId.TryWriteBytes(householdBytes);
        memberId.TryWriteBytes(memberBytes);
        var householdKey = BinaryPrimitives.ReadInt32LittleEndian(householdBytes[..4]);
        var memberKey = BinaryPrimitives.ReadInt32LittleEndian(memberBytes[..4]);
        return dbContext.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock({0}, {1})", [householdKey, memberKey], cancellationToken);
    }
}

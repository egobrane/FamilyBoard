using FamilyDashboard.Api.Features.Chores;
using FamilyDashboard.Api.Domain.Chores;

namespace FamilyDashboard.Api.Tests.Chores;

public sealed class ChoreDueTimeServiceTests
{
    private static readonly TimeProvider Time = new FixedTimeProvider(new DateTimeOffset(2026, 8, 22, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void ResolvesHouseholdLocalTimeToUtc()
    {
        var service = new ChoreDueTimeService(Time);
        Assert.True(service.TryResolve(new DateOnly(2026, 8, 23), new TimeOnly(18, 30),
            "America/New_York", out var dueAt, out var error));
        Assert.Null(error);
        Assert.Equal(new DateTimeOffset(2026, 8, 23, 22, 30, 0, TimeSpan.Zero), dueAt);
    }

    [Fact]
    public void RejectsDaylightSavingGapAndAmbiguity()
    {
        var service = new ChoreDueTimeService(Time);
        Assert.False(service.TryResolve(new DateOnly(2027, 3, 14), new TimeOnly(2, 30),
            "America/New_York", out _, out _));
        Assert.False(service.TryResolve(new DateOnly(2026, 11, 1), new TimeOnly(1, 30),
            "America/New_York", out _, out _));
    }

    [Fact]
    public void RecurringTimesResolveDaylightSavingEdgesDeterministically()
    {
        var service = new ChoreDueTimeService(Time);
        Assert.True(service.TryResolveRecurring(new DateOnly(2027, 3, 14), new TimeOnly(2, 30),
            "America/New_York", out var gap, out var gapResolution, out _));
        Assert.Equal(ChoreDueTimeResolution.ShiftedForward, gapResolution);
        Assert.Equal(new DateTimeOffset(2027, 3, 14, 7, 0, 0, TimeSpan.Zero), gap);
        Assert.True(service.TryResolveRecurring(new DateOnly(2026, 11, 1), new TimeOnly(1, 30),
            "America/New_York", out var ambiguous, out var ambiguousResolution, out _));
        Assert.Equal(ChoreDueTimeResolution.AmbiguousEarlier, ambiguousResolution);
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 5, 30, 0, TimeSpan.Zero), ambiguous);
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}

using FamilyDashboard.Api.Domain.Chores;
using FamilyDashboard.Api.Features.Chores;

namespace FamilyDashboard.Api.Tests.Chores;

public sealed class ChoreRecurrenceCalculatorTests
{
    private readonly ChoreRecurrenceCalculator calculator = new();

    [Fact]
    public void SupportsDailyIntervals()
    {
        var start = new DateOnly(2026, 8, 24);
        Assert.Equal(start.AddDays(2), calculator.FindNext(
            ChoreRecurrenceKind.Daily, 2, null, start, null, start.AddDays(1)));
    }

    [Fact]
    public void SupportsSelectedWeekdaysAndWeeklyIntervals()
    {
        var start = new DateOnly(2026, 8, 24); // Monday
        const int mondayAndWednesday = 1 | 4;
        Assert.Equal(new DateOnly(2026, 8, 26), calculator.FindNext(
            ChoreRecurrenceKind.Weekly, 2, mondayAndWednesday, start, null, start.AddDays(1)));
        Assert.Equal(new DateOnly(2026, 9, 7), calculator.FindNext(
            ChoreRecurrenceKind.Weekly, 2, mondayAndWednesday, start, null, new DateOnly(2026, 8, 27)));
    }

    [Fact]
    public void HonorsInclusiveEndDate()
    {
        var start = new DateOnly(2026, 8, 24);
        Assert.Null(calculator.FindNext(ChoreRecurrenceKind.Daily, 1, null,
            start, start.AddDays(2), start.AddDays(3)));
    }
}

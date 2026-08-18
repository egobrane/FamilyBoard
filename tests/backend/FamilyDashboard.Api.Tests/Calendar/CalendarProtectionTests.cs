using FamilyDashboard.Api.Configuration;
using FamilyDashboard.Api.Features.Calendar;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FamilyDashboard.Api.Tests.Calendar;

public sealed class CalendarProtectionTests
{
    [Fact]
    public void OAuthTokensUseConnectionAndTokenKindSpecificProtection()
    {
        using var services = new ServiceCollection().AddDataProtection().Services.BuildServiceProvider();
        var protector = new CalendarTokenProtector(services.GetRequiredService<IDataProtectionProvider>());
        var connectionId = Guid.NewGuid();

        var protectedAccess = protector.Protect(connectionId, "access-token", "raw-access-token");
        var protectedRefresh = protector.Protect(connectionId, "refresh-token", "raw-refresh-token");

        Assert.DoesNotContain("raw-access-token", protectedAccess);
        Assert.Equal("raw-access-token", protector.Unprotect(connectionId, "access-token", protectedAccess));
        Assert.Equal("raw-refresh-token", protector.Unprotect(connectionId, "refresh-token", protectedRefresh));
        Assert.ThrowsAny<Exception>(() => protector.Unprotect(connectionId, "refresh-token", protectedAccess));
        Assert.ThrowsAny<Exception>(() => protector.Unprotect(Guid.NewGuid(), "access-token", protectedAccess));
    }

    [Fact]
    public void AuthorizationStateAndCursorAreBoundAndTamperEvident()
    {
        using var services = new ServiceCollection().AddDataProtection().Services.BuildServiceProvider();
        var protector = new CalendarStateProtector(
            services.GetRequiredService<IDataProtectionProvider>(),
            TimeProvider.System,
            Options.Create(new GoogleCalendarConfiguration()));
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var householdId = Guid.NewGuid();

        var authorization = protector.CreateAuthorization(
            userId, sessionId, householdId, $"/households/{householdId}/calendars");

        Assert.True(protector.TryReadAuthorization(authorization.State, out var state));
        Assert.Equal(userId, state!.UserAccountId);
        Assert.Equal(sessionId, state.UserSessionId);
        Assert.Equal(householdId, state.HouseholdId);
        Assert.False(protector.TryReadAuthorization(authorization.State + "tampered", out _));

        var cursor = protector.CreateCursor(new CalendarPageCursor(
            householdId,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(1),
            new Dictionary<Guid, string?> { [Guid.NewGuid()] = "provider-page-token" },
            DateTimeOffset.UtcNow.AddMinutes(10)));
        Assert.DoesNotContain("provider-page-token", cursor);
        Assert.True(protector.TryReadCursor(cursor, out var decoded));
        Assert.Equal(householdId, decoded!.HouseholdId);
    }
}

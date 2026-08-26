using FamilyDashboard.Api.Configuration;
using FamilyDashboard.Api.Features.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FamilyDashboard.Api.Tests.Tasks;

public sealed class TasksProtectionTests
{
    [Fact]
    public void OAuthTokensAreConnectionAndKindSpecific()
    {
        using var services = new ServiceCollection().AddDataProtection().Services.BuildServiceProvider();
        var protector = new TasksTokenProtector(services.GetRequiredService<IDataProtectionProvider>());
        var connectionId = Guid.NewGuid();
        var protectedAccess = protector.Protect(connectionId, "access-token", "raw-access");
        var protectedRefresh = protector.Protect(connectionId, "refresh-token", "raw-refresh");
        Assert.DoesNotContain("raw-access", protectedAccess);
        Assert.Equal("raw-access", protector.Unprotect(connectionId, "access-token", protectedAccess));
        Assert.Equal("raw-refresh", protector.Unprotect(connectionId, "refresh-token", protectedRefresh));
        Assert.ThrowsAny<Exception>(() => protector.Unprotect(connectionId, "refresh-token", protectedAccess));
    }

    [Fact]
    public void AuthorizationStateAndPageCursorAreTamperEvident()
    {
        using var services = new ServiceCollection().AddDataProtection().Services.BuildServiceProvider();
        var protector = new TasksStateProtector(
            services.GetRequiredService<IDataProtectionProvider>(), TimeProvider.System,
            Options.Create(new GoogleTasksConfiguration()));
        var userId = Guid.NewGuid(); var sessionId = Guid.NewGuid(); var householdId = Guid.NewGuid();
        var authorization = protector.CreateAuthorization(userId, sessionId, householdId,
            $"/households/{householdId:D}/tasks");
        Assert.True(protector.TryReadAuthorization(authorization.State, out var state));
        Assert.Equal(userId, state!.UserAccountId);
        Assert.False(protector.TryReadAuthorization(authorization.State + "tampered", out _));
        var sourceId = Guid.NewGuid();
        var cursor = protector.CreateCursor(new TasksPageCursor(householdId, false,
            new Dictionary<Guid, string> { [sourceId] = "private-provider-token" },
            DateTimeOffset.UtcNow.AddMinutes(10)));
        Assert.DoesNotContain("private-provider-token", cursor);
        Assert.True(protector.TryReadCursor(cursor, out var decoded));
        Assert.Equal("private-provider-token", decoded!.RemainingSources[sourceId]);
    }
}

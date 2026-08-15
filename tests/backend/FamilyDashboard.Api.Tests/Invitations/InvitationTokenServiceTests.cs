using FamilyDashboard.Api.Features.Invitations;

namespace FamilyDashboard.Api.Tests.Invitations;

public sealed class InvitationTokenServiceTests
{
    [Fact]
    public void TokensAreRandomUrlSafeAndHashToTheStoredValue()
    {
        var service = new InvitationTokenService();

        var first = service.Create();
        var second = service.Create();

        Assert.NotEqual(first.Token, second.Token);
        Assert.Equal(43, first.Token.Length);
        Assert.DoesNotContain('=', first.Token);
        Assert.True(service.TryHash(first.Token, out var hash));
        Assert.Equal(first.Hash, hash);
        Assert.False(service.TryHash("not-a-valid-token", out _));
    }
}

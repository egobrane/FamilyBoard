using FamilyDashboard.Api.Configuration;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace FamilyDashboard.Api.Features.Invitations;

public sealed class PendingInvitationCookieService
{
    public const string CookieName = "__Host-FamilyDashboard.PendingInvitation";

    private readonly IDataProtector _protector;
    private readonly InvitationConfiguration _configuration;
    private readonly TimeProvider _timeProvider;

    public PendingInvitationCookieService(
        IDataProtectionProvider dataProtectionProvider,
        IOptions<InvitationConfiguration> configuration,
        TimeProvider timeProvider)
    {
        _protector = dataProtectionProvider.CreateProtector(
            "FamilyDashboard.PendingInvitation.v1");
        _configuration = configuration.Value;
        _timeProvider = timeProvider;
    }

    public void Set(HttpResponse response, Guid invitationId, DateTimeOffset invitationExpiresAt)
    {
        var expiresAt = Min(
            _timeProvider.GetUtcNow().Add(_configuration.PendingCookieLifetime),
            invitationExpiresAt);
        var value = _protector.Protect($"{invitationId:D}|{expiresAt.ToUnixTimeSeconds()}");
        response.Cookies.Append(CookieName, value, CookieOptions(expiresAt));
    }

    public bool TryRead(HttpRequest request, out Guid invitationId)
    {
        invitationId = Guid.Empty;
        if (!request.Cookies.TryGetValue(CookieName, out var value))
        {
            return false;
        }

        try
        {
            var parts = _protector.Unprotect(value).Split('|');
            return parts.Length == 2
                && Guid.TryParse(parts[0], out invitationId)
                && long.TryParse(parts[1], out var expiresAt)
                && DateTimeOffset.FromUnixTimeSeconds(expiresAt) > _timeProvider.GetUtcNow();
        }
        catch (Exception exception) when (
            exception is CryptographicException or FormatException or ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    public void Delete(HttpResponse response) =>
        response.Cookies.Delete(CookieName, CookieOptions(null));

    private static CookieOptions CookieOptions(DateTimeOffset? expiresAt) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        IsEssential = true,
        Path = "/",
        Expires = expiresAt,
    };

    private static DateTimeOffset Min(DateTimeOffset first, DateTimeOffset second) =>
        first <= second ? first : second;
}

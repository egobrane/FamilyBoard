using System.Security.Claims;
using System.Text.Encodings.Web;
using FamilyDashboard.Api.Security;
using FamilyDashboard.Api.Domain.Identity;
using FamilyDashboard.Api.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FamilyDashboard.Api.Tests.Authentication;

internal sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IServiceProvider services)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string UserIdHeaderName = "X-Test-User-Id";
    public const string SessionIdHeaderName = "X-Test-Session-Id";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserIdHeaderName, out var values))
        {
            return AuthenticateResult.NoResult();
        }

        if (!Guid.TryParse(values.ToString(), out var userAccountId))
        {
            return AuthenticateResult.Fail(
                $"{UserIdHeaderName} must contain a valid user account identifier.");
        }

        var claims = new List<Claim>
        {
            new(FamilyDashboardClaimTypes.UserAccountId, userAccountId.ToString()),
        };
        if (Request.Headers.TryGetValue(SessionIdHeaderName, out var sessionValues)
            && Guid.TryParse(sessionValues.ToString(), out var sessionId))
        {
            claims.Add(new Claim(FamilyDashboardClaimTypes.UserSessionId, sessionId.ToString()));
        }
        else if (services.GetService<FamilyDashboardDbContext>() is { } dbContext)
        {
            var now = DateTimeOffset.UtcNow;
            var session = new UserSession
            {
                UserAccountId = userAccountId,
                CreatedAt = now,
                LastSeenAt = now,
                ExpiresAt = now.AddDays(1),
                AbsoluteExpiresAt = now.AddDays(2),
            };
            dbContext.UserSessions.Add(session);
            try
            {
                await dbContext.SaveChangesAsync(Context.RequestAborted);
                claims.Add(new Claim(FamilyDashboardClaimTypes.UserSessionId, session.Id.ToString()));
            }
            catch
            {
                dbContext.Entry(session).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
            }
        }
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return AuthenticateResult.Success(ticket);
    }
}

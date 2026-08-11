using System.Security.Claims;
using System.Text.Encodings.Web;
using FamilyDashboard.Api.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FamilyDashboard.Api.Tests.Authentication;

internal sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string UserIdHeaderName = "X-Test-User-Id";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserIdHeaderName, out var values))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!Guid.TryParse(values.ToString(), out var userAccountId))
        {
            return Task.FromResult(AuthenticateResult.Fail(
                $"{UserIdHeaderName} must contain a valid user account identifier."));
        }

        var identity = new ClaimsIdentity(
            [new Claim(FamilyDashboardClaimTypes.UserAccountId, userAccountId.ToString())],
            SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

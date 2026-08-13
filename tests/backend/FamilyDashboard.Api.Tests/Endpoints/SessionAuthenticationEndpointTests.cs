using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using FamilyDashboard.Api.Domain.Identity;
using FamilyDashboard.Api.Features.Authentication;
using FamilyDashboard.Api.Features.Common;
using FamilyDashboard.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using FamilyAuthenticationSchemes = FamilyDashboard.Api.Features.Authentication.AuthenticationSchemes;

namespace FamilyDashboard.Api.Tests.Endpoints;

[Collection("PostgreSQL integration")]
public sealed class SessionAuthenticationEndpointTests
{
    [PostgreSqlFact]
    public async Task CurrentUserUsesDatabaseBackedCookieSession()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var (account, session) = await SeedSessionAsync(database);
        using var factory = new CookieSessionWebApplicationFactory(ConnectionString());
        using var client = CreateClient(factory);
        using var request = AuthenticatedRequest(factory, session, HttpMethod.Get, "/api/auth/me");

        using var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var currentUser = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
        Assert.NotNull(currentUser);
        Assert.Equal(account.Id, currentUser.User.Id);
        Assert.NotNull(currentUser.Session);
        Assert.Equal(session.ExpiresAt, currentUser.Session.ExpiresAt);
        Assert.False(currentUser.Session.IsSharedDisplay);
    }

    [PostgreSqlFact]
    public async Task RevokedExpiredAndDisabledSessionsFailClosed()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var (account, session) = await SeedSessionAsync(database);
        using var factory = new CookieSessionWebApplicationFactory(ConnectionString());
        using var client = CreateClient(factory);

        session.RevokedAt = DateTimeOffset.UtcNow;
        await database.DbContext.SaveChangesAsync();
        using var revokedRequest = AuthenticatedRequest(factory, session, HttpMethod.Get, "/api/auth/me");
        using var revokedResponse = await client.SendAsync(revokedRequest);
        await AssertAuthenticationRequiredAsync(revokedResponse);

        session.RevokedAt = null;
        session.CreatedAt = DateTimeOffset.UtcNow.AddDays(-2);
        session.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await database.DbContext.SaveChangesAsync();
        using var expiredRequest = AuthenticatedRequest(factory, session, HttpMethod.Get, "/api/auth/me");
        using var expiredResponse = await client.SendAsync(expiredRequest);
        await AssertAuthenticationRequiredAsync(expiredResponse);

        session.ExpiresAt = DateTimeOffset.UtcNow.AddDays(1);
        account.IsActive = false;
        await database.DbContext.SaveChangesAsync();
        using var disabledRequest = AuthenticatedRequest(factory, session, HttpMethod.Get, "/api/auth/me");
        using var disabledResponse = await client.SendAsync(disabledRequest);
        await AssertAuthenticationRequiredAsync(disabledResponse);
    }

    [PostgreSqlFact]
    public async Task LogoutRequiresAntiforgeryAndRevokesSession()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var (_, session) = await SeedSessionAsync(database);
        using var factory = new CookieSessionWebApplicationFactory(ConnectionString());
        using var client = CreateClient(factory);
        var sessionCookie = CreateSessionCookie(factory, session);

        using var missingToken = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        missingToken.Headers.TryAddWithoutValidation("Cookie", sessionCookie);
        using var missingTokenResponse = await client.SendAsync(missingToken);
        Assert.Equal(HttpStatusCode.BadRequest, missingTokenResponse.StatusCode);

        using var tokenRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/antiforgery");
        tokenRequest.Headers.TryAddWithoutValidation("Cookie", sessionCookie);
        using var tokenResponse = await client.SendAsync(tokenRequest);
        tokenResponse.EnsureSuccessStatusCode();
        var token = await tokenResponse.Content.ReadFromJsonAsync<AntiforgeryTokenResponse>();
        Assert.NotNull(token);
        var antiforgeryCookie = tokenResponse.Headers.GetValues("Set-Cookie")
            .Select(value => value.Split(';', 2)[0])
            .Single(value => value.StartsWith("__Host-FamilyDashboard.Antiforgery=", StringComparison.Ordinal));

        using var logout = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logout.Headers.TryAddWithoutValidation("Cookie", $"{sessionCookie}; {antiforgeryCookie}");
        logout.Headers.TryAddWithoutValidation(token.HeaderName, token.RequestToken);
        using var logoutResponse = await client.SendAsync(logout);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        await database.DbContext.Entry(session).ReloadAsync();
        Assert.NotNull(session.RevokedAt);
    }

    [Fact]
    public async Task DisabledGoogleLoginReturnsServiceUnavailableProblem()
    {
        using var factory = new CookieSessionWebApplicationFactory("Host=localhost;Database=unused");
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/api/auth/login/google?returnUrl=/");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            ApiProblemCodes.AuthenticationUnavailable,
            document.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task CredentialedCorsAllowsOnlyTheConfiguredFrontend()
    {
        using var factory = new CookieSessionWebApplicationFactory("Host=localhost;Database=unused");
        using var client = CreateClient(factory);
        using var allowed = Preflight("https://family.egobrane.net");
        using var allowedResponse = await client.SendAsync(allowed);
        Assert.Equal(HttpStatusCode.NoContent, allowedResponse.StatusCode);
        Assert.Equal(
            "https://family.egobrane.net",
            Assert.Single(allowedResponse.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Equal(
            "true",
            Assert.Single(allowedResponse.Headers.GetValues("Access-Control-Allow-Credentials")));

        using var denied = Preflight("https://evil.example");
        using var deniedResponse = await client.SendAsync(denied);
        Assert.False(deniedResponse.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(deniedResponse.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task GoogleLoginUsesIdentityScopesAndRejectsExternalReturnUrls()
    {
        using var factory = new CookieSessionWebApplicationFactory(
            "Host=localhost;Database=unused",
            enableGoogle: true);
        var google = factory.Services
            .GetRequiredService<IOptionsMonitor<GoogleOptions>>()
            .Get(FamilyAuthenticationSchemes.Google);
        Assert.Equal(["openid", "profile", "email"], google.Scope);
        Assert.False(google.SaveTokens);
        Assert.Equal("online", google.AccessType);

        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });
        using var challenge = await client.GetAsync("/api/auth/login/google?returnUrl=/households");
        Assert.Equal(HttpStatusCode.Redirect, challenge.StatusCode);
        Assert.Equal("accounts.google.com", challenge.Headers.Location!.Host);

        using var invalid = await client.GetAsync(
            "/api/auth/login/google?returnUrl=https%3A%2F%2Fevil.example");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    private static HttpClient CreateClient(CookieSessionWebApplicationFactory factory) =>
        factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
            BaseAddress = new Uri("https://localhost"),
        });

    private static HttpRequestMessage Preflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/auth/logout");
        request.Headers.TryAddWithoutValidation("Origin", origin);
        request.Headers.TryAddWithoutValidation("Access-Control-Request-Method", "POST");
        request.Headers.TryAddWithoutValidation("Access-Control-Request-Headers", "X-CSRF-TOKEN");
        return request;
    }

    private static HttpRequestMessage AuthenticatedRequest(
        CookieSessionWebApplicationFactory factory,
        UserSession session,
        HttpMethod method,
        string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("Cookie", CreateSessionCookie(factory, session));
        return request;
    }

    private static string CreateSessionCookie(
        CookieSessionWebApplicationFactory factory,
        UserSession session)
    {
        var options = factory.Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(FamilyAuthenticationSchemes.ApplicationCookie);
        var properties = new AuthenticationProperties
        {
            IsPersistent = true,
            IssuedUtc = session.CreatedAt,
            ExpiresUtc = session.ExpiresAt,
        };
        var ticket = new AuthenticationTicket(
            UserSessionService.CreatePrincipal(session),
            properties,
            FamilyAuthenticationSchemes.ApplicationCookie);
        return $"{options.Cookie.Name}={options.TicketDataFormat.Protect(ticket)}";
    }

    private static async Task<(UserAccount Account, UserSession Session)> SeedSessionAsync(
        PostgreSqlTestDatabase database)
    {
        var now = DateTimeOffset.UtcNow;
        var account = new UserAccount
        {
            DisplayName = "Alex Adult",
            PrimaryEmail = "alex@example.test",
        };
        var session = new UserSession
        {
            UserAccount = account,
            CreatedAt = now,
            LastSeenAt = now,
            ExpiresAt = now.AddDays(14),
            AbsoluteExpiresAt = now.AddDays(30),
        };
        database.DbContext.AddRange(account, session);
        await database.DbContext.SaveChangesAsync();
        return (account, session);
    }

    private static string ConnectionString() =>
        Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION_STRING")!;

    private static async Task AssertAuthenticationRequiredAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            ApiProblemCodes.AuthenticationRequired,
            document.RootElement.GetProperty("code").GetString());
    }
}

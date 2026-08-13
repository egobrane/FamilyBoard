using Azure.Core;
using Azure.Identity;
using FamilyDashboard.Api.Configuration;
using FamilyDashboard.Api.Features.Common;
using FamilyDashboard.Api.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace FamilyDashboard.Api.Features.Authentication;

public static class AuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddFamilyDashboardAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var authenticationConfiguration = configuration
            .GetSection(AuthenticationConfiguration.SectionName)
            .Get<AuthenticationConfiguration>() ?? new AuthenticationConfiguration();
        var dataProtectionConfiguration = configuration
            .GetSection(DataProtectionConfiguration.SectionName)
            .Get<DataProtectionConfiguration>() ?? new DataProtectionConfiguration();

        services.AddOptions<AuthenticationConfiguration>()
            .Bind(configuration.GetSection(AuthenticationConfiguration.SectionName));
        services.AddOptions<DataProtectionConfiguration>()
            .Bind(configuration.GetSection(DataProtectionConfiguration.SectionName));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<UserSessionService>();
        services.AddScoped<GoogleSignInService>();
        services.AddScoped<AntiforgeryEndpointFilter>();

        var dataProtection = services.AddDataProtection()
            .SetApplicationName(dataProtectionConfiguration.ApplicationName);
        if (dataProtectionConfiguration.UseAzure)
        {
            if (string.IsNullOrWhiteSpace(dataProtectionConfiguration.BlobUri)
                || string.IsNullOrWhiteSpace(dataProtectionConfiguration.KeyIdentifier))
            {
                throw new InvalidOperationException(
                    "Azure Data Protection requires BlobUri and KeyIdentifier.");
            }

            TokenCredential credential = string.IsNullOrWhiteSpace(
                dataProtectionConfiguration.ManagedIdentityClientId)
                ? new DefaultAzureCredential()
                : new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(
                    dataProtectionConfiguration.ManagedIdentityClientId));
            dataProtection
                .PersistKeysToAzureBlobStorage(
                    new Uri(dataProtectionConfiguration.BlobUri),
                    credential)
                .ProtectKeysWithAzureKeyVault(
                    new Uri(dataProtectionConfiguration.KeyIdentifier),
                    credential);
        }

        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "__Host-FamilyDashboard.Antiforgery";
            options.Cookie.Path = "/";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.IsEssential = true;
        });

        var authentication = services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = AuthenticationSchemes.ApplicationCookie;
                options.DefaultChallengeScheme = AuthenticationSchemes.ApplicationCookie;
                options.DefaultForbidScheme = AuthenticationSchemes.ApplicationCookie;
                options.DefaultSignInScheme = AuthenticationSchemes.ApplicationCookie;
            })
            .AddCookie(AuthenticationSchemes.ApplicationCookie, options =>
            {
                options.Cookie.Name = "__Host-FamilyDashboard.Session";
                options.Cookie.Path = "/";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.IsEssential = true;
                options.SlidingExpiration = false;
                options.Events.OnValidatePrincipal = ValidateSessionAsync;
                options.Events.OnRedirectToLogin = SuppressRedirectAsync;
                options.Events.OnRedirectToAccessDenied = SuppressAccessDeniedRedirectAsync;
            })
            .AddCookie(AuthenticationSchemes.ExternalCookie, options =>
            {
                options.Cookie.Name = "__Host-FamilyDashboard.External";
                options.Cookie.Path = "/";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.IsEssential = true;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
                options.SlidingExpiration = false;
            });

        if (authenticationConfiguration.Google.Enabled)
        {
            if (string.IsNullOrWhiteSpace(authenticationConfiguration.Google.ClientId)
                || string.IsNullOrWhiteSpace(authenticationConfiguration.Google.ClientSecret))
            {
                throw new InvalidOperationException(
                    "Google authentication is enabled but its client configuration is incomplete.");
            }

            authentication.AddGoogle(AuthenticationSchemes.Google, options =>
            {
                options.SignInScheme = AuthenticationSchemes.ExternalCookie;
                options.ClientId = authenticationConfiguration.Google.ClientId;
                options.ClientSecret = authenticationConfiguration.Google.ClientSecret;
                options.CallbackPath = "/api/auth/callback/google";
                options.SaveTokens = false;
                options.AccessType = "online";
                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");
                options.ClaimActions.MapJsonKey(
                    GoogleSignInService.EmailVerifiedClaim,
                    "email_verified");
                options.CorrelationCookie.Name = "__Host-FamilyDashboard.Google.Correlation.";
                options.CorrelationCookie.Path = "/";
                options.CorrelationCookie.HttpOnly = true;
                options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
                options.CorrelationCookie.SameSite = SameSiteMode.None;
                options.CorrelationCookie.IsEssential = true;
                options.Events.OnRemoteFailure = context =>
                {
                    context.HandleResponse();
                    context.Response.Redirect(
                        $"{authenticationConfiguration.FrontendOrigin.TrimEnd('/')}/auth/error?code={ApiProblemCodes.AuthenticationFailed}");
                    return Task.CompletedTask;
                };
            });
        }

        return services;
    }

    private static async Task ValidateSessionAsync(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        if (principal is null
            || !principal.TryGetUserAccountId(out var userAccountId)
            || !principal.TryGetUserSessionId(out var sessionId))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(AuthenticationSchemes.ApplicationCookie);
            return;
        }

        var sessionService = context.HttpContext.RequestServices
            .GetRequiredService<UserSessionService>();
        var result = await sessionService.ValidateAndRenewAsync(
            sessionId,
            userAccountId,
            context.HttpContext.RequestAborted);
        if (!result.IsValid || result.Session is null)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(AuthenticationSchemes.ApplicationCookie);
            return;
        }

        if (result.WasRenewed)
        {
            context.ShouldRenew = true;
            context.Properties.ExpiresUtc = result.Session.ExpiresAt;
        }
    }

    private static Task SuppressRedirectAsync(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    private static Task SuppressAccessDeniedRedirectAsync(
        RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }
}

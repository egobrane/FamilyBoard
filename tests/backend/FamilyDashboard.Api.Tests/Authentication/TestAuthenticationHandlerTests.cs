using FamilyDashboard.Api.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyDashboard.Api.Tests.Authentication;

public sealed class TestAuthenticationHandlerTests
{
    [Fact]
    public async Task MissingHeaderDoesNotAuthenticate()
    {
        await using var services = CreateTestServices();
        var context = CreateHttpContext(services);

        var result = await context.AuthenticateAsync(TestAuthenticationHandler.SchemeName);

        Assert.False(result.Succeeded);
        Assert.True(result.None);
    }

    [Fact]
    public async Task MalformedHeaderFailsAuthentication()
    {
        await using var services = CreateTestServices();
        var context = CreateHttpContext(services);
        context.Request.Headers[TestAuthenticationHandler.UserIdHeaderName] = "not-a-guid";

        var result = await context.AuthenticateAsync(TestAuthenticationHandler.SchemeName);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failure);
    }

    [Fact]
    public async Task ValidHeaderCreatesOnlyTheInternalUserAccountClaim()
    {
        await using var services = CreateTestServices();
        var context = CreateHttpContext(services);
        var userAccountId = Guid.NewGuid();
        context.Request.Headers[TestAuthenticationHandler.UserIdHeaderName] = userAccountId.ToString();

        var result = await context.AuthenticateAsync(TestAuthenticationHandler.SchemeName);

        Assert.True(result.Succeeded);
        var claim = Assert.Single(result.Principal!.Claims);
        Assert.Equal(FamilyDashboardClaimTypes.UserAccountId, claim.Type);
        Assert.Equal(userAccountId.ToString(), claim.Value);
    }

    [Fact]
    public async Task ProductionApplicationDoesNotRegisterTheTestScheme()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var schemeProvider = factory.Services.GetService<IAuthenticationSchemeProvider>();

        if (schemeProvider is null)
        {
            return;
        }

        var schemes = await schemeProvider.GetAllSchemesAsync();
        Assert.DoesNotContain(schemes, scheme => scheme.Name == TestAuthenticationHandler.SchemeName);
    }

    private static ServiceProvider CreateTestServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services
            .AddAuthentication(TestAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                TestAuthenticationHandler.SchemeName,
                _ => { });

        return services.BuildServiceProvider();
    }

    private static DefaultHttpContext CreateHttpContext(IServiceProvider services)
    {
        return new DefaultHttpContext
        {
            RequestServices = services,
        };
    }
}

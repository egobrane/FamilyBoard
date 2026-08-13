using FamilyDashboard.Api.Configuration;
using FamilyDashboard.Api.Features.Authentication;
using FamilyDashboard.Api.Features.HouseholdMembers;
using FamilyDashboard.Api.Features.Households;
using FamilyDashboard.Api.Persistence;
using FamilyDashboard.Api.Security;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddFamilyDashboardAuthentication(builder.Configuration);
builder.Services.AddFamilyDashboardAuthorization();
builder.Services.AddScoped<HouseholdService>();
builder.Services.AddScoped<HouseholdMemberService>();

var corsOptions = builder.Configuration
    .GetSection(CorsOptions.SectionName)
    .Get<CorsOptions>() ?? new CorsOptions();

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsOptions.PolicyName, policy =>
    {
        if (corsOptions.AllowedOrigins.Length > 0)
        {
            policy
                .WithOrigins(corsOptions.AllowedOrigins)
                .WithHeaders("Content-Type", "X-CSRF-TOKEN")
                .WithMethods("GET", "POST", "PATCH")
                .AllowCredentials();
        }
    });
});

var connectionString = builder.Configuration.GetConnectionString("FamilyDashboard") ?? string.Empty;
builder.Services.AddDbContext<FamilyDashboardDbContext>(options => options.UseNpgsql(connectionString));
builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<FamilyDashboardDbContext>("postgresql", tags: ["ready"])
    .AddCheck<DataProtectionHealthCheck>("data-protection", tags: ["ready"]);

var app = builder.Build();

if (args.Contains("--migrate", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<FamilyDashboardDbContext>();
    await dbContext.Database.MigrateAsync();
    return;
}

app.UseExceptionHandler();
app.UseStatusCodePages(async statusCodeContext =>
{
    var httpContext = statusCodeContext.HttpContext;
    var (code, title) = httpContext.Response.StatusCode switch
    {
        StatusCodes.Status401Unauthorized =>
            (FamilyDashboard.Api.Features.Common.ApiProblemCodes.AuthenticationRequired,
                "Authentication is required."),
        StatusCodes.Status403Forbidden =>
            (FamilyDashboard.Api.Features.Common.ApiProblemCodes.AdultAccessRequired,
                "Adult household access is required."),
        _ => (FamilyDashboard.Api.Features.Common.ApiProblemCodes.UnexpectedError,
            "The request could not be completed."),
    };
    var problem = FamilyDashboard.Api.Features.Common.ApiProblems.Create(
        httpContext,
        httpContext.Response.StatusCode,
        code,
        title);
    await Results.Problem(problem).ExecuteAsync(httpContext);
});
app.UseCors(CorsOptions.PolicyName);
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
});
app.MapAuthenticationEndpoints();
app.MapHouseholdEndpoints();
app.MapHouseholdMemberEndpoints();

await app.RunAsync();

public partial class Program;

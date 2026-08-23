using FamilyDashboard.Api.Configuration;
using FamilyDashboard.Api.Features.Authentication;
using FamilyDashboard.Api.Features.Calendar;
using FamilyDashboard.Api.Features.Chores;
using FamilyDashboard.Api.Features.HouseholdMembers;
using FamilyDashboard.Api.Features.Households;
using FamilyDashboard.Api.Features.Invitations;
using FamilyDashboard.Api.Features.ParentAccess;
using FamilyDashboard.Api.Persistence;
using FamilyDashboard.Api.Security;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddFamilyDashboardAuthentication(builder.Configuration);
builder.Services.AddFamilyDashboardAuthorization();
builder.Services.AddScoped<HouseholdService>();
builder.Services.AddScoped<HouseholdMemberService>();
builder.Services.AddScoped<InvitationService>();
builder.Services.AddSingleton<InvitationTokenService>();
builder.Services.AddSingleton<PendingInvitationCookieService>();
builder.Services.AddScoped<ParentAccessService>();
builder.Services.AddSingleton<ParentPinHasher>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient(nameof(GoogleCalendarProviderClient));
builder.Services.AddScoped<IGoogleCalendarProviderClient, GoogleCalendarProviderClient>();
builder.Services.AddScoped<GoogleCalendarService>();
builder.Services.AddScoped<ChoreService>();
builder.Services.AddScoped<ChoreScheduleService>();
builder.Services.AddScoped<ChoreAssignmentGenerator>();
builder.Services.AddSingleton<ChoreRecurrenceCalculator>();
builder.Services.AddSingleton<ChoreDueTimeService>();
builder.Services.AddSingleton<CalendarTokenProtector>();
builder.Services.AddSingleton<CalendarStateProtector>();
builder.Services.AddSingleton<CalendarCorrelationCookieService>();
builder.Services.AddOptions<GoogleCalendarConfiguration>()
    .Bind(builder.Configuration.GetSection(GoogleCalendarConfiguration.SectionName))
    .Validate(options => (!options.EventCreationEnabled || options.Enabled) && (!options.Enabled || (
        !string.IsNullOrWhiteSpace(options.ClientId)
        && !string.IsNullOrWhiteSpace(options.ClientSecret)
        && Uri.TryCreate(options.CallbackUrl, UriKind.Absolute, out var callback)
        && (callback.Scheme == Uri.UriSchemeHttps
            || (callback.Scheme == Uri.UriSchemeHttp && callback.IsLoopback))
        && options.AuthorizationLifetime > TimeSpan.Zero
        && options.FreshCacheLifetime > TimeSpan.Zero
        && options.StaleCacheLifetime >= options.FreshCacheLifetime
        && options.MaximumCalendarsPerHousehold is > 0 and <= 100
        && options.MaximumEventsPerRequest is > 0 and <= 2500)),
        "Google Calendar configuration is incomplete or invalid.")
    .ValidateOnStart();
builder.Services.AddOptions<ParentAccessConfiguration>()
    .Bind(builder.Configuration.GetSection(ParentAccessConfiguration.SectionName))
    .Validate(options =>
        options.PepperVersion > 0
        && options.PinLength >= 6
        && options.WorkFactor > 0
        && options.ElevationLifetime > TimeSpan.Zero
        && options.RecentAuthenticationLifetime > TimeSpan.Zero
        && options.MaximumFailures > 0
        && options.FailureWindow > TimeSpan.Zero
        && options.LockoutLifetime > TimeSpan.Zero,
        "Parent access policy values are invalid.");
builder.Services.AddOptions<InvitationConfiguration>()
    .Bind(builder.Configuration.GetSection(InvitationConfiguration.SectionName))
    .Validate(
        options => options.Lifetime > TimeSpan.Zero && options.PendingCookieLifetime > TimeSpan.Zero,
        "Invitation lifetimes must be positive.")
    .ValidateOnStart();
builder.Services.AddOptions<ChoreGenerationConfiguration>()
    .Bind(builder.Configuration.GetSection(ChoreGenerationConfiguration.SectionName))
    .Validate(options => options.HorizonHours is >= 1 and <= 168
        && options.MaximumAssignmentsPerRun is >= 1 and <= 1000,
        "Chore generation values are invalid.")
    .ValidateOnStart();

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
                .WithMethods("GET", "POST", "PUT", "PATCH")
                .AllowCredentials();
        }
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        var isCalendarCreation = context.HttpContext.Request.Path.Value?.EndsWith(
            "/calendar/events", StringComparison.Ordinal) == true;
        var isChoreCompletion = context.HttpContext.Request.Path.Value?.EndsWith(
            "/completions", StringComparison.Ordinal) == true;
        var problem = FamilyDashboard.Api.Features.Common.ApiProblems.Create(
            context.HttpContext,
            StatusCodes.Status429TooManyRequests,
            isCalendarCreation
                ? FamilyDashboard.Api.Features.Common.ApiProblemCodes.CalendarEventCreationRateLimited
                : isChoreCompletion
                    ? FamilyDashboard.Api.Features.Common.ApiProblemCodes.ChoreCompletionRateLimited
                : FamilyDashboard.Api.Features.Common.ApiProblemCodes.ParentPinRateLimited,
            isCalendarCreation
                ? "Too many calendar events were submitted. Try again shortly."
                : isChoreCompletion
                    ? "Too many chore completions were submitted. Try again shortly."
                : "Too many parent PIN attempts. Try again shortly.");
        problem.Extensions["retryAfterSeconds"] = 60;
        await Results.Problem(problem).ExecuteAsync(context.HttpContext);
    };
    options.AddPolicy("parent-pin-verification", context =>
    {
        var sessionId = context.User.FindFirst(FamilyDashboardClaimTypes.UserSessionId)?.Value
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";
        var householdId = context.Request.RouteValues["householdId"]?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            $"{sessionId}:{householdId}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            });
    });
    options.AddPolicy("calendar-event-creation", context =>
    {
        var sessionId = context.User.FindFirst(FamilyDashboardClaimTypes.UserSessionId)?.Value
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";
        var householdId = context.Request.RouteValues["householdId"]?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            $"calendar:{sessionId}:{householdId}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            });
    });
    options.AddPolicy("chore-completion", context =>
    {
        var sessionId = context.User.FindFirst(FamilyDashboardClaimTypes.UserSessionId)?.Value
            ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var householdId = context.Request.RouteValues["householdId"]?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            $"chore:{sessionId}:{householdId}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            });
    });
});

var connectionString = builder.Configuration.GetConnectionString("FamilyDashboard") ?? string.Empty;
builder.Services.AddDbContext<FamilyDashboardDbContext>(options => options.UseNpgsql(connectionString));
builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<FamilyDashboardDbContext>("postgresql", tags: ["ready"])
    .AddCheck<DataProtectionHealthCheck>("data-protection", tags: ["ready"])
    .AddCheck<ParentAccessConfigurationHealthCheck>("parent-access", tags: ["ready"]);

var app = builder.Build();

if (args.Contains("--migrate", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<FamilyDashboardDbContext>();
    await dbContext.Database.MigrateAsync();
    return;
}

if (args.Contains("--generate-chore-assignments", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    var generator = scope.ServiceProvider.GetRequiredService<ChoreAssignmentGenerator>();
    await generator.GenerateAsync(CancellationToken.None);
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
app.UseRateLimiter();
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
app.MapInvitationEndpoints();
app.MapParentAccessEndpoints();
app.MapGoogleCalendarEndpoints();
app.MapChoreEndpoints();

await app.RunAsync();

public partial class Program;

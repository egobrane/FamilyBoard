using FamilyDashboard.Api.Configuration;
using FamilyDashboard.Api.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

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
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

var connectionString = builder.Configuration.GetConnectionString("FamilyDashboard") ?? string.Empty;
builder.Services.AddDbContext<FamilyDashboardDbContext>(options => options.UseNpgsql(connectionString));
builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<FamilyDashboardDbContext>("postgresql", tags: ["ready"]);

var app = builder.Build();

if (args.Contains("--migrate", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<FamilyDashboardDbContext>();
    await dbContext.Database.MigrateAsync();
    return;
}

app.UseExceptionHandler();
app.UseCors(CorsOptions.PolicyName);

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

await app.RunAsync();

public partial class Program;

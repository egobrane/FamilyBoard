using FamilyDashboard.Api.Domain.Chores;
using FamilyDashboard.Api.Domain.Households;
using FamilyDashboard.Api.Domain.Identity;
using FamilyDashboard.Api.Domain.Integrations;
using FamilyDashboard.Api.Domain.Rewards;
using Microsoft.EntityFrameworkCore;

namespace FamilyDashboard.Api.Persistence;

public sealed class FamilyDashboardDbContext(DbContextOptions<FamilyDashboardDbContext> options)
    : DbContext(options)
{
    public DbSet<Household> Households => Set<Household>();
    public DbSet<HouseholdConfiguration> HouseholdConfigurations => Set<HouseholdConfiguration>();
    public DbSet<HouseholdMember> HouseholdMembers => Set<HouseholdMember>();
    public DbSet<HouseholdMembership> HouseholdMemberships => Set<HouseholdMembership>();
    public DbSet<HouseholdInvitation> HouseholdInvitations => Set<HouseholdInvitation>();
    public DbSet<HouseholdAccessPin> HouseholdAccessPins => Set<HouseholdAccessPin>();
    public DbSet<ParentAccessAuditEvent> ParentAccessAuditEvents => Set<ParentAccessAuditEvent>();
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<ExternalIdentity> ExternalIdentities => Set<ExternalIdentity>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<GoogleCalendarConnection> GoogleCalendarConnections => Set<GoogleCalendarConnection>();
    public DbSet<HouseholdCalendarSource> HouseholdCalendarSources => Set<HouseholdCalendarSource>();
    public DbSet<CalendarEventCreationReceipt> CalendarEventCreationReceipts => Set<CalendarEventCreationReceipt>();
    public DbSet<ApplicationPreference> ApplicationPreferences => Set<ApplicationPreference>();
    public DbSet<ChoreDefinition> ChoreDefinitions => Set<ChoreDefinition>();
    public DbSet<ChoreSchedule> ChoreSchedules => Set<ChoreSchedule>();
    public DbSet<ChoreAssignment> ChoreAssignments => Set<ChoreAssignment>();
    public DbSet<ChoreCompletion> ChoreCompletions => Set<ChoreCompletion>();
    public DbSet<PointTransaction> PointTransactions => Set<PointTransaction>();
    public DbSet<Reward> Rewards => Set<Reward>();
    public DbSet<RewardRedemption> RewardRedemptions => Set<RewardRedemption>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FamilyDashboardDbContext).Assembly);
    }
}

namespace FamilyDashboard.Api.Configuration;

public sealed class ChoreGenerationConfiguration
{
    public const string SectionName = "ChoreGeneration";
    public int HorizonHours { get; set; } = 36;
    public int MaximumAssignmentsPerRun { get; set; } = 100;
}

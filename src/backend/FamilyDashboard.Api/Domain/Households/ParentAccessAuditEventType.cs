namespace FamilyDashboard.Api.Domain.Households;

public enum ParentAccessAuditEventType
{
    PinSetup,
    PinChanged,
    PinRecovered,
    VerificationSucceeded,
    VerificationFailed,
    CooldownStarted,
    ExplicitlyLocked,
    SharedDisplayEnabled,
    SharedDisplayDisabled,
}

public enum ParentAccessAuditOutcome
{
    Succeeded,
    Rejected,
}

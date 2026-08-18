using Microsoft.AspNetCore.DataProtection;

namespace FamilyDashboard.Api.Features.Calendar;

public sealed class CalendarTokenProtector(IDataProtectionProvider provider)
{
    public string Protect(Guid connectionId, string tokenKind, string value) =>
        CreateProtector(connectionId, tokenKind).Protect(value);

    public string Unprotect(Guid connectionId, string tokenKind, string value) =>
        CreateProtector(connectionId, tokenKind).Unprotect(value);

    private IDataProtector CreateProtector(Guid connectionId, string tokenKind) =>
        provider.CreateProtector(
            "FamilyDashboard.GoogleCalendarIntegration.v1",
            connectionId.ToString("D"),
            tokenKind);
}

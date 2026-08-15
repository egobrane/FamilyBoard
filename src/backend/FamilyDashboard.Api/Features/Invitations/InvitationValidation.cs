using System.Net.Mail;

namespace FamilyDashboard.Api.Features.Invitations;

internal static class InvitationValidation
{
    public static bool TryNormalizeEmail(
        CreateInvitationRequest? request,
        out string? normalized,
        out IDictionary<string, string[]> errors)
    {
        errors = new Dictionary<string, string[]>();
        normalized = request?.IntendedEmail.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            errors["intendedEmail"] = ["An email address is required."];
            return false;
        }

        if (normalized.Length > 320)
        {
            errors["intendedEmail"] = ["The email address must be 320 characters or fewer."];
            return false;
        }

        try
        {
            var parsed = new MailAddress(normalized);
            if (!string.Equals(parsed.Address, normalized, StringComparison.OrdinalIgnoreCase))
            {
                throw new FormatException();
            }
        }
        catch (FormatException)
        {
            errors["intendedEmail"] = ["Enter a valid email address."];
            return false;
        }

        return true;
    }
}

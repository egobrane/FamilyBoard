namespace FamilyDashboard.Api.Features.Authentication;

public static class ReturnUrlValidator
{
    public const int MaximumLength = 2048;

    public static bool TryNormalize(string? returnUrl, out string normalized)
    {
        normalized = "/";
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return true;
        }

        if (returnUrl.Length > MaximumLength
            || returnUrl[0] != '/'
            || returnUrl.StartsWith("//", StringComparison.Ordinal)
            || returnUrl.Contains('\\')
            || returnUrl.Any(char.IsControl))
        {
            return false;
        }

        normalized = returnUrl;
        return true;
    }
}

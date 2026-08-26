using System.Security.Cryptography;
using System.Text;

namespace FamilyDashboard.Api.Features.Tasks;

public sealed class TasksCorrelationCookieService
{
    public const string CookieName = "__Secure-FamilyDashboard.Tasks.Correlation";
    public const string CallbackPath = "/api/integrations/google-tasks/callback";

    public void Write(HttpResponse response, string state, DateTimeOffset expiresAt)
    {
        response.Cookies.Append(CookieName, Hash(state), new CookieOptions
        {
            Path = CallbackPath,
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Expires = expiresAt,
        });
    }

    public bool ValidateAndDelete(HttpRequest request, HttpResponse response, string state)
    {
        var valid = request.Cookies.TryGetValue(CookieName, out var expected)
            && CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(expected),
                Encoding.ASCII.GetBytes(Hash(state)));
        response.Cookies.Delete(CookieName, new CookieOptions
        {
            Path = CallbackPath,
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
        });
        return valid;
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

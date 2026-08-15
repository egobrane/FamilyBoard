using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace FamilyDashboard.Api.Features.Invitations;

public sealed class InvitationTokenService
{
    public (string Token, byte[] Hash) Create()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return (WebEncoders.Base64UrlEncode(bytes), SHA256.HashData(bytes));
    }

    public bool TryHash(string? token, out byte[] hash)
    {
        hash = [];
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            var bytes = WebEncoders.Base64UrlDecode(token.Trim());
            if (bytes.Length != 32)
            {
                return false;
            }

            hash = SHA256.HashData(bytes);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

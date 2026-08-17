using System.Security.Cryptography;
using System.Text;
using FamilyDashboard.Api.Configuration;
using FamilyDashboard.Api.Domain.Households;
using Microsoft.Extensions.Options;

namespace FamilyDashboard.Api.Features.ParentAccess;

public sealed class ParentPinHasher(IOptions<ParentAccessConfiguration> options)
{
    public const short CurrentHashVersion = 1;
    private readonly ParentAccessConfiguration _configuration = options.Value;

    public bool IsAvailable => TryGetPepper(out _);

    public HouseholdAccessPin Create(
        Guid householdId,
        Guid actorUserAccountId,
        string pin,
        DateTimeOffset now)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        return new HouseholdAccessPin
        {
            HouseholdId = householdId,
            PinHash = Derive(pin, salt, _configuration.WorkFactor),
            Salt = salt,
            HashVersion = CurrentHashVersion,
            WorkFactor = _configuration.WorkFactor,
            PepperVersion = _configuration.PepperVersion,
            CreatedAt = now,
            ChangedAt = now,
            ChangedByUserAccountId = actorUserAccountId,
        };
    }

    public bool Verify(HouseholdAccessPin stored, string pin)
    {
        if (stored.HashVersion != CurrentHashVersion
            || stored.PepperVersion != _configuration.PepperVersion
            || stored.Salt.Length != 16
            || stored.PinHash.Length != 32)
        {
            return false;
        }

        var candidate = Derive(pin, stored.Salt, stored.WorkFactor);
        return CryptographicOperations.FixedTimeEquals(candidate, stored.PinHash);
    }

    public bool NeedsUpgrade(HouseholdAccessPin stored) =>
        stored.HashVersion != CurrentHashVersion
        || stored.PepperVersion != _configuration.PepperVersion
        || stored.WorkFactor < _configuration.WorkFactor;

    public void Upgrade(
        HouseholdAccessPin stored,
        Guid actorUserAccountId,
        string pin,
        DateTimeOffset now)
    {
        var replacement = Create(stored.HouseholdId, actorUserAccountId, pin, now);
        stored.PinHash = replacement.PinHash;
        stored.Salt = replacement.Salt;
        stored.HashVersion = replacement.HashVersion;
        stored.WorkFactor = replacement.WorkFactor;
        stored.PepperVersion = replacement.PepperVersion;
        stored.ChangedAt = now;
        stored.ChangedByUserAccountId = actorUserAccountId;
    }

    private byte[] Derive(string pin, byte[] salt, int workFactor)
    {
        if (!TryGetPepper(out var pepper))
        {
            throw new InvalidOperationException("Parent access is not securely configured.");
        }

        var pinBytes = Encoding.UTF8.GetBytes(pin);
        try
        {
            using var hmac = new HMACSHA256(pepper);
            var pepperedPin = hmac.ComputeHash(pinBytes);
            try
            {
                return Rfc2898DeriveBytes.Pbkdf2(
                    pepperedPin,
                    salt,
                    workFactor,
                    HashAlgorithmName.SHA256,
                    32);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(pepperedPin);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pinBytes);
            CryptographicOperations.ZeroMemory(pepper);
        }
    }

    private bool TryGetPepper(out byte[] pepper)
    {
        pepper = [];
        if (!_configuration.Enabled || string.IsNullOrWhiteSpace(_configuration.Pepper))
        {
            return false;
        }

        try
        {
            pepper = Convert.FromBase64String(_configuration.Pepper);
            if (pepper.Length == 32 && _configuration.PepperVersion > 0)
            {
                return true;
            }
            CryptographicOperations.ZeroMemory(pepper);
            pepper = [];
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

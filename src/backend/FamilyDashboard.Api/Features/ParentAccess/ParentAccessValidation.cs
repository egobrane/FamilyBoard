using FamilyDashboard.Api.Configuration;

namespace FamilyDashboard.Api.Features.ParentAccess;

public static class ParentAccessValidation
{
    public static bool TryValidatePin(
        string? pin,
        ParentAccessConfiguration configuration,
        out IDictionary<string, string[]> errors)
    {
        errors = new Dictionary<string, string[]>();
        if (pin is null
            || pin.Length != configuration.PinLength
            || pin.Any(character => character is < '0' or > '9'))
        {
            errors["pin"] = [$"The parent PIN must contain exactly {configuration.PinLength} digits."];
        }

        return errors.Count == 0;
    }

    public static bool TryValidateDeviceLabel(
        string? deviceLabel,
        out string? normalized,
        out IDictionary<string, string[]> errors)
    {
        normalized = string.IsNullOrWhiteSpace(deviceLabel) ? null : deviceLabel.Trim();
        errors = new Dictionary<string, string[]>();
        if (normalized?.Length > 80)
        {
            errors["deviceLabel"] = ["The device label cannot exceed 80 characters."];
        }
        return errors.Count == 0;
    }
}

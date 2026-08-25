namespace FamilyDashboard.Api.Features.Points;

public static class PointValidation
{
    public static bool TryAdjustment(CreatePointAdjustmentRequest? request,
        out CreatePointAdjustmentRequest? clean, out Dictionary<string, string[]> errors)
    {
        errors = [];
        clean = null;
        if (request is null)
        {
            errors["adjustment"] = ["Adjustment data is required."];
            return false;
        }
        if (request.ClientRequestId == Guid.Empty)
            errors["clientRequestId"] = ["A client request ID is required."];
        if (request.HouseholdMemberId == Guid.Empty)
            errors["householdMemberId"] = ["A household member is required."];
        if (request.Amount == 0 || Math.Abs((long)request.Amount) > 10000)
            errors["amount"] = ["Amount must be between -10,000 and 10,000 and cannot be zero."];
        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length is < 1 or > 240)
            errors["reason"] = ["Reason must contain between 1 and 240 characters."];
        clean = request with { Reason = reason };
        return errors.Count == 0;
    }

    public static bool TryReversal(ReversePointTransactionRequest? request,
        out ReversePointTransactionRequest? clean, out Dictionary<string, string[]> errors)
    {
        errors = [];
        clean = null;
        if (request is null)
        {
            errors["reversal"] = ["Reversal data is required."];
            return false;
        }
        if (request.ClientRequestId == Guid.Empty)
            errors["clientRequestId"] = ["A client request ID is required."];
        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length is < 1 or > 240)
            errors["reason"] = ["Reason must contain between 1 and 240 characters."];
        clean = request with { Reason = reason };
        return errors.Count == 0;
    }
}

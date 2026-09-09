namespace FamilyDashboard.Api.Features.Points;

public static class PointValidation
{
    public static bool TryDeductionDefinition(Guid requestId, string? title, int deductionAmount,
        out CreatePointDeductionDefinitionRequest? clean,
        out Dictionary<string, string[]> errors)
    {
        errors = [];
        var normalizedTitle = title?.Trim() ?? string.Empty;
        if (requestId == Guid.Empty)
            errors["clientRequestId"] = ["A request ID is required."];
        if (normalizedTitle.Length is < 1 or > 120)
            errors["title"] = ["Title must be between 1 and 120 characters."];
        if (deductionAmount is < 1 or > 10000)
            errors["deductionAmount"] = ["Deduction amount must be between 1 and 10,000 points."];
        clean = errors.Count == 0
            ? new CreatePointDeductionDefinitionRequest(requestId, normalizedTitle, deductionAmount)
            : null;
        return clean is not null;
    }

    public static bool TryDeductionApplication(ApplyPointDeductionRequest? request,
        out Dictionary<string, string[]> errors)
    {
        errors = [];
        if (request is null)
        {
            errors["deduction"] = ["Deduction application data is required."];
            return false;
        }
        if (request.ClientRequestId == Guid.Empty)
            errors["clientRequestId"] = ["A client request ID is required."];
        if (request.HouseholdMemberId == Guid.Empty)
            errors["householdMemberId"] = ["An active household member is required."];
        if (request.ExpectedDefinitionVersion < 1)
            errors["expectedDefinitionVersion"] = ["A valid definition version is required."];
        return errors.Count == 0;
    }

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

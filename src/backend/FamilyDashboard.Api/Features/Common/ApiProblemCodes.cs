using Microsoft.AspNetCore.Mvc;

namespace FamilyDashboard.Api.Features.Common;

public static class ApiProblemCodes
{
    public const string AuthenticationRequired = "authentication_required";
    public const string AuthenticationUnavailable = "authentication_unavailable";
    public const string AuthenticationFailed = "authentication_failed";
    public const string InvalidReturnUrl = "invalid_return_url";
    public const string AntiforgeryValidationFailed = "antiforgery_validation_failed";
    public const string AccountUnavailable = "account_unavailable";
    public const string HouseholdNotFound = "household_not_found";
    public const string HouseholdMemberNotFound = "household_member_not_found";
    public const string AdultAccessRequired = "adult_access_required";
    public const string LastActiveAdult = "last_active_adult";
    public const string SelfDeactivationRequiresLeaveFlow = "self_deactivation_requires_leave_flow";
    public const string ValidationFailed = "validation_failed";
    public const string Conflict = "conflict";
    public const string ActiveInvitationExists = "active_invitation_exists";
    public const string InvitationNotFound = "invitation_not_found";
    public const string InvitationExpired = "invitation_expired";
    public const string InvitationRevoked = "invitation_revoked";
    public const string InvitationUsed = "invitation_used";
    public const string InvitationUnavailable = "invitation_unavailable";
    public const string InvitationEmailMismatch = "invitation_email_mismatch";
    public const string InvitationOriginNotAllowed = "invitation_origin_not_allowed";
    public const string InvitationConflict = "invitation_conflict";
    public const string ParentAccessUnavailable = "parent_access_unavailable";
    public const string ParentElevationRequired = "parent_elevation_required";
    public const string ParentPinNotConfigured = "parent_pin_not_configured";
    public const string ParentPinAlreadyConfigured = "parent_pin_already_configured";
    public const string ParentPinInvalid = "parent_pin_invalid";
    public const string ParentPinLocked = "parent_pin_locked";
    public const string ParentPinRateLimited = "parent_pin_rate_limited";
    public const string RecentAuthenticationRequired = "recent_authentication_required";
    public const string PrivateSessionRequired = "private_session_required";
    public const string SharedDisplayRequiresPin = "shared_display_requires_pin";
    public const string ParentAccessConflict = "parent_access_conflict";
    public const string GoogleCalendarUnavailable = "google_calendar_unavailable";
    public const string CalendarConnectionRequired = "calendar_connection_required";
    public const string CalendarReauthorizationRequired = "calendar_reauthorization_required";
    public const string CalendarAuthorizationExpired = "calendar_authorization_expired";
    public const string CalendarAuthorizationDenied = "calendar_authorization_denied";
    public const string CalendarAuthorizationFailed = "calendar_authorization_failed";
    public const string CalendarOfflineAccessRequired = "calendar_offline_access_required";
    public const string CalendarScopeMissing = "calendar_scope_missing";
    public const string CalendarProviderUnavailable = "calendar_provider_unavailable";
    public const string CalendarProviderRateLimited = "calendar_provider_rate_limited";
    public const string CalendarSourceNotFound = "calendar_source_not_found";
    public const string CalendarSourceConflict = "calendar_source_conflict";
    public const string CalendarDisconnectConfirmationRequired = "calendar_disconnect_confirmation_required";
    public const string CalendarRangeInvalid = "calendar_range_invalid";
    public const string CalendarCursorInvalid = "calendar_cursor_invalid";
    public const string CalendarAccountMismatch = "calendar_account_mismatch";
    public const string CalendarEventCreationUnavailable = "calendar_event_creation_unavailable";
    public const string CalendarWriteAuthorizationRequired = "calendar_write_authorization_required";
    public const string CalendarEventCreationTargetInvalid = "calendar_event_creation_target_invalid";
    public const string CalendarIdempotencyConflict = "calendar_idempotency_conflict";
    public const string CalendarEventCreationRateLimited = "calendar_event_creation_rate_limited";
    public const string CalendarEventManagementUnavailable = "calendar_event_management_unavailable";
    public const string CalendarEventNotManaged = "calendar_event_not_managed";
    public const string CalendarEventNotFound = "calendar_event_not_found";
    public const string CalendarEventUnsupported = "calendar_event_unsupported";
    public const string CalendarEventVersionConflict = "calendar_event_version_conflict";
    public const string CalendarEventMutationIdempotencyConflict = "calendar_event_mutation_idempotency_conflict";
    public const string CalendarEventDeleteConfirmationRequired = "calendar_event_delete_confirmation_required";
    public const string CalendarEventWriteForbidden = "calendar_event_write_forbidden";
    public const string GoogleTasksUnavailable = "google_tasks_unavailable";
    public const string TasksConnectionRequired = "tasks_connection_required";
    public const string TasksReauthorizationRequired = "tasks_reauthorization_required";
    public const string TasksAuthorizationExpired = "tasks_authorization_expired";
    public const string TasksAuthorizationDenied = "tasks_authorization_denied";
    public const string TasksAuthorizationFailed = "tasks_authorization_failed";
    public const string TasksOfflineAccessRequired = "tasks_offline_access_required";
    public const string TasksScopeMissing = "tasks_scope_missing";
    public const string TasksProviderUnavailable = "tasks_provider_unavailable";
    public const string TasksProviderRateLimited = "tasks_provider_rate_limited";
    public const string TasksSourceNotFound = "tasks_source_not_found";
    public const string TasksSourceConflict = "tasks_source_conflict";
    public const string TasksDisconnectConfirmationRequired = "tasks_disconnect_confirmation_required";
    public const string TasksCursorInvalid = "tasks_cursor_invalid";
    public const string TasksAccountMismatch = "tasks_account_mismatch";
    public const string TasksWriteUnavailable = "tasks_write_unavailable";
    public const string TasksWriteAuthorizationRequired = "tasks_write_authorization_required";
    public const string TasksWriteTargetRequired = "tasks_write_target_required";
    public const string TasksWriteTargetConflict = "tasks_write_target_conflict";
    public const string TasksTaskNotFound = "tasks_task_not_found";
    public const string TasksTaskReadOnly = "tasks_task_read_only";
    public const string TasksTaskConflict = "tasks_task_conflict";
    public const string TasksIdempotencyConflict = "tasks_idempotency_conflict";
    public const string TasksMutationOutcomeUnknown = "tasks_mutation_outcome_unknown";
    public const string HouseholdSelectionRequired = "household_selection_required";
    public const string ChoreDefinitionNotFound = "chore_definition_not_found";
    public const string ChoreDefinitionInactive = "chore_definition_inactive";
    public const string ChoreAssignmentNotFound = "chore_assignment_not_found";
    public const string ChoreAssignmentNotActionable = "chore_assignment_not_actionable";
    public const string ChoreCompletionNotFound = "chore_completion_not_found";
    public const string ChoreCompletionPendingReview = "chore_completion_pending_review";
    public const string ChoreCompletionAlreadyReviewed = "chore_completion_already_reviewed";
    public const string ChoreMemberInactive = "chore_member_inactive";
    public const string ChoreIdempotencyConflict = "chore_idempotency_conflict";
    public const string ChoreConcurrencyConflict = "chore_concurrency_conflict";
    public const string ChoreCompletionRateLimited = "chore_completion_rate_limited";
    public const string ChoreScheduleNotFound = "chore_schedule_not_found";
    public const string ChoreScheduleInvalid = "invalid_chore_schedule";
    public const string ChoreScheduleDependencyInactive = "chore_schedule_dependency_inactive";
    public const string ChoreScheduleRequestConflict = "chore_schedule_request_conflict";
    public const string ChoreScheduleVersionConflict = "chore_schedule_version_conflict";
    public const string PointMemberNotFound = "point_member_not_found";
    public const string PointTransactionNotFound = "point_transaction_not_found";
    public const string PointIdempotencyConflict = "point_idempotency_conflict";
    public const string PointTransactionAlreadyReversed = "point_transaction_already_reversed";
    public const string PointTransactionNotReversible = "point_transaction_not_reversible";
    public const string PointConcurrencyConflict = "point_concurrency_conflict";
    public const string RewardNotFound = "reward_not_found";
    public const string RewardMemberNotFound = "reward_member_not_found";
    public const string RewardInactive = "reward_inactive";
    public const string RewardMemberInactive = "reward_member_inactive";
    public const string RewardInsufficientPoints = "reward_insufficient_points";
    public const string RewardIdempotencyConflict = "reward_idempotency_conflict";
    public const string RewardConcurrencyConflict = "reward_concurrency_conflict";
    public const string RewardRedemptionNotFound = "reward_redemption_not_found";
    public const string RewardRedemptionIdempotencyConflict = "reward_redemption_idempotency_conflict";
    public const string RewardRedemptionInvalidTransition = "reward_redemption_invalid_transition";
    public const string RewardRedemptionLegacyRequiresResolution = "reward_redemption_legacy_requires_resolution";
    public const string HouseholdMediaUnavailable = "household_media_unavailable";
    public const string DashboardPreferencesConflict = "dashboard_preferences_conflict";
    public const string WeatherUnavailable = "weather_unavailable";
    public const string WeatherProviderRateLimited = "weather_provider_rate_limited";
    public const string UnexpectedError = "unexpected_error";
}

public static class ApiProblems
{
    public static ProblemDetails Create(
        HttpContext httpContext,
        int status,
        string code,
        string title,
        string? detail = null,
        IDictionary<string, string[]>? errors = null)
    {
        ProblemDetails problem = errors is null
            ? new ProblemDetails()
            : new ValidationProblemDetails(errors);

        problem.Type = $"urn:family-dashboard:problem:{code}";
        problem.Title = title;
        problem.Status = status;
        problem.Detail = detail;
        problem.Instance = httpContext.Request.Path;
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        return problem;
    }
}

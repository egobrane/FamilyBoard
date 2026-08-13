using System.Data;
using System.Security.Claims;
using FamilyDashboard.Api.Domain.Identity;
using FamilyDashboard.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FamilyDashboard.Api.Features.Authentication;

public enum GoogleSignInStatus
{
    Success,
    InvalidIdentity,
    AccountDisabled,
}

public sealed record GoogleSignInResult(
    GoogleSignInStatus Status,
    UserAccount? Account = null,
    UserSession? Session = null);

public sealed class GoogleSignInService(
    FamilyDashboardDbContext dbContext,
    UserSessionService sessionService,
    TimeProvider timeProvider)
{
    private const int MaximumSignInAttempts = 4;
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromMilliseconds(25);

    public const string Provider = "google";
    public const string EmailVerifiedClaim = "family_dashboard:google:email_verified";

    public async Task<GoogleSignInResult> SignInAsync(
        ClaimsPrincipal externalPrincipal,
        CancellationToken cancellationToken)
    {
        var subject = externalPrincipal.FindFirstValue(ClaimTypes.NameIdentifier)?.Trim();
        var email = externalPrincipal.FindFirstValue(ClaimTypes.Email)?.Trim();
        var displayName = externalPrincipal.FindFirstValue(ClaimTypes.Name)?.Trim();
        var emailVerified = string.Equals(
            externalPrincipal.FindFirstValue(EmailVerifiedClaim),
            "true",
            StringComparison.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(subject)
            || string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(displayName)
            || !emailVerified)
        {
            return new GoogleSignInResult(GoogleSignInStatus.InvalidIdentity);
        }

        for (var attempt = 0; attempt < MaximumSignInAttempts; attempt++)
        {
            try
            {
                return await SignInAttemptAsync(
                    subject,
                    email,
                    displayName,
                    cancellationToken);
            }
            catch (Exception exception) when (
                attempt < MaximumSignInAttempts - 1 && IsConcurrencyConflict(exception))
            {
                dbContext.ChangeTracker.Clear();
                await Task.Delay(
                    InitialRetryDelay * (attempt + 1),
                    timeProvider,
                    cancellationToken);
            }
        }

        throw new InvalidOperationException("Google sign-in retry was unexpectedly exhausted.");
    }

    private async Task<GoogleSignInResult> SignInAttemptAsync(
        string subject,
        string email,
        string displayName,
        CancellationToken cancellationToken)
    {

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var now = timeProvider.GetUtcNow();
        var identity = await dbContext.ExternalIdentities
            .Include(candidate => candidate.UserAccount)
            .SingleOrDefaultAsync(candidate =>
                candidate.Provider == Provider && candidate.ProviderSubject == subject,
                cancellationToken);

        UserAccount account;
        if (identity is null)
        {
            account = new UserAccount
            {
                DisplayName = displayName,
                PrimaryEmail = email,
                CreatedAt = now,
                UpdatedAt = now,
            };
            identity = new ExternalIdentity
            {
                UserAccount = account,
                Provider = Provider,
                ProviderSubject = subject,
                Email = email,
                EmailVerified = true,
                LastLoginAt = now,
                CreatedAt = now,
            };
            dbContext.ExternalIdentities.Add(identity);
        }
        else
        {
            account = identity.UserAccount;
            if (!account.IsActive)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new GoogleSignInResult(GoogleSignInStatus.AccountDisabled);
            }

            account.DisplayName = displayName;
            account.PrimaryEmail = email;
            account.UpdatedAt = now;
            identity.Email = email;
            identity.EmailVerified = true;
            identity.LastLoginAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        var session = await sessionService.CreateAsync(
            account,
            isSharedDisplay: false,
            deviceLabel: null,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new GoogleSignInResult(GoogleSignInStatus.Success, account, session);
    }

    private static bool IsConcurrencyConflict(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres
                && postgres.SqlState is PostgresErrorCodes.UniqueViolation
                    or PostgresErrorCodes.SerializationFailure)
            {
                return true;
            }
        }

        return false;
    }
}

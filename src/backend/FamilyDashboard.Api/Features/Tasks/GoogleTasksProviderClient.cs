using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FamilyDashboard.Api.Configuration;
using Google;
using Google.Apis.Auth;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Tasks.v1;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace FamilyDashboard.Api.Features.Tasks;

public static class GoogleTasksScopes
{
    public const string OpenId = "openid";
    public const string Email = "email";
    public const string TasksReadOnly = "https://www.googleapis.com/auth/tasks.readonly";
    public static readonly string[] Required = [OpenId, Email, TasksReadOnly];
}

public sealed record GoogleTasksTokenResult(
    string AccessToken,
    string? RefreshToken,
    DateTimeOffset ExpiresAt,
    string[] Scopes,
    string ProviderSubject,
    string ProviderEmail);

public sealed record GoogleTasksRefreshResult(string AccessToken, DateTimeOffset ExpiresAt);
public sealed record GoogleProviderTaskList(string Id, string Name);
public sealed record GoogleProviderTask(
    string Id,
    string Title,
    string? Notes,
    string Status,
    string? DueDate,
    DateTimeOffset? CompletedAt,
    string? ParentTaskId,
    string Position,
    bool IsAssigned);
public sealed record GoogleProviderTaskPage(IReadOnlyList<GoogleProviderTask> Tasks, string? NextPageToken);

public enum GoogleTasksProviderFailure
{
    Unavailable,
    RateLimited,
    ReauthorizationRequired,
    InvalidResponse,
}

public sealed class GoogleTasksProviderException(
    GoogleTasksProviderFailure failure,
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
    public GoogleTasksProviderFailure Failure { get; } = failure;
}

public interface IGoogleTasksProviderClient
{
    string CreateAuthorizationUrl(string state);
    Task<GoogleTasksTokenResult> ExchangeCodeAsync(string code, CancellationToken cancellationToken);
    Task<GoogleTasksRefreshResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
    Task<IReadOnlyList<GoogleProviderTaskList>> ListTaskListsAsync(
        string accessToken, CancellationToken cancellationToken);
    Task<GoogleProviderTaskPage> ListTasksAsync(
        string accessToken, string taskListId, bool includeCompleted,
        string? pageToken, int maximumResults, CancellationToken cancellationToken);
    Task RevokeAsync(string token, CancellationToken cancellationToken);
}

public sealed class GoogleTasksProviderClient(
    IHttpClientFactory httpClientFactory,
    IOptions<GoogleTasksConfiguration> options,
    TimeProvider timeProvider) : IGoogleTasksProviderClient
{
    private readonly GoogleTasksConfiguration _configuration = options.Value;

    public string CreateAuthorizationUrl(string state)
    {
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = _configuration.ClientId,
            ["redirect_uri"] = _configuration.CallbackUrl,
            ["response_type"] = "code",
            ["scope"] = string.Join(' ', GoogleTasksScopes.Required),
            ["access_type"] = "offline",
            ["include_granted_scopes"] = "true",
            ["prompt"] = "consent select_account",
            ["state"] = state,
        };
        return QueryHelpers.AddQueryString("https://accounts.google.com/o/oauth2/v2/auth", query);
    }

    public async Task<GoogleTasksTokenResult> ExchangeCodeAsync(
        string code, CancellationToken cancellationToken)
    {
        var token = await SendTokenRequestAsync(new Dictionary<string, string>
        {
            ["client_id"] = _configuration.ClientId,
            ["client_secret"] = _configuration.ClientSecret,
            ["code"] = code,
            ["redirect_uri"] = _configuration.CallbackUrl,
            ["grant_type"] = "authorization_code",
        }, cancellationToken);
        if (string.IsNullOrWhiteSpace(token.IdToken))
            throw new GoogleTasksProviderException(
                GoogleTasksProviderFailure.InvalidResponse,
                "Google did not return an identity token.");

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(token.IdToken,
                new GoogleJsonWebSignature.ValidationSettings { Audience = [_configuration.ClientId] });
        }
        catch (InvalidJwtException exception)
        {
            throw new GoogleTasksProviderException(
                GoogleTasksProviderFailure.InvalidResponse,
                "Google returned an invalid identity token.", exception);
        }

        if (string.IsNullOrWhiteSpace(payload.Subject)
            || string.IsNullOrWhiteSpace(payload.Email)
            || payload.EmailVerified != true)
            throw new GoogleTasksProviderException(
                GoogleTasksProviderFailure.InvalidResponse,
                "Google did not identify the Tasks account.");

        return new GoogleTasksTokenResult(
            token.AccessToken!,
            token.RefreshToken,
            timeProvider.GetUtcNow() + TimeSpan.FromSeconds(token.ExpiresIn),
            (token.Scope ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries),
            payload.Subject,
            payload.Email);
    }

    public async Task<GoogleTasksRefreshResult> RefreshAsync(
        string refreshToken, CancellationToken cancellationToken)
    {
        var token = await SendTokenRequestAsync(new Dictionary<string, string>
        {
            ["client_id"] = _configuration.ClientId,
            ["client_secret"] = _configuration.ClientSecret,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token",
        }, cancellationToken);
        return new GoogleTasksRefreshResult(
            token.AccessToken!,
            timeProvider.GetUtcNow() + TimeSpan.FromSeconds(token.ExpiresIn));
    }

    public async Task<IReadOnlyList<GoogleProviderTaskList>> ListTaskListsAsync(
        string accessToken, CancellationToken cancellationToken)
    {
        try
        {
            using var service = CreateService(accessToken);
            var request = service.Tasklists.List();
            request.MaxResults = 1000;
            var results = new List<GoogleProviderTaskList>();
            do
            {
                var response = await request.ExecuteAsync(cancellationToken);
                results.AddRange((response.Items ?? []).Select(item =>
                    new GoogleProviderTaskList(item.Id, item.Title ?? "Untitled task list")));
                request.PageToken = response.NextPageToken;
            } while (!string.IsNullOrWhiteSpace(request.PageToken));
            return results;
        }
        catch (GoogleApiException exception)
        {
            throw Map(exception);
        }
    }

    public async Task<GoogleProviderTaskPage> ListTasksAsync(
        string accessToken, string taskListId, bool includeCompleted,
        string? pageToken, int maximumResults, CancellationToken cancellationToken)
    {
        try
        {
            using var service = CreateService(accessToken);
            var request = service.Tasks.List(taskListId);
            request.MaxResults = Math.Clamp(maximumResults, 1, 100);
            request.PageToken = pageToken;
            request.ShowCompleted = includeCompleted;
            request.ShowHidden = includeCompleted;
            request.ShowDeleted = false;
            request.ShowAssigned = true;
            var response = await request.ExecuteAsync(cancellationToken);
            var tasks = (response.Items ?? []).Select(item => new GoogleProviderTask(
                item.Id,
                item.Title ?? "Untitled task",
                string.IsNullOrWhiteSpace(item.Notes) ? null : item.Notes,
                item.Status ?? "needsAction",
                string.IsNullOrWhiteSpace(item.Due) ? null : item.Due[..Math.Min(10, item.Due.Length)],
                DateTimeOffset.TryParse(item.Completed, out var completed) ? completed : null,
                item.Parent,
                item.Position ?? string.Empty,
                item.AssignmentInfo is not null)).ToArray();
            return new GoogleProviderTaskPage(tasks, response.NextPageToken);
        }
        catch (GoogleApiException exception)
        {
            throw Map(exception);
        }
    }

    public async Task RevokeAsync(string token, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(nameof(GoogleTasksProviderClient));
        using var response = await client.PostAsync(
            QueryHelpers.AddQueryString("https://oauth2.googleapis.com/revoke", "token", token),
            null,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new GoogleTasksProviderException(
                GoogleTasksProviderFailure.Unavailable,
                "Google Tasks authorization could not be revoked.");
    }

    private static TasksService CreateService(string accessToken) => new(new BaseClientService.Initializer
    {
        HttpClientInitializer = GoogleCredential.FromAccessToken(accessToken),
        ApplicationName = "Family Dashboard",
    });

    private async Task<OAuthTokenResponse> SendTokenRequestAsync(
        Dictionary<string, string> values, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(nameof(GoogleTasksProviderClient));
        using var response = await client.PostAsync(
            "https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(values),
            cancellationToken);
        var token = await response.Content.ReadFromJsonAsync<OAuthTokenResponse>(cancellationToken);
        if (!response.IsSuccessStatusCode || token is null || string.IsNullOrWhiteSpace(token.AccessToken))
        {
            var failure = response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized
                ? GoogleTasksProviderFailure.ReauthorizationRequired
                : response.StatusCode == HttpStatusCode.TooManyRequests
                    ? GoogleTasksProviderFailure.RateLimited
                    : GoogleTasksProviderFailure.Unavailable;
            throw new GoogleTasksProviderException(failure, "Google rejected the Tasks token request.");
        }
        return token;
    }

    private static GoogleTasksProviderException Map(GoogleApiException exception)
    {
        var failure = exception.HttpStatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                GoogleTasksProviderFailure.ReauthorizationRequired,
            HttpStatusCode.TooManyRequests => GoogleTasksProviderFailure.RateLimited,
            _ => GoogleTasksProviderFailure.Unavailable,
        };
        return new GoogleTasksProviderException(failure, "Google Tasks request failed.", exception);
    }

    private sealed record OAuthTokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("expires_in")] long ExpiresIn,
        [property: JsonPropertyName("scope")] string? Scope,
        [property: JsonPropertyName("id_token")] string? IdToken);
}

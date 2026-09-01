using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FamilyDashboard.Api.Domain.Households;
using FamilyDashboard.Api.Domain.Identity;
using FamilyDashboard.Api.Features.Common;
using FamilyDashboard.Api.Features.HouseholdMembers;
using FamilyDashboard.Api.Features.Households;
using FamilyDashboard.Api.Tests.Authentication;
using FamilyDashboard.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FamilyDashboard.Api.Tests.Endpoints;

[Collection("PostgreSQL integration")]
public sealed class HouseholdEndpointTests
{
    [PostgreSqlFact]
    public async Task BootstrapCreatesHouseholdConfigurationAdultProfileAndMembershipAtomically()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var account = await AddAccountAsync(database, "Taylor Adult", "taylor@example.test");
        using var client = CreateAuthenticatedClient(database, account.Id);

        using var response = await client.PostAsJsonAsync(
            "/api/households",
            new CreateHouseholdRequest(
                "  Taylor Family  ",
                "America/New_York",
                "en-US",
                "monday"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var household = await response.Content.ReadFromJsonAsync<HouseholdResponse>();
        Assert.NotNull(household);
        Assert.Equal("Taylor Family", household.Name);
        Assert.Equal("monday", household.WeekStartsOn);
        Assert.Equal("adult", household.Access.Role);
        Assert.True(household.Access.CanAdminister);

        database.DbContext.ChangeTracker.Clear();
        Assert.Equal(1, await database.DbContext.Households.CountAsync());
        Assert.Equal(1, await database.DbContext.HouseholdConfigurations.CountAsync());
        Assert.Equal(1, await database.DbContext.HouseholdMembers.CountAsync());
        Assert.Equal(1, await database.DbContext.HouseholdMemberships.CountAsync());
        Assert.Equal(
            "Taylor Adult",
            await database.DbContext.HouseholdMembers.Select(member => member.DisplayName).SingleAsync());
    }

    [PostgreSqlFact]
    public async Task ChildProfilesRemainProfileOnlyAndCanBeUpdatedByAnAdult()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var account = await AddAccountAsync(database, "Morgan Adult", "morgan@example.test");
        using var client = CreateAuthenticatedClient(database, account.Id);
        var household = await BootstrapAsync(client, "Morgan Family");

        using var createResponse = await client.PostAsJsonAsync(
            $"/api/households/{household.Id}/members",
            new CreateChildMemberRequest("  Riley  ", "Sky-Blue"));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var child = await createResponse.Content.ReadFromJsonAsync<HouseholdMemberResponse>();
        Assert.NotNull(child);
        Assert.Equal("Riley", child.DisplayName);
        Assert.Equal("child", child.Role);
        Assert.Equal("sky-blue", child.AvatarColor);

        using var updateResponse = await client.PatchAsJsonAsync(
            $"/api/households/{household.Id}/members/{child.Id}",
            new UpdateHouseholdMemberRequest("Riley Updated", null, false));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        database.DbContext.ChangeTracker.Clear();
        Assert.Equal(1, await database.DbContext.UserAccounts.CountAsync());
        Assert.Equal(1, await database.DbContext.HouseholdMemberships.CountAsync());
        Assert.Equal(2, await database.DbContext.HouseholdMembers.CountAsync());
        Assert.False(await database.DbContext.HouseholdMembers
            .Where(member => member.Id == child.Id)
            .Select(member => member.IsActive)
            .SingleAsync());
    }

    [PostgreSqlFact]
    public async Task MemberPhotosRemainPrivateVersionedAndHouseholdIsolated()
    {
        var mediaPath = Path.Combine(Path.GetTempPath(), "family-dashboard-member-photo-tests", Guid.NewGuid().ToString("N"));
        try
        {
            await using var database = await PostgreSqlTestDatabase.CreateAsync(mediaPath);
            var firstAccount = await AddAccountAsync(database, "Photo Adult", "photo-adult@example.test");
            var secondAccount = await AddAccountAsync(database, "Other Adult", "other-photo@example.test");
            using var firstClient = CreateAuthenticatedClient(database, firstAccount.Id);
            using var secondClient = CreateAuthenticatedClient(database, secondAccount.Id);
            var firstHousehold = await BootstrapAsync(firstClient, "Photo Household");
            var secondHousehold = await BootstrapAsync(secondClient, "Other Household");
            var members = await firstClient.GetFromJsonAsync<List<HouseholdMemberResponse>>(
                $"/api/households/{firstHousehold.Id}/members");
            var adult = Assert.Single(members!);

            using var upload = MemberPhotoUploadRequest(
                $"/api/households/{firstHousehold.Id}/members/{adult.Id}/photo",
                adult.PhotoVersion);
            using var uploadResponse = await firstClient.SendAsync(upload);
            Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);
            var uploaded = (await uploadResponse.Content.ReadFromJsonAsync<HouseholdMemberResponse>())!;
            Assert.NotNull(uploaded.Photo);
            Assert.Equal(adult.PhotoVersion + 1, uploaded.PhotoVersion);

            using var imageResponse = await firstClient.GetAsync(uploaded.Photo.MediumUrl);
            Assert.Equal(HttpStatusCode.OK, imageResponse.StatusCode);
            Assert.Equal("image/jpeg", imageResponse.Content.Headers.ContentType?.MediaType);
            Assert.True(imageResponse.Headers.CacheControl?.NoCache);
            Assert.True(imageResponse.Headers.CacheControl?.Private);
            Assert.NotNull(imageResponse.Headers.ETag);

            using var conditional = new HttpRequestMessage(HttpMethod.Get, uploaded.Photo.MediumUrl);
            conditional.Headers.IfNoneMatch.Add(imageResponse.Headers.ETag!);
            using var notModified = await firstClient.SendAsync(conditional);
            Assert.Equal(HttpStatusCode.NotModified, notModified.StatusCode);

            using var isolated = await secondClient.GetAsync(
                uploaded.Photo.MediumUrl.Replace(firstHousehold.Id.ToString(), secondHousehold.Id.ToString(), StringComparison.Ordinal));
            Assert.Equal(HttpStatusCode.NotFound, isolated.StatusCode);

            using var positionResponse = await firstClient.PutAsJsonAsync(
                $"/api/households/{firstHousehold.Id}/members/{adult.Id}/photo-position",
                new UpdateHouseholdMemberPhotoPositionRequest(uploaded.PhotoVersion, 0.2m, 0.8m));
            Assert.Equal(HttpStatusCode.OK, positionResponse.StatusCode);
            var positioned = (await positionResponse.Content.ReadFromJsonAsync<HouseholdMemberResponse>())!;
            Assert.Equal(0.2m, positioned.Photo!.FocalX);
            Assert.Equal(0.8m, positioned.Photo.FocalY);

            using var staleRemoval = await firstClient.SendAsync(new HttpRequestMessage(
                HttpMethod.Delete,
                $"/api/households/{firstHousehold.Id}/members/{adult.Id}/photo")
            {
                Content = JsonContent.Create(new RemoveHouseholdMemberPhotoRequest(uploaded.PhotoVersion)),
            });
            Assert.Equal(HttpStatusCode.Conflict, staleRemoval.StatusCode);

            using var removal = await firstClient.SendAsync(new HttpRequestMessage(
                HttpMethod.Delete,
                $"/api/households/{firstHousehold.Id}/members/{adult.Id}/photo")
            {
                Content = JsonContent.Create(new RemoveHouseholdMemberPhotoRequest(positioned.PhotoVersion)),
            });
            Assert.Equal(HttpStatusCode.OK, removal.StatusCode);
            Assert.Null((await removal.Content.ReadFromJsonAsync<HouseholdMemberResponse>())!.Photo);

            using var removedImage = await firstClient.GetAsync(uploaded.Photo.MediumUrl);
            Assert.Equal(HttpStatusCode.NotFound, removedImage.StatusCode);
            using var invalidUpload = InvalidMemberPhotoUploadRequest(
                $"/api/households/{firstHousehold.Id}/members/{adult.Id}/photo",
                positioned.PhotoVersion + 1);
            using var invalidUploadResponse = await firstClient.SendAsync(invalidUpload);
            Assert.Equal(HttpStatusCode.BadRequest, invalidUploadResponse.StatusCode);
            Assert.Equal(ApiProblemCodes.ValidationFailed, await ReadProblemCodeAsync(invalidUploadResponse));
            database.DbContext.ChangeTracker.Clear();
            Assert.Equal(HouseholdMemberPhotoAssetState.Retired, await database.DbContext.HouseholdMemberPhotoAssets
                .Where(value => value.Id == uploaded.Photo.AssetId)
                .Select(value => value.State)
                .SingleAsync());
        }
        finally
        {
            if (Directory.Exists(mediaPath)) Directory.Delete(mediaPath, recursive: true);
        }
    }

    [PostgreSqlFact]
    public async Task CrossHouseholdAccessReturnsNotFoundWithoutChangingData()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var firstAccount = await AddAccountAsync(database, "First Adult", "first@example.test");
        var secondAccount = await AddAccountAsync(database, "Second Adult", "second@example.test");
        using var firstClient = CreateAuthenticatedClient(database, firstAccount.Id);
        using var secondClient = CreateAuthenticatedClient(database, secondAccount.Id);
        var firstHousehold = await BootstrapAsync(firstClient, "First Household");
        var secondHousehold = await BootstrapAsync(secondClient, "Second Household");

        using var readResponse = await firstClient.GetAsync($"/api/households/{secondHousehold.Id}");
        Assert.Equal(HttpStatusCode.NotFound, readResponse.StatusCode);
        Assert.Equal(ApiProblemCodes.HouseholdNotFound, await ReadProblemCodeAsync(readResponse));

        using var updateResponse = await firstClient.PatchAsJsonAsync(
            $"/api/households/{secondHousehold.Id}",
            new UpdateHouseholdRequest("Compromised", null, null, null));
        Assert.Equal(HttpStatusCode.NotFound, updateResponse.StatusCode);

        database.DbContext.ChangeTracker.Clear();
        Assert.Equal(
            "Second Household",
            await database.DbContext.Households
                .Where(candidate => candidate.Id == secondHousehold.Id)
                .Select(candidate => candidate.Name)
                .SingleAsync());
        Assert.NotEqual(firstHousehold.Id, secondHousehold.Id);
    }

    [PostgreSqlFact]
    public async Task OnlyAdultMustUseTheFutureLeaveHouseholdWorkflow()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var account = await AddAccountAsync(database, "Only Adult", "only@example.test");
        using var client = CreateAuthenticatedClient(database, account.Id);
        var household = await BootstrapAsync(client, "Only Household");

        using var response = await client.PatchAsJsonAsync(
            $"/api/households/{household.Id}/members/{household.Access.MemberId}",
            new UpdateHouseholdMemberRequest(null, null, false));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            ApiProblemCodes.SelfDeactivationRequiresLeaveFlow,
            await ReadProblemCodeAsync(response));
        database.DbContext.ChangeTracker.Clear();
        Assert.True(await database.DbContext.HouseholdMembers
            .Where(member => member.Id == household.Access.MemberId)
            .Select(member => member.IsActive)
            .SingleAsync());
    }

    [PostgreSqlFact]
    public async Task AdultCanBeDeactivatedWhenAnotherActiveAdultRemains()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var firstAccount = await AddAccountAsync(database, "First Adult", "first-adult@example.test");
        var secondAccount = await AddAccountAsync(database, "Second Adult", "second-adult@example.test");
        using var firstClient = CreateAuthenticatedClient(database, firstAccount.Id);
        var household = await BootstrapAsync(firstClient, "Two Adult Household");
        var secondMember = new HouseholdMember
        {
            HouseholdId = household.Id,
            DisplayName = secondAccount.DisplayName,
            Role = HouseholdMemberRole.Adult,
        };
        database.DbContext.HouseholdMembers.Add(secondMember);
        database.DbContext.HouseholdMemberships.Add(new HouseholdMembership
        {
            UserAccountId = secondAccount.Id,
            HouseholdId = household.Id,
            HouseholdMemberId = secondMember.Id,
        });
        await database.DbContext.SaveChangesAsync();

        using var secondClient = CreateAuthenticatedClient(database, secondAccount.Id);
        using var response = await secondClient.PatchAsJsonAsync(
            $"/api/households/{household.Id}/members/{household.Access.MemberId}",
            new UpdateHouseholdMemberRequest(null, null, false));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        database.DbContext.ChangeTracker.Clear();
        Assert.Equal(1, await database.DbContext.HouseholdMembers.CountAsync(
            member => member.HouseholdId == household.Id
                && member.IsActive
                && member.Role == HouseholdMemberRole.Adult));
    }

    [PostgreSqlFact]
    public async Task AdultCannotDeactivateTheirOwnLinkedProfile()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var firstAccount = await AddAccountAsync(database, "First Adult", "first-self@example.test");
        var secondAccount = await AddAccountAsync(database, "Second Adult", "second-self@example.test");
        using var client = CreateAuthenticatedClient(database, firstAccount.Id);
        var household = await BootstrapAsync(client, "Self Protection Household");
        var secondMember = new HouseholdMember
        {
            HouseholdId = household.Id,
            DisplayName = secondAccount.DisplayName,
            Role = HouseholdMemberRole.Adult,
        };
        database.DbContext.HouseholdMembers.Add(secondMember);
        database.DbContext.HouseholdMemberships.Add(new HouseholdMembership
        {
            UserAccountId = secondAccount.Id,
            HouseholdId = household.Id,
            HouseholdMemberId = secondMember.Id,
        });
        await database.DbContext.SaveChangesAsync();

        using var response = await client.PatchAsJsonAsync(
            $"/api/households/{household.Id}/members/{household.Access.MemberId}",
            new UpdateHouseholdMemberRequest(null, null, false));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            ApiProblemCodes.SelfDeactivationRequiresLeaveFlow,
            await ReadProblemCodeAsync(response));
        database.DbContext.ChangeTracker.Clear();
        Assert.True(await database.DbContext.HouseholdMembers
            .Where(member => member.Id == household.Access.MemberId)
            .Select(member => member.IsActive)
            .SingleAsync());
    }

    [PostgreSqlFact]
    public async Task InvalidHouseholdInputReturnsFieldErrors()
    {
        await using var database = await PostgreSqlTestDatabase.CreateAsync();
        var account = await AddAccountAsync(database, "Validation Adult", "validation@example.test");
        using var client = CreateAuthenticatedClient(database, account.Id);

        using var response = await client.PostAsJsonAsync(
            "/api/households",
            new CreateHouseholdRequest(" ", "Not/AZone", "not-a-locale", "Funday"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            ApiProblemCodes.ValidationFailed,
            document.RootElement.GetProperty("code").GetString());
        var errors = document.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("name", out _));
        Assert.True(errors.TryGetProperty("timeZone", out _));
        Assert.True(errors.TryGetProperty("locale", out _));
        Assert.True(errors.TryGetProperty("weekStartsOn", out _));
    }

    private static async Task<UserAccount> AddAccountAsync(
        PostgreSqlTestDatabase database,
        string displayName,
        string email)
    {
        var account = new UserAccount
        {
            DisplayName = displayName,
            PrimaryEmail = email,
        };
        database.DbContext.UserAccounts.Add(account);
        await database.DbContext.SaveChangesAsync();
        return account;
    }

    private static HttpClient CreateAuthenticatedClient(
        PostgreSqlTestDatabase database,
        Guid userAccountId)
    {
        var client = database.Factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            TestAuthenticationHandler.UserIdHeaderName,
            userAccountId.ToString());
        return client;
    }

    private static async Task<HouseholdResponse> BootstrapAsync(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/households",
            new CreateHouseholdRequest(name, "America/New_York", "en-US", "Sunday"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<HouseholdResponse>())!;
    }

    private static async Task<string?> ReadProblemCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString();
    }

    private static HttpRequestMessage MemberPhotoUploadRequest(string path, long photoVersion)
    {
        var bytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
        var image = new ByteArrayContent(bytes);
        image.Headers.ContentType = new("image/png");
        var content = new MultipartFormDataContent();
        content.Add(image, "photo", "member.png");
        content.Add(new StringContent(photoVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)), "expectedPhotoVersion");
        return new HttpRequestMessage(HttpMethod.Post, path) { Content = content };
    }

    private static HttpRequestMessage InvalidMemberPhotoUploadRequest(string path, long photoVersion)
    {
        var image = new ByteArrayContent("not an image"u8.ToArray());
        image.Headers.ContentType = new("image/png");
        var content = new MultipartFormDataContent();
        content.Add(image, "photo", "member.png");
        content.Add(new StringContent(photoVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)), "expectedPhotoVersion");
        return new HttpRequestMessage(HttpMethod.Post, path) { Content = content };
    }
}

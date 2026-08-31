using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using FamilyDashboard.Api.Configuration;
using Microsoft.Extensions.Options;

namespace FamilyDashboard.Api.Features.Dashboard;

public interface IHouseholdPhotoStore
{
    Task WriteAsync(string path, Stream content, string contentType, CancellationToken cancellationToken);
    Task<HouseholdPhotoContent?> ReadAsync(string path, CancellationToken cancellationToken);
    Task DeletePrefixAsync(string prefix, CancellationToken cancellationToken);
}

public sealed record HouseholdPhotoContent(Stream Content, string ContentType, string ETag, long Length);

internal sealed class FileSystemHouseholdPhotoStore(IOptions<HouseholdMediaConfiguration> options) : IHouseholdPhotoStore
{
    private readonly string root = Path.GetFullPath(options.Value.LocalPath);

    public async Task WriteAsync(string path, Stream content, string contentType, CancellationToken cancellationToken)
    {
        var target = Resolve(path);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        await using var output = File.Create(target);
        await content.CopyToAsync(output, cancellationToken);
    }

    public Task<HouseholdPhotoContent?> ReadAsync(string path, CancellationToken cancellationToken)
    {
        var target = Resolve(path);
        if (!File.Exists(target)) return Task.FromResult<HouseholdPhotoContent?>(null);
        var info = new FileInfo(target);
        return Task.FromResult<HouseholdPhotoContent?>(new(
            File.Open(target, FileMode.Open, FileAccess.Read, FileShare.Read),
            "image/jpeg", $"\"{info.Length:x}-{info.LastWriteTimeUtc.Ticks:x}\"", info.Length));
    }

    public Task DeletePrefixAsync(string prefix, CancellationToken cancellationToken)
    {
        var directory = Resolve(prefix);
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
        return Task.CompletedTask;
    }

    private string Resolve(string path)
    {
        var target = Path.GetFullPath(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)));
        if (!target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("Photo path escaped the configured storage root.");
        return target;
    }
}

internal sealed class AzureBlobHouseholdPhotoStore : IHouseholdPhotoStore
{
    private readonly BlobContainerClient container;

    public AzureBlobHouseholdPhotoStore(IOptions<HouseholdMediaConfiguration> options)
    {
        var value = options.Value;
        var credential = string.IsNullOrWhiteSpace(value.ManagedIdentityClientId)
            ? new DefaultAzureCredential()
            : new DefaultAzureCredential(new DefaultAzureCredentialOptions { ManagedIdentityClientId = value.ManagedIdentityClientId });
        container = new BlobContainerClient(new Uri(value.BlobContainerUri), credential);
    }

    public async Task WriteAsync(string path, Stream content, string contentType, CancellationToken cancellationToken)
    {
        await container.GetBlobClient(path).UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType, CacheControl = "private, max-age=31536000, immutable" },
        }, cancellationToken);
    }

    public async Task<HouseholdPhotoContent?> ReadAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var response = await container.GetBlobClient(path).DownloadStreamingAsync(cancellationToken: cancellationToken);
            return new(response.Value.Content, response.Value.Details.ContentType,
                response.Value.Details.ETag.ToString(), response.Value.Details.ContentLength);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    public async Task DeletePrefixAsync(string prefix, CancellationToken cancellationToken)
    {
        await foreach (var item in container.GetBlobsAsync(BlobTraits.None, BlobStates.None,
                           prefix.TrimEnd('/') + '/', cancellationToken))
            await container.DeleteBlobIfExistsAsync(item.Name, cancellationToken: cancellationToken);
    }
}

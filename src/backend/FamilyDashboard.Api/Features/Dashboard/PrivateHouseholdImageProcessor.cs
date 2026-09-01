using FamilyDashboard.Api.Configuration;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace FamilyDashboard.Api.Features.Dashboard;

internal sealed record ProcessedHouseholdImage(
    int PixelWidth,
    int PixelHeight,
    IReadOnlyDictionary<string, byte[]> Variants)
{
    public long TotalByteLength => Variants.Values.Sum(value => (long)value.Length);
}

internal sealed class PrivateHouseholdImageProcessor(IOptions<HouseholdMediaConfiguration> mediaOptions)
{
    public async Task<ProcessedHouseholdImage> ProcessAsync(
        Stream upload,
        long uploadLength,
        IReadOnlyDictionary<string, int> variantMaximumEdges,
        CancellationToken cancellationToken)
    {
        var options = mediaOptions.Value;
        if (uploadLength is <= 0 || uploadLength > options.MaximumUploadBytes)
            throw new InvalidHouseholdPhotoException("Photo must be between 1 byte and 10 MB.");

        using var buffered = new MemoryStream((int)Math.Min(uploadLength, options.MaximumUploadBytes));
        await upload.CopyToAsync(buffered, cancellationToken);
        if (buffered.Length > options.MaximumUploadBytes)
            throw new InvalidHouseholdPhotoException("Photo cannot exceed 10 MB.");
        buffered.Position = 0;
        ImageInfo? info;
        try
        {
            info = await Image.IdentifyAsync(buffered, cancellationToken);
        }
        catch (Exception exception) when (exception is UnknownImageFormatException or InvalidImageContentException)
        {
            throw new InvalidHouseholdPhotoException("Use a valid JPEG, PNG, or WebP photo.");
        }
        if (info is null || info.Width <= 0 || info.Height <= 0
            || info.Width > options.MaximumDimension || info.Height > options.MaximumDimension
            || (long)info.Width * info.Height > options.MaximumPixelCount)
            throw new InvalidHouseholdPhotoException("Photo dimensions are invalid or exceed the safe processing limit.");
        var format = info.Metadata.DecodedImageFormat?.Name?.ToUpperInvariant();
        if (format is not ("JPEG" or "PNG" or "WEBP"))
            throw new InvalidHouseholdPhotoException("Use a JPEG, PNG, or WebP photo.");

        buffered.Position = 0;
        Image image;
        try
        {
            image = await Image.LoadAsync(buffered, cancellationToken);
        }
        catch (Exception exception) when (exception is UnknownImageFormatException or InvalidImageContentException)
        {
            throw new InvalidHouseholdPhotoException("Use a valid JPEG, PNG, or WebP photo.");
        }
        using (image)
        {
            if (image.Frames.Count != 1)
                throw new InvalidHouseholdPhotoException("Animated or multi-frame photos are not supported.");
            image.Mutate(context => context.AutoOrient());

            var variants = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var variant in variantMaximumEdges)
            {
                var scale = Math.Min(1d, variant.Value / (double)Math.Max(image.Width, image.Height));
                var targetWidth = Math.Max(1, (int)Math.Round(image.Width * scale));
                var targetHeight = Math.Max(1, (int)Math.Round(image.Height * scale));
                using var clone = image.Clone(context =>
                {
                    if (scale < 1d) context.Resize(targetWidth, targetHeight);
                });
                clone.Metadata.ExifProfile = null;
                clone.Metadata.IccProfile = null;
                clone.Metadata.IptcProfile = null;
                clone.Metadata.XmpProfile = null;
                using var output = new MemoryStream();
                await clone.SaveAsJpegAsync(output, new JpegEncoder { Quality = 84 }, cancellationToken);
                variants.Add(variant.Key, output.ToArray());
            }

            return new ProcessedHouseholdImage(image.Width, image.Height, variants);
        }
    }
}

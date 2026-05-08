using AltTextGen.Server.Configuration;
using AltTextGen.Server.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;

namespace AltTextGen.Server.Services;

public sealed class AltTextMediaGenerationService(
    IMediaService mediaService,
    MediaFileManager mediaFileManager,
    IAltTextGenerationService altTextGenerationService,
    IOptionsMonitor<AltTextGenerationOptions> optionsMonitor,
    ILogger<AltTextMediaGenerationService> logger) : IAltTextMediaGenerationService
{
    private const string UmbracoFilePropertyAlias = "umbracoFile";

    public async Task<AltTextMediaGenerationResult> GenerateForMediaAsync(GenerateAltTextRequest request, CancellationToken cancellationToken)
    {
        AltTextGenerationOptions options = optionsMonitor.CurrentValue;

        if (!options.Enabled)
        {
            return new AltTextMediaGenerationResult(
                AltTextMediaGenerationStatus.Disabled,
                "Alt text generation is disabled.");
        }

        if (request.MediaKey == Guid.Empty)
        {
            return new AltTextMediaGenerationResult(
                AltTextMediaGenerationStatus.InvalidRequest,
                "A media key is required.");
        }

        IMedia? media = mediaService.GetById(request.MediaKey);
        if (media is null)
        {
            return new AltTextMediaGenerationResult(
                AltTextMediaGenerationStatus.NotFound,
                $"Media item '{request.MediaKey}' was not found.");
        }

        logger.LogInformation(
            "AltTextGen media loaded. Key={MediaKey} Name={MediaName} Type={MediaTypeAlias} FileValue={FileValue}",
            media.Key,
            media.Name,
            media.ContentType.Alias,
            media.GetValue(UmbracoFilePropertyAlias)?.ToString());

        if (!IsAllowedMediaType(media, options))
        {
            return new AltTextMediaGenerationResult(
                AltTextMediaGenerationStatus.InvalidRequest,
                $"Media item '{media.Name}' is not configured as an image media type.");
        }

        if (!media.HasProperty(options.AltTextPropertyAlias))
        {
            return new AltTextMediaGenerationResult(
                AltTextMediaGenerationStatus.InvalidRequest,
                $"Media item '{media.Name}' does not have an '{options.AltTextPropertyAlias}' property.");
        }

        string? existingAltText = media.GetValue(options.AltTextPropertyAlias, request.Culture, request.Segment)?.ToString();
        if (!request.Overwrite && !string.IsNullOrWhiteSpace(existingAltText))
        {
            return new AltTextMediaGenerationResult(
                AltTextMediaGenerationStatus.Conflict,
                $"Media item '{media.Name}' already has alt text.");
        }

        string? fileValue = media.GetValue(UmbracoFilePropertyAlias)?.ToString();
        MediaImageFile imageFile;
        try
        {
            imageFile = await ReadMediaImageFileAsync(media, options, cancellationToken);
        }
        catch (FileNotFoundException ex)
        {
            logger.LogWarning(ex, "AltTextGen media file was not found for media {MediaKey}.", media.Key);
            return new AltTextMediaGenerationResult(AltTextMediaGenerationStatus.NotFound, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "AltTextGen media file could not be loaded for media {MediaKey}.", media.Key);
            return new AltTextMediaGenerationResult(AltTextMediaGenerationStatus.InvalidRequest, ex.Message);
        }

        logger.LogInformation(
            "AltTextGen media file loaded. Key={MediaKey} Path={FilePath} MediaType={MediaType} Bytes={ByteCount}",
            media.Key,
            imageFile.Path,
            imageFile.MediaType,
            imageFile.Bytes.Length);

        AltTextGenerationContext context = new(
            media.Key,
            media.Name ?? string.Empty,
            media.ContentType.Alias,
            fileValue,
            imageFile.Path,
            imageFile.MediaType,
            imageFile.Bytes,
            existingAltText,
            request.Culture,
            request.Segment);

        AltTextGenerationResult result;
        try
        {
            result = await altTextGenerationService.GenerateAsync(context, cancellationToken);
            logger.LogInformation("AltTextGen generation result: {Result}", result);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "AltTextGen generation failed validation for media {MediaKey}.", media.Key);
            return new AltTextMediaGenerationResult(AltTextMediaGenerationStatus.InvalidRequest, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AltTextGen generation failed for media {MediaKey}.", media.Key);
            return new AltTextMediaGenerationResult(
                AltTextMediaGenerationStatus.ProviderError,
                "Alt text generation failed while calling the AI provider.");
        }

        media.SetValue(options.AltTextPropertyAlias, result.AltText, request.Culture, request.Segment);
        mediaService.Save(media);

        logger.LogInformation(
            "Generated alt text for media {MediaKey} using provider {Provider}.",
            media.Key,
            result.Provider);

        return new AltTextMediaGenerationResult(
            AltTextMediaGenerationStatus.Success,
            "Alt text generated.",
            new GenerateAltTextResponse(
                media.Key,
                options.AltTextPropertyAlias,
                result.AltText,
                result.Provider,
                Saved: true));
    }

    private static bool IsAllowedMediaType(IMedia media, AltTextGenerationOptions options)
    {
        if (options.AllowedMediaTypeAliases.Length == 0)
        {
            return true;
        }

        return options.AllowedMediaTypeAliases.Contains(media.ContentType.Alias, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<MediaImageFile> ReadMediaImageFileAsync(IMedia media, AltTextGenerationOptions options, CancellationToken cancellationToken)
    {
        Stream stream;
        string? relativePath = null;
        try
        {
            stream = mediaFileManager.GetFile(media, out relativePath, UmbracoFilePropertyAlias);
        }
        catch (FileNotFoundException ex)
        {
            throw new FileNotFoundException(
                "The media image file was not found on the server. Re-upload the file in the Media library, or copy it under wwwroot/media so it matches the path in the umbracoFile property (uSync imports media nodes, not binaries).",
                ex.FileName ?? relativePath,
                ex);
        }

        if (ReferenceEquals(stream, Stream.Null) || string.IsNullOrWhiteSpace(relativePath))
        {
            throw new InvalidOperationException("The media item does not contain an image file path.");
        }

        string extension = Path.GetExtension(relativePath).ToLowerInvariant();
        HashSet<string> allowedExtensions = GetAllowedExtensions(options);
        if (!allowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException(
                $"The media file type '{extension}' is not supported. Allowed extensions: {string.Join(", ", allowedExtensions.OrderBy(value => value))}.");
        }

        string mediaType = GetImageMediaType(extension);
        if (!mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"The media file type '{extension}' is not supported.");
        }

        await using (stream)
        {
            using MemoryStream memoryStream = new();
            await stream.CopyToAsync(memoryStream, cancellationToken);
            return new MediaImageFile(relativePath, mediaType, memoryStream.ToArray());
        }
    }

    private static HashSet<string> GetAllowedExtensions(AltTextGenerationOptions options)
    {
        return options.AllowedFileExtensions
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(NormalizeExtension)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeExtension(string value)
    {
        string extension = value.Trim().ToLowerInvariant();
        return extension.StartsWith('.') ? extension : $".{extension}";
    }

    private static string GetImageMediaType(string extension)
    {
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }

    private sealed record MediaImageFile(string Path, string MediaType, byte[] Bytes);
}

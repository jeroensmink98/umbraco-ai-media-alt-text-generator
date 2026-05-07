using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AltTextGen.Server.Configuration;
using AltTextGen.Server.Models;
using AltTextGen.Server.Services;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Web.Common.Controllers;

namespace AltTextGen.Server.Controllers;

[ApiController]
[Route("umbraco/backoffice/api/alt-text-generation")]
public sealed class AltTextGenerationController(
    IMediaService mediaService,
    MediaFileManager mediaFileManager,
    IAltTextGenerationService altTextGenerationService,
    IOptionsMonitor<AltTextGenerationOptions> optionsMonitor,
    ILogger<AltTextGenerationController> logger) : UmbracoAuthorizedController
{
    private const string UmbracoFilePropertyAlias = "umbracoFile";

    [HttpPost("generate")]
    public async Task<IActionResult> GenerateAsync(
        [FromBody] GenerateAltTextRequest request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "AltTextGen request received. MediaKey={MediaKey} Culture={Culture} Segment={Segment} Overwrite={Overwrite} User={UserName}",
            request.MediaKey,
            request.Culture,
            request.Segment,
            request.Overwrite,
            User?.Identity?.Name ?? "(unknown)");

        AltTextGenerationOptions options = optionsMonitor.CurrentValue;

        if (!options.Enabled)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Alt text generation is disabled.");
        }

        if (request.MediaKey == Guid.Empty)
        {
            return BadRequest("A media key is required.");
        }

        IMedia? media = mediaService.GetById(request.MediaKey);
        if (media is null)
        {
            return NotFound($"Media item '{request.MediaKey}' was not found.");
        }

        logger.LogInformation(
            "AltTextGen media loaded. Key={MediaKey} Name={MediaName} Type={MediaTypeAlias} FileValue={FileValue}",
            media.Key,
            media.Name,
            media.ContentType.Alias,
            media.GetValue(UmbracoFilePropertyAlias)?.ToString());

        if (!IsAllowedMediaType(media, options))
        {
            return BadRequest($"Media item '{media.Name}' is not configured as an image media type.");
        }

        if (!media.HasProperty(options.AltTextPropertyAlias))
        {
            return BadRequest($"Media item '{media.Name}' does not have an '{options.AltTextPropertyAlias}' property.");
        }

        string? existingAltText = media.GetValue(options.AltTextPropertyAlias, request.Culture, request.Segment)?.ToString();
        if (!request.Overwrite && !string.IsNullOrWhiteSpace(existingAltText))
        {
            return Conflict($"Media item '{media.Name}' already has alt text.");
        }

        string? fileValue = media.GetValue(UmbracoFilePropertyAlias)?.ToString();
        MediaImageFile imageFile;
        try
        {
            imageFile = await ReadMediaImageFileAsync(media, cancellationToken);
        }
        catch (FileNotFoundException ex)
        {
            logger.LogWarning(ex, "AltTextGen media file was not found for media {MediaKey}.", media.Key);
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "AltTextGen media file could not be loaded for media {MediaKey}.", media.Key);
            return BadRequest(ex.Message);
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
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AltTextGen generation failed for media {MediaKey}.", media.Key);
            return StatusCode(StatusCodes.Status502BadGateway, "Alt text generation failed while calling the AI provider.");
        }

        media.SetValue(options.AltTextPropertyAlias, result.AltText, request.Culture, request.Segment);
        mediaService.Save(media);

        logger.LogInformation(
            "Generated alt text for media {MediaKey} using provider {Provider}.",
            media.Key,
            result.Provider);

        return Ok(new GenerateAltTextResponse(
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

    private async Task<MediaImageFile> ReadMediaImageFileAsync(IMedia media, CancellationToken cancellationToken)
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

        string mediaType = GetImageMediaType(relativePath);
        if (!mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"The media file type '{Path.GetExtension(relativePath)}' is not supported.");
        }

        await using (stream)
        {
            using MemoryStream memoryStream = new();
            await stream.CopyToAsync(memoryStream, cancellationToken);
            return new MediaImageFile(relativePath, mediaType, memoryStream.ToArray());
        }
    }

    private static string GetImageMediaType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".svg" => "image/svg+xml",
            _ => "application/octet-stream"
        };
    }

    private sealed record MediaImageFile(string Path, string MediaType, byte[] Bytes);
}

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AltTextGen.Server.Configuration;
using AltTextGen.Server.Models;
using AltTextGen.Server.Services;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Web.Common.Controllers;

namespace AltTextGen.Server.Controllers;

[ApiController]
[Route("umbraco/backoffice/api/alt-text-generation")]
public sealed class AltTextGenerationController(
    IMediaService mediaService,
    IAltTextMediaGenerationService mediaGenerationService,
    IAltTextBatchJobQueue batchJobQueue,
    AltTextBatchJobStore batchJobStore,
    IOptionsMonitor<AltTextGenerationOptions> optionsMonitor,
    ILogger<AltTextGenerationController> logger) : UmbracoAuthorizedController
{
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

        AltTextMediaGenerationResult result = await mediaGenerationService.GenerateForMediaAsync(request, cancellationToken);
        return ToHttpResult(result);
    }

    [HttpPost("generate-under-folder")]
    public async Task<IActionResult> GenerateUnderFolderAsync(
        [FromBody] GenerateAltTextUnderFolderRequest request,
        CancellationToken cancellationToken)
    {
        AltTextGenerationOptions options = optionsMonitor.CurrentValue;

        if (!options.Enabled)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Alt text generation is disabled.");
        }

        if (request.ParentMediaKey == Guid.Empty)
        {
            return BadRequest("A parent media key is required.");
        }

        IMedia? parentMedia = mediaService.GetById(request.ParentMediaKey);
        if (parentMedia is null)
        {
            return NotFound($"Media item '{request.ParentMediaKey}' was not found.");
        }

        if (!parentMedia.ContentType.Alias.Equals("Folder", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest($"Media item '{parentMedia.Name}' is not a Folder.");
        }

        IReadOnlyList<Guid> mediaKeys = await GetDescendantImageKeysAsync(parentMedia, options, cancellationToken);
        if (mediaKeys.Count == 0)
        {
            return BadRequest("No eligible image media items were found under this folder.");
        }

        AltTextBatchJobState job = batchJobStore.CreateQueuedJob(
            parentMedia.Key,
            parentMedia.Name ?? "Folder",
            mediaKeys.Count);

        await batchJobQueue.QueueAsync(
            new AltTextBatchJobWorkItem(job.JobId, mediaKeys, request.Culture, request.Segment, request.Overwrite),
            cancellationToken);

        return Accepted(new GenerateAltTextUnderFolderResponse(
            job.JobId,
            job.ParentMediaKey,
            job.ParentMediaName,
            job.TotalItems));
    }

    [HttpGet("jobs/{jobId:guid}")]
    public IActionResult GetBatchJobStatus(Guid jobId)
    {
        if (!batchJobStore.TryGetJob(jobId, out AltTextBatchJobState? job) || job is null)
        {
            return NotFound($"Batch job '{jobId}' was not found.");
        }

        return Ok(job.ToResponse());
    }

    private async Task<IReadOnlyList<Guid>> GetDescendantImageKeysAsync(
        IMedia parentMedia,
        AltTextGenerationOptions options,
        CancellationToken cancellationToken)
    {
        const int pageSize = 200;
        long pageIndex = 0;
        long totalRecords = 0;
        List<Guid> keys = [];

        do
        {
            cancellationToken.ThrowIfCancellationRequested();

            IEnumerable<IMedia> page = mediaService.GetPagedDescendants(
                parentMedia.Id,
                pageIndex,
                pageSize,
                out totalRecords);

            keys.AddRange(page
                .Where(media => IsAllowedMediaTypeAlias(media.ContentType.Alias, options))
                .Select(media => media.Key));

            pageIndex++;
        } while (pageIndex * pageSize < totalRecords);

        return keys;
    }

    private static bool IsAllowedMediaTypeAlias(string mediaTypeAlias, AltTextGenerationOptions options)
    {
        if (options.AllowedMediaTypeAliases.Length == 0)
        {
            return true;
        }

        return options.AllowedMediaTypeAliases.Contains(mediaTypeAlias, StringComparer.OrdinalIgnoreCase);
    }

    private IActionResult ToHttpResult(AltTextMediaGenerationResult result)
    {
        return result.Status switch
        {
            AltTextMediaGenerationStatus.Success => Ok(result.Response),
            AltTextMediaGenerationStatus.Disabled => StatusCode(StatusCodes.Status503ServiceUnavailable, result.Message),
            AltTextMediaGenerationStatus.InvalidRequest => BadRequest(result.Message),
            AltTextMediaGenerationStatus.NotFound => NotFound(result.Message),
            AltTextMediaGenerationStatus.Conflict => Conflict(result.Message),
            AltTextMediaGenerationStatus.ProviderError => StatusCode(StatusCodes.Status502BadGateway, result.Message),
            _ => StatusCode(StatusCodes.Status500InternalServerError, "Unexpected alt text generation result.")
        };
    }
}

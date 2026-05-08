using AltTextGen.Server.Configuration;
using AltTextGen.Server.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AltTextGen.Server.Services;

public sealed class AltTextBatchWorker(
    IAltTextBatchJobQueue queue,
    AltTextBatchJobStore jobStore,
    IAltTextMediaGenerationService mediaGenerationService,
    IOptionsMonitor<AltTextGenerationOptions> optionsMonitor,
    ILogger<AltTextBatchWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (AltTextBatchJobWorkItem workItem in queue.DequeueAsync(stoppingToken))
        {
            if (!jobStore.TryGetJob(workItem.JobId, out AltTextBatchJobState? jobState) || jobState is null)
            {
                logger.LogWarning("AltTextGen batch job {JobId} is missing from the store.", workItem.JobId);
                continue;
            }

            jobState.MarkRunning();

            try
            {
                AltTextGenerationOptions options = optionsMonitor.CurrentValue;
                int concurrency = Math.Max(1, options.BatchConcurrency);

                logger.LogInformation(
                    "AltTextGen batch job {JobId} started. Items={ItemCount} Concurrency={Concurrency}",
                    workItem.JobId,
                    workItem.MediaKeys.Count,
                    concurrency);

                ParallelOptions parallelOptions = new()
                {
                    CancellationToken = stoppingToken,
                    MaxDegreeOfParallelism = concurrency
                };

                await Parallel.ForEachAsync(workItem.MediaKeys, parallelOptions, async (mediaKey, cancellationToken) =>
                {
                    AltTextMediaGenerationResult result = await mediaGenerationService.GenerateForMediaAsync(
                        new GenerateAltTextRequest(mediaKey, workItem.Culture, workItem.Segment, workItem.Overwrite),
                        cancellationToken);

                    jobState.RecordResult(result);
                });

                jobState.MarkCompleted();
                logger.LogInformation("AltTextGen batch job {JobId} completed.", workItem.JobId);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                jobState.MarkFailed("Batch processing was cancelled because the application is shutting down.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "AltTextGen batch job {JobId} failed.", workItem.JobId);
                jobState.MarkFailed("Batch processing failed unexpectedly.");
            }
        }
    }
}

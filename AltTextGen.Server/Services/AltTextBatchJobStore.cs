using AltTextGen.Server.Models;
using System.Collections.Concurrent;

namespace AltTextGen.Server.Services;

public enum AltTextBatchJobStatus
{
    Queued,
    Running,
    Completed,
    Failed
}

public sealed class AltTextBatchJobStore
{
    private readonly ConcurrentDictionary<Guid, AltTextBatchJobState> _jobs = new();

    public AltTextBatchJobState CreateQueuedJob(Guid parentMediaKey, string parentMediaName, int totalItems)
    {
        AltTextBatchJobState state = new(
            Guid.NewGuid(),
            parentMediaKey,
            parentMediaName,
            totalItems);

        _jobs[state.JobId] = state;
        return state;
    }

    public bool TryGetJob(Guid jobId, out AltTextBatchJobState? state)
    {
        bool found = _jobs.TryGetValue(jobId, out AltTextBatchJobState? existing);
        state = existing;
        return found;
    }
}

public sealed class AltTextBatchJobState
{
    private readonly object _sync = new();

    public AltTextBatchJobState(Guid jobId, Guid parentMediaKey, string parentMediaName, int totalItems)
    {
        JobId = jobId;
        ParentMediaKey = parentMediaKey;
        ParentMediaName = parentMediaName;
        TotalItems = totalItems;
        Status = AltTextBatchJobStatus.Queued;
        CreatedUtc = DateTimeOffset.UtcNow;
    }

    public Guid JobId { get; }
    public Guid ParentMediaKey { get; }
    public string ParentMediaName { get; }
    public int TotalItems { get; }
    public int ProcessedItems { get; private set; }
    public int SucceededItems { get; private set; }
    public int FailedItems { get; private set; }
    public int SkippedItems { get; private set; }
    public AltTextBatchJobStatus Status { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset CreatedUtc { get; }
    public DateTimeOffset? StartedUtc { get; private set; }
    public DateTimeOffset? CompletedUtc { get; private set; }

    public void MarkRunning()
    {
        lock (_sync)
        {
            Status = AltTextBatchJobStatus.Running;
            StartedUtc ??= DateTimeOffset.UtcNow;
        }
    }

    public void RecordResult(AltTextMediaGenerationResult result)
    {
        lock (_sync)
        {
            ProcessedItems++;

            switch (result.Status)
            {
                case AltTextMediaGenerationStatus.Success:
                    SucceededItems++;
                    break;
                case AltTextMediaGenerationStatus.Conflict:
                case AltTextMediaGenerationStatus.InvalidRequest:
                case AltTextMediaGenerationStatus.NotFound:
                    SkippedItems++;
                    break;
                default:
                    FailedItems++;
                    break;
            }
        }
    }

    public void MarkCompleted()
    {
        lock (_sync)
        {
            Status = AltTextBatchJobStatus.Completed;
            CompletedUtc = DateTimeOffset.UtcNow;
        }
    }

    public void MarkFailed(string message)
    {
        lock (_sync)
        {
            Status = AltTextBatchJobStatus.Failed;
            ErrorMessage = message;
            CompletedUtc = DateTimeOffset.UtcNow;
        }
    }

    public AltTextBatchJobStatusResponse ToResponse()
    {
        lock (_sync)
        {
            return new AltTextBatchJobStatusResponse(
                JobId,
                Status.ToString(),
                ParentMediaKey,
                ParentMediaName,
                TotalItems,
                ProcessedItems,
                SucceededItems,
                SkippedItems,
                FailedItems,
                ErrorMessage,
                CreatedUtc,
                StartedUtc,
                CompletedUtc);
        }
    }
}

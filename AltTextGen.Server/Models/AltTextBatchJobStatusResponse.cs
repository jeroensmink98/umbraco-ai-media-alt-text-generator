namespace AltTextGen.Server.Models;

public sealed record AltTextBatchJobStatusResponse(
    Guid JobId,
    string Status,
    Guid ParentMediaKey,
    string ParentMediaName,
    int TotalItems,
    int ProcessedItems,
    int SucceededItems,
    int SkippedItems,
    int FailedItems,
    string? ErrorMessage,
    DateTimeOffset CreatedUtc,
    DateTimeOffset? StartedUtc,
    DateTimeOffset? CompletedUtc);

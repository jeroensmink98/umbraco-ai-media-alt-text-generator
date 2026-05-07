namespace AltTextGen.Server.Services;

public sealed record AltTextGenerationContext(
    Guid MediaKey,
    string MediaName,
    string MediaTypeAlias,
    string? FileValue,
    string FilePath,
    string ImageMediaType,
    byte[] ImageBytes,
    string? ExistingAltText,
    string? Culture,
    string? Segment);

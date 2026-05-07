namespace AltTextGen.Server.Models;

public sealed record GenerateAltTextRequest(
    Guid MediaKey,
    string? Culture,
    string? Segment,
    bool Overwrite = true);

namespace AltTextGen.Server.Models;

public sealed record GenerateAltTextUnderFolderRequest(
    Guid ParentMediaKey,
    string? Culture,
    string? Segment,
    bool Overwrite = true);

namespace AltTextGen.Server.Models;

public sealed record GenerateAltTextUnderFolderResponse(
    Guid JobId,
    Guid ParentMediaKey,
    string ParentMediaName,
    int TotalItemsQueued);
